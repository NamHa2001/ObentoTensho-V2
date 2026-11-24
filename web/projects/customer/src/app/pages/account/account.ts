import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Schemas } from '@ot/api';
import { CalendarDays, ChevronRight, Gift, LayoutDashboard, LogOut, LucideAngularModule, ReceiptText, Star, UserRound, X } from 'lucide-angular';
import { switchMap } from 'rxjs';

import { Api, Page } from '../../core/api';
import { Auth, ContactPolicy } from '../../core/auth';
import { askConfirm } from '../../core/dialog';
import { errorText, Submit } from '../../shared/form';
import { Modal } from '../../shared/modal';
import { givenName, localPhone, ORDER_STATUS, RESERVATION_STATUS, TIERS, today, VnDatePipe } from '../../shared/format';
import { discountShort, discountTerms, RedeemReward, VOUCHER_SOURCE } from '../../shared/voucher';
import { VndPipe } from '../../shared/vnd.pipe';

const MAX_AVATAR_BYTES = 5 * 1024 * 1024;
const RECENT_ORDERS = 5;
const OPEN_RESERVATION = ['pending', 'confirmed'];

// Cột trái kiểu Pawsome: hộp điểm thưởng nền đỏ và menu dọc; cột phải là trang con
@Component({
  selector: 'ot-account',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LucideAngularModule],
  template: `
    <div class="ot-page-split">
      <div class="ot-acc">
        <aside class="ot-acc-side">
          @if (auth.profile(); as p) {
            <div class="ot-acc-points">
              <p class="font-medium">Điểm thưởng</p>
              <div class="mt-3 flex items-center gap-4">
                <span class="ot-acc-ring">{{ p.currentPoints }}</span>
                <p class="text-sm leading-snug">
                  Hạng <b>{{ tiers[p.memberTier] }}</b>
                  @if (loyalty()?.nextTier; as next) { <span class="block opacity-90">Còn {{ loyalty()!.pointsToNextTier }} điểm để lên hạng {{ tiers[next] }}</span> }
                </p>
              </div>
              <a class="mt-3 inline-block text-sm font-medium underline underline-offset-4" routerLink="diem-thuong">Đổi điểm lấy voucher</a>
            </div>
          }
          <nav class="ot-acc-menu" aria-label="Tài khoản">
            @for (t of tabs; track t.path) {
              <a #item class="ot-acc-item" [routerLink]="t.path" routerLinkActive="is-active" (isActiveChange)="$event && reveal(item)"><lucide-icon [img]="t.icon" [size]="18" />{{ t.label }}</a>
            }
            <button type="button" class="ot-acc-item ot-acc-out" (click)="logout()"><lucide-icon [img]="icons.logout" [size]="18" />Đăng xuất</button>
          </nav>
        </aside>
        <main class="min-w-0"><router-outlet /></main>
      </div>
    </div>
  `,
})
export class Account {
  private readonly router = inject(Router);
  protected readonly auth = inject(Auth);
  protected readonly tiers = TIERS;
  protected readonly icons = { logout: LogOut };
  protected readonly loyalty = signal<Schemas['LoyaltySummaryDto'] | null>(null);
  protected readonly tabs = [
    { path: 'tong-quan', label: 'Tổng quan', icon: LayoutDashboard },
    { path: 'thong-tin', label: 'Thông tin tài khoản', icon: UserRound },
    { path: 'don-hang', label: 'Đơn hàng', icon: ReceiptText },
    { path: 'dat-ban', label: 'Đặt bàn', icon: CalendarDays },
    { path: 'diem-thuong', label: 'Điểm thưởng & voucher', icon: Gift },
    { path: 'danh-gia', label: 'Đánh giá của tôi', icon: Star },
  ];

  constructor() {
    inject(Api).getOr<Schemas['LoyaltySummaryDto'] | null>('account/loyalty-points', null).subscribe((s) => this.loyalty.set(s));
  }

  // Điện thoại: menu là hàng cuộn ngang, kéo mục đang mở vào giữa để khách thấy mình đang ở đâu
  protected reveal(item: HTMLElement): void {
    const row = item.parentElement;
    if (row && row.scrollWidth > row.clientWidth) {
      row.scrollTo({ left: item.offsetLeft - (row.clientWidth - item.offsetWidth) / 2 });
    }
  }

  protected logout(): void {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/'));
  }
}

@Component({
  selector: 'ot-account-overview',
  imports: [RouterLink, LucideAngularModule, VndPipe, VnDatePipe],
  template: `
    @if (auth.profile(); as p) {
      <h2 class="ot-acc-title">Tổng quan</h2>
      @if (policy.requirePhone()) {
        @if (!p.isPhoneVerified) { <p class="ot-error mb-4">Số điện thoại chưa xác minh nên chưa đặt món, đặt bàn được. <a class="underline" routerLink="/xac-minh-sdt">Xác minh ngay</a></p> }
      } @else if (!p.isEmailVerified) { <p class="ot-error mb-4">Email chưa xác thực nên chưa đặt món, đặt bàn được. <a class="underline" routerLink="/xac-minh-email">Xác thực ngay</a></p> }

      <div class="grid grid-cols-2 gap-3 md:gap-4 xl:grid-cols-4">
        <div class="ot-stat">
          <span class="ot-stat-value">{{ p.currentPoints }}</span>
          <span class="text-sm text-muted">Điểm thưởng</span>
          <a class="ot-stat-link" routerLink="../diem-thuong">Xem <lucide-icon [img]="next" [size]="16" /></a>
        </div>
        <div class="ot-stat">
          <span class="ot-stat-value">{{ tiers[p.memberTier] }}</span>
          <span class="text-sm text-muted">{{ discount() > 0 ? 'Giảm ' + discount() + '% mỗi đơn' : 'Hạng thành viên' }}</span>
          <a class="ot-stat-link" routerLink="../diem-thuong">Quyền lợi <lucide-icon [img]="next" [size]="16" /></a>
        </div>
        <div class="ot-stat">
          <span class="ot-stat-value">{{ orderTotal() }}</span>
          <span class="text-sm text-muted">Đơn hàng</span>
          <a class="ot-stat-link" routerLink="../don-hang">Xem tất cả <lucide-icon [img]="next" [size]="16" /></a>
        </div>
        <div class="ot-stat">
          <span class="ot-stat-value">{{ upcoming() }}</span>
          <span class="text-sm text-muted">Đặt bàn sắp tới</span>
          <a class="ot-stat-link" routerLink="../dat-ban">Xem <lucide-icon [img]="next" [size]="16" /></a>
        </div>
      </div>

      <div class="mt-4 flex items-center gap-3 rounded-lg bg-surface-2 px-5 py-4">
        @if (vouchers() > 0) {
          <p>Bạn đang có <b>{{ vouchers() }} voucher</b> dùng được. Nhập mã ở bước thanh toán để được giảm giá.</p>
        } @else {
          <p>Mỗi đơn hoàn thành đều được cộng điểm thưởng, đủ điểm là đổi được voucher giảm giá.</p>
        }
      </div>

      <section class="ot-panel mt-4">
        <div class="flex items-center justify-between gap-3">
          <h2>Đơn hàng gần đây</h2>
          <a class="flex items-center gap-1 text-sm font-medium text-primary" routerLink="../don-hang">Xem tất cả <lucide-icon [img]="next" [size]="16" /></a>
        </div>
        @if (orders().length === 0) {
          <p class="mt-3 text-muted">Bạn chưa có đơn hàng nào.</p>
          <a class="ot-btn ot-btn-primary mt-3" routerLink="/menu">Đặt món ngay</a>
        } @else {
          <div class="mt-2">
            @for (o of orders(); track o.code) {
              <a class="ot-line-row" [routerLink]="['/tai-khoan/don-hang', o.code]">
                <span class="min-w-0"><b class="block truncate">{{ o.code }}</b><small class="text-muted">{{ o.createdAtUtc | vnDate }}</small></span>
                <span class="ot-badge" [attr.data-s]="o.status">{{ statuses[o.status] }}</span>
                <b class="text-primary sm:text-right">{{ o.totalAmount | vnd }}</b>
              </a>
            }
          </div>
        }
      </section>
    }
  `,
})
export class AccountOverview {
  private readonly api = inject(Api);
  protected readonly auth = inject(Auth);
  protected readonly policy = inject(ContactPolicy);
  protected readonly tiers = TIERS;
  protected readonly statuses = ORDER_STATUS;
  protected readonly next = ChevronRight;
  protected readonly orders = signal<Schemas['PublicOrderDto'][]>([]);
  protected readonly orderTotal = signal(0);
  protected readonly upcoming = signal(0);
  protected readonly vouchers = signal(0);
  protected readonly discount = signal(0);

  constructor() {
    this.api.getPage<Schemas['PublicOrderDto']>('account/orders', { page: 1, pageSize: RECENT_ORDERS }).subscribe({
      next: (r) => {
        this.orders.set(r.data);
        this.orderTotal.set(r.meta.total);
      },
      error: () => undefined,
    });
    const now = today();
    this.api
      .getOr<Schemas['CustomerReservationDto'][]>('account/reservations', [])
      .subscribe((list) => this.upcoming.set(list.filter((r) => OPEN_RESERVATION.includes(r.status) && r.reservedDate >= now).length));
    this.api.getOr<Schemas['AccountVoucherDto'][]>('account/vouchers', []).subscribe((v) => this.vouchers.set(v.filter((x) => x.usable).length));
    this.api.getOr<Schemas['LoyaltySummaryDto'] | null>('account/loyalty-points', null).subscribe((s) => this.discount.set(s?.discountPercent ?? 0));
  }
}

// Kiểu Pawsome: thẻ thông tin ở trên, mỗi mục cài đặt một dòng có nút, bấm mới mở biểu mẫu
@Component({
  selector: 'ot-account-profile',
  imports: [FormsModule, RouterLink, VnDatePipe],
  template: `
    @if (auth.profile(); as p) {
      <h2 class="ot-acc-title">Thông tin tài khoản</h2>
      <div class="grid gap-3">
        @if (policy.requirePhone() && !p.isPhoneVerified) { <p class="ot-notice">Số điện thoại chưa xác minh. <a class="underline" routerLink="/xac-minh-sdt">Xác minh ngay</a></p> }
        @if (!p.isEmailVerified) { <p class="ot-notice">Email chưa xác thực, hãy kiểm tra hộp thư. <button type="button" class="py-2 underline" [disabled]="resent()" (click)="resend(p.email)">{{ resent() ? 'Đã gửi lại' : 'Gửi lại thư' }}</button></p> }
        @if (p.deletionScheduledAtUtc) {
          <p class="ot-error">Tài khoản sẽ bị xóa ngày {{ p.deletionScheduledAtUtc | vnDate }}. <button type="button" class="underline" (click)="cancelDeletion()">Hủy xóa</button></p>
        }

        <section class="ot-panel">
          @if (!editing()) {
            <div class="flex flex-col gap-5 sm:flex-row sm:items-center">
              <div class="flex shrink-0 flex-col items-center gap-2">
                @if (avatar(); as src) {
                  <img class="size-24 rounded-full object-cover" [src]="src" alt="Ảnh đại diện" />
                } @else {
                  <span class="flex size-24 items-center justify-center rounded-full bg-primary-soft font-heading text-4xl font-medium text-primary" aria-hidden="true">{{ initial(p.fullName) }}</span>
                }
                <div class="flex gap-3 text-sm">
                  <label class="cursor-pointer font-medium text-primary">{{ avatar() ? 'Đổi ảnh' : 'Thêm ảnh' }}<input class="sr-only" type="file" accept="image/*" (change)="upload($event)" /></label>
                  @if (avatar()) { <button type="button" class="text-muted" (click)="removeAvatar()">Xóa</button> }
                </div>
              </div>
              <div class="min-w-0 flex-1">
                <p class="text-lg font-medium">{{ p.fullName }}</p>
                <dl class="mt-2 grid gap-1 text-sm">
                  <div class="flex gap-2"><dt class="w-24 shrink-0 text-muted">Email</dt><dd class="min-w-0 break-all">{{ p.email }}</dd></div>
                  <div class="flex gap-2"><dt class="w-24 shrink-0 text-muted">Điện thoại</dt><dd>{{ phone(p.phoneNumber) || 'Chưa có' }} @if (p.isPhoneVerified) { <span class="text-success">· đã xác minh</span> }</dd></div>
                  <div class="flex gap-2"><dt class="w-24 shrink-0 text-muted">Ngày sinh</dt><dd>{{ p.dateOfBirth ? (p.dateOfBirth | vnDate) : 'Chưa có' }}</dd></div>
                </dl>
              </div>
              <div class="flex shrink-0 flex-col items-start gap-2 sm:items-end">
                <span class="text-sm text-muted">Hạng thành viên</span>
                <span class="font-heading text-xl font-medium">{{ tiers[p.memberTier] }}</span>
                <button type="button" class="ot-btn ot-btn-primary mt-1" (click)="startEdit()">Sửa thông tin</button>
              </div>
            </div>
            @if (avatarForm.error(); as e) { <p class="ot-error mt-3">{{ e }}</p> }
            @if (saved()) { <p class="mt-3 text-success">Đã lưu thông tin.</p> }
          } @else {
            <form (ngSubmit)="save()">
              <h2>Sửa thông tin</h2>
              <div class="ot-form-grid mt-4">
                <label class="ot-span-2">Họ tên<input class="ot-input" name="name" required [(ngModel)]="model.fullName" /></label>
                <label>Số điện thoại<input class="ot-input" name="phone" type="tel" [(ngModel)]="model.phoneNumber" /></label>
                <label>Ngày sinh<input class="ot-input" name="dob" type="date" [(ngModel)]="model.dateOfBirth" /></label>
              </div>
              @if (profileForm.error(); as e) { <p class="ot-error mt-3">{{ e }}</p> }
              <div class="mt-4 flex justify-end gap-3">
                <button type="button" class="ot-btn ot-btn-outline" (click)="editing.set(false)">Hủy</button>
                <button class="ot-btn ot-btn-primary" [disabled]="profileForm.busy()">Lưu</button>
              </div>
            </form>
          }
        </section>

        <div class="ot-row-card">
          <p class="ot-row-label">Mật khẩu</p>
          @if (open() !== 'password') { <button type="button" class="ot-btn ot-btn-outline" (click)="toggle('password')">Đổi mật khẩu</button> }
          @if (passwordDone()) { <p class="w-full text-success">Đã đổi mật khẩu.</p> }
          @if (open() === 'password') {
            <form class="w-full" (ngSubmit)="changePassword()">
              <div class="ot-form-grid">
                <label>Mật khẩu hiện tại<input class="ot-input" name="cur" type="password" autocomplete="current-password" required [(ngModel)]="password.currentPassword" /></label>
                <label>Mật khẩu mới<input class="ot-input" name="new" type="password" autocomplete="new-password" required [(ngModel)]="password.newPassword" /></label>
              </div>
              @if (passwordForm.error(); as e) { <p class="ot-error mt-3">{{ e }}</p> }
              <div class="mt-4 flex justify-end gap-3">
                <button type="button" class="ot-btn ot-btn-outline" (click)="toggle(null)">Hủy</button>
                <button class="ot-btn ot-btn-primary" [disabled]="passwordForm.busy()">Lưu mật khẩu</button>
              </div>
            </form>
          }
        </div>

        <div class="ot-row-card">
          <div class="min-w-0">
            <p class="ot-row-label">Email đăng nhập</p>
            <p class="mt-1 break-all">{{ p.email }}</p>
            @if (p.pendingEmail) { <small class="text-muted">Đang chờ xác nhận đổi sang {{ p.pendingEmail }}</small> }
          </div>
          @if (open() !== 'email') { <button type="button" class="ot-btn ot-btn-outline" (click)="toggle('email')">Đổi email</button> }
          @if (emailSent()) { <p class="w-full text-success">Đã gửi liên kết xác nhận tới email mới.</p> }
          @if (open() === 'email') {
            <form class="w-full" (ngSubmit)="changeEmail()">
              <div class="ot-form-grid">
                <label>Email mới<input class="ot-input" name="newEmail" type="email" required [(ngModel)]="email.newEmail" /></label>
                <label>Mật khẩu hiện tại<input class="ot-input" name="emailPass" type="password" autocomplete="current-password" required [(ngModel)]="email.currentPassword" /></label>
              </div>
              @if (emailForm.error(); as e) { <p class="ot-error mt-3">{{ e }}</p> }
              <div class="mt-4 flex justify-end gap-3">
                <button type="button" class="ot-btn ot-btn-outline" (click)="toggle(null)">Hủy</button>
                <button class="ot-btn ot-btn-primary" [disabled]="emailForm.busy()">Gửi xác nhận</button>
              </div>
            </form>
          }
        </div>

        <section class="ot-row-card">
          <p class="ot-row-label w-full">Thiết bị đang đăng nhập</p>
          <div class="w-full">
            @for (s of sessions(); track s.id) {
              <div class="flex items-center justify-between gap-3 border-b border-line py-2 last:border-b-0">
                <span class="min-w-0 text-sm font-medium">{{ device(s.userAgent) }}<small class="block text-muted">Lần cuối {{ s.lastActiveAtUtc | vnDate: true }}</small></span>
                <button type="button" class="min-h-11 shrink-0 px-2 font-medium text-primary" (click)="revoke(s.id)">Đăng xuất</button>
              </div>
            } @empty {
              <p class="text-sm text-muted">Không có thiết bị nào khác.</p>
            }
          </div>
          @if (sessionError(); as e) { <p class="ot-error w-full">{{ e }}</p> }
        </section>

        <div class="ot-row-card">
          <div><p class="ot-row-label">Xóa tài khoản</p><p class="mt-1 text-sm text-muted">Tài khoản bị xóa sau 7 ngày, trong thời gian đó bạn vẫn hủy được.</p></div>
          @if (open() !== 'delete') { <button type="button" class="ot-btn ot-btn-outline text-danger" (click)="toggle('delete')">Xóa tài khoản</button> }
          @if (open() === 'delete') {
            <form class="w-full" (ngSubmit)="remove()">
              <label>Nhập mật khẩu để xác nhận<input class="ot-input" name="delPass" type="password" autocomplete="current-password" required [(ngModel)]="deletePassword" /></label>
              @if (deleteForm.error(); as e) { <p class="ot-error mt-3">{{ e }}</p> }
              <div class="mt-4 flex justify-end gap-3">
                <button type="button" class="ot-btn ot-btn-outline" (click)="toggle(null)">Hủy</button>
                <button class="ot-btn ot-btn-primary" [disabled]="deleteForm.busy()">Yêu cầu xóa</button>
              </div>
            </form>
          }
        </div>
      </div>
    }
  `,
})
export class AccountProfile {
  protected readonly sessionError = signal<string | null>(null);
  private readonly api = inject(Api);
  protected readonly auth = inject(Auth);
  protected readonly policy = inject(ContactPolicy);
  protected readonly tiers = TIERS;
  protected readonly phone = localPhone;
  protected readonly profileForm = new Submit();
  protected readonly passwordForm = new Submit();
  protected readonly emailForm = new Submit();
  protected readonly deleteForm = new Submit();
  protected readonly avatarForm = new Submit();
  protected readonly editing = signal(false);
  protected readonly open = signal<'password' | 'email' | 'delete' | null>(null);
  protected readonly saved = signal(false);
  protected readonly emailSent = signal(false);
  protected readonly passwordDone = signal(false);
  protected readonly resent = signal(false);
  protected readonly sessions = signal<Schemas['AccountSessionDto'][]>([]);
  protected model = { fullName: '', phoneNumber: '', dateOfBirth: '' };
  protected password = { currentPassword: '', newPassword: '' };
  protected email = { newEmail: '', currentPassword: '' };
  protected deletePassword = '';

  constructor() {
    this.loadSessions();
  }

  protected avatar(): string | null {
    return this.auth.profile()?.avatar?.variants.at(-1)?.url ?? null;
  }

  // Chuỗi User-Agent khó đọc: chỉ hiện trình duyệt và hệ điều hành
  protected device(userAgent: string | null | undefined): string {
    const ua = userAgent ?? '';
    const browser = [['Edg/', 'Edge'], ['OPR/', 'Opera'], ['CocCoc', 'Cốc Cốc'], ['Firefox/', 'Firefox'], ['Chrome/', 'Chrome'], ['Safari/', 'Safari']].find(([key]) => ua.includes(key))?.[1];
    const os = [['iPhone', 'iPhone'], ['iPad', 'iPad'], ['Android', 'Android'], ['Windows', 'Windows'], ['Mac OS', 'macOS'], ['Linux', 'Linux']].find(([key]) => ua.includes(key))?.[1];
    return browser && os ? `${browser} · ${os}` : browser ?? os ?? 'Thiết bị khác';
  }

  protected initial(fullName: string): string {
    return givenName(fullName).charAt(0).toUpperCase();
  }

  protected startEdit(): void {
    const p = this.auth.profile();
    this.model = { fullName: p?.fullName ?? '', phoneNumber: localPhone(p?.phoneNumber), dateOfBirth: p?.dateOfBirth ?? '' };
    this.profileForm.error.set(null);
    this.saved.set(false);
    this.editing.set(true);
  }

  // Mở một biểu mẫu thì đóng biểu mẫu khác, và xóa chữ đã gõ dở
  protected toggle(form: 'password' | 'email' | 'delete' | null): void {
    this.password = { currentPassword: '', newPassword: '' };
    this.email = { newEmail: '', currentPassword: '' };
    this.deletePassword = '';
    this.passwordForm.error.set(null);
    this.emailForm.error.set(null);
    this.deleteForm.error.set(null);
    this.passwordDone.set(false);
    this.emailSent.set(false);
    this.open.set(form);
  }

  private loadSessions(): void {
    this.api.getOr<Schemas['AccountSessionDto'][]>('account/sessions', []).subscribe((s) => this.sessions.set(s));
  }

  protected save(): void {
    const body = { ...this.model, phoneNumber: this.model.phoneNumber || null, dateOfBirth: this.model.dateOfBirth || null, preferredLang: this.auth.profile()?.preferredLang ?? 'vi' };
    this.profileForm.run(this.api.put('account/profile', body).pipe(switchMap(() => this.auth.loadProfile())), () => {
      this.editing.set(false);
      this.saved.set(true);
    });
  }

  protected changePassword(): void {
    // Đổi mật khẩu thu hồi token cũ, server trả token mới cho phiên này
    this.passwordForm.run(this.api.put<Schemas['AccessTokenResponse']>('account/change-password', this.password), (r) => {
      this.auth.token.set(r.accessToken);
      this.toggle(null);
      this.passwordDone.set(true);
    }, (e) => {
      if (e instanceof HttpErrorResponse && e.status === 401) {
        this.passwordForm.error.set('Mật khẩu hiện tại không đúng.');
        return true;
      }

      return false;
    });
  }

  protected changeEmail(): void {
    this.emailForm.run(this.api.post('account/change-email', this.email).pipe(switchMap(() => this.auth.loadProfile())), () => {
      this.toggle(null);
      this.emailSent.set(true);
    });
  }

  protected upload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (file && file.size > MAX_AVATAR_BYTES) {
      this.avatarForm.error.set('Ảnh tối đa 5 MB.');
    } else if (file) {
      const data = new FormData();
      data.append('file', file);
      this.avatarForm.run(this.api.post('account/avatar', data).pipe(switchMap(() => this.auth.loadProfile())), () => undefined);
    }
  }

  protected removeAvatar(): void {
    this.avatarForm.run(this.api.delete('account/avatar').pipe(switchMap(() => this.auth.loadProfile())), () => undefined);
  }

  protected resend(email: string): void {
    this.api.post('auth/resend-verification', { email }).subscribe({ next: () => this.resent.set(true), error: () => undefined });
  }

  protected revoke(id: string): void {
    this.sessionError.set(null);
    this.api.delete(`account/sessions/${id}`).subscribe({ next: () => this.loadSessions(), error: (e: unknown) => this.sessionError.set(errorText(e)) });
  }

  protected async remove(): Promise<void> {
    if (await askConfirm('Bạn chắc chắn muốn xóa tài khoản?')) {
      this.deleteForm.run(this.api.post('account/delete', { password: this.deletePassword }).pipe(switchMap(() => this.auth.loadProfile())), () => this.toggle(null));
    }
  }

  protected cancelDeletion(): void {
    this.deleteForm.run(this.api.post('account/delete/cancel').pipe(switchMap(() => this.auth.loadProfile())), () => undefined);
  }
}

@Component({
  selector: 'ot-account-reservations',
  imports: [RouterLink, VnDatePipe],
  template: `
    <h2 class="ot-acc-title">Đặt bàn</h2>
    @if (error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    <section class="ot-panel">
      @for (r of items(); track r.id) {
        <div class="ot-line-row">
          <span class="min-w-0"><b class="block truncate">{{ r.reservationCode }}</b><small class="text-muted">{{ r.reservedTime.slice(0, 5) }}, {{ r.reservedDate | vnDate }}</small></span>
          <span class="ot-badge" [attr.data-s]="r.status">{{ statuses[r.status] }}</span>
          @if (r.status === 'pending' || r.status === 'confirmed') {
            <button type="button" class="justify-self-start font-medium text-danger sm:justify-self-end" (click)="cancel(r.reservationCode)">Hủy đặt bàn</button>
          } @else {
            <span></span>
          }
        </div>
      } @empty {
        <p class="text-muted">Bạn chưa có lượt đặt bàn nào.</p>
        <a class="ot-btn ot-btn-primary mt-3" routerLink="/dat-ban">Đặt bàn</a>
      }
    </section>
  `,
})
export class AccountReservations {
  private readonly api = inject(Api);
  protected readonly statuses = RESERVATION_STATUS;
  protected readonly items = signal<Schemas['CustomerReservationDto'][]>([]);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getOr<Schemas['CustomerReservationDto'][]>('account/reservations', []).subscribe((i) => this.items.set(i));
  }

  protected async cancel(code: string): Promise<void> {
    if (await askConfirm('Bạn chắc chắn muốn hủy đặt bàn này?')) {
      this.api.post(`reservations/${encodeURIComponent(code)}/cancel`, { token: null, reason: null }).subscribe({ next: () => this.load(), error: (e: unknown) => this.error.set(errorText(e)) });
    }
  }
}

@Component({
  selector: 'ot-account-orders',
  imports: [RouterLink, VndPipe, VnDatePipe],
  template: `
    <h2 class="ot-acc-title">Đơn hàng</h2>
    @if (error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    <section class="ot-panel">
      @for (o of items(); track o.code) {
        <a class="ot-line-row" [routerLink]="['/tai-khoan/don-hang', o.code]">
          <span class="min-w-0"><b class="block truncate">{{ o.code }}</b><small class="text-muted">Đặt {{ o.createdAtUtc | vnDate }} · lấy {{ o.pickupTime.slice(0, 5) }}, {{ o.pickupDate | vnDate }}</small></span>
          <span class="ot-badge" [attr.data-s]="o.status">{{ statuses[o.status] }}</span>
          <b class="text-primary sm:text-right">{{ o.totalAmount | vnd }}</b>
        </a>
      } @empty {
        @if (!error()) {
          <p class="text-muted">Bạn chưa có đơn hàng nào.</p>
          <a class="ot-btn ot-btn-primary mt-3" routerLink="/menu">Đặt món ngay</a>
        }
      }
      @if (items().length < total()) { <button type="button" class="ot-btn ot-btn-outline mt-3 w-full" (click)="more()">Xem thêm</button> }
    </section>
  `,
})
export class AccountOrders {
  protected readonly error = signal<string | null>(null);
  private readonly api = inject(Api);
  protected readonly statuses = ORDER_STATUS;
  protected readonly items = signal<Schemas['PublicOrderDto'][]>([]);
  protected readonly total = signal(0);
  private page = 0;

  constructor() {
    this.more();
  }

  protected more(): void {
    this.page += 1;
    this.error.set(null);
    this.api.getPage<Schemas['PublicOrderDto']>('account/orders', { page: this.page, pageSize: 20 }).subscribe({
      next: (r: Page<Schemas['PublicOrderDto']>) => {
        this.items.update((i) => [...i, ...r.data]);
        this.total.set(r.meta.total);
      },
      error: (e: unknown) => {
        this.page -= 1;
        this.error.set(errorText(e));
      },
    });
  }
}

@Component({
  selector: 'ot-account-points',
  imports: [VnDatePipe, LucideAngularModule, Modal],
  template: `
    <h2 class="ot-acc-title">Điểm thưởng & voucher</h2>
    @if (summary(); as s) {
      <div class="grid grid-cols-2 gap-3 md:gap-4 xl:grid-cols-3">
        <div class="ot-stat"><span class="ot-stat-value text-primary">{{ s.currentPoints }}</span><span class="text-sm text-muted">Điểm hiện có</span></div>
        <div class="ot-stat"><span class="ot-stat-value">{{ tiers[s.tier] }}</span><span class="text-sm text-muted">{{ s.discountPercent > 0 ? 'Giảm ' + s.discountPercent + '% mỗi đơn' : 'Hạng thành viên' }}</span></div>
        <div class="ot-stat col-span-2 xl:col-span-1">
          @if (s.nextTier) {
            <span class="ot-stat-value">{{ s.pointsToNextTier }}</span><span class="text-sm text-muted">Điểm nữa để lên hạng {{ tiers[s.nextTier] }}</span>
          } @else {
            <span class="ot-stat-value">Cao nhất</span><span class="text-sm text-muted">Bạn đang ở hạng cao nhất</span>
          }
        </div>
      </div>
      <div class="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-lg bg-surface-2 px-5 py-4">
        <p>{{ rewards().length > 0 ? 'Có ' + rewards().length + ' ưu đãi đổi bằng điểm. Chọn ưu đãi bạn thích, đủ điểm là đổi ngay.' : 'Tích điểm sau mỗi đơn để đổi voucher giảm giá.' }}</p>
        <button type="button" class="ot-btn ot-btn-primary" (click)="openRewards()">Đổi điểm</button>
      </div>
      @if (redeemed(); as code) { <p class="mt-2 text-success">Đã đổi điểm. Mã <b>{{ code }}</b> đã nằm trong "Voucher của tôi", nhập ở bước thanh toán để dùng.</p> }
      @if (s.expiringPoints > 0) { <p class="mt-2 text-sm text-muted">{{ s.expiringPoints }} điểm sẽ hết hạn ngày {{ s.nextExpiryAtUtc | vnDate }}.</p> }
      @if (s.pointsDebt > 0) { <p class="ot-error mt-2">Nợ điểm: {{ s.pointsDebt }}</p> }
    }

    <section class="ot-panel mt-4">
      <h2>Voucher của tôi</h2>
      <div class="mt-3 grid gap-3 md:grid-cols-2">
        @for (v of vouchers(); track v.code) {
          <div class="ot-ticket" [class.is-off]="!v.usable">
            <div class="ot-ticket-stub"><span class="ot-ticket-amount">{{ short(v) }}</span><small>giảm</small></div>
            <div class="min-w-0 flex-1 p-3">
              <p class="text-xs font-medium text-muted">{{ sources[v.source] || 'Voucher' }}</p>
              <p class="font-medium">{{ v.code }}</p>
              <p class="text-sm text-muted">{{ terms(v) }}</p>
              <p class="mt-1 text-xs" [class.text-danger]="!v.usable" [class.text-muted]="v.usable">{{ v.usable ? 'HSD ' + (v.endDate | vnDate) : 'Đã dùng hoặc hết hạn' }}</p>
            </div>
          </div>
        } @empty {
          <p class="text-muted md:col-span-2">Bạn chưa có voucher. Bấm "Đổi điểm" ở trên để đổi voucher bằng điểm thưởng.</p>
        }
      </div>
    </section>

    <section class="ot-panel mt-4">
      <h2>Lịch sử điểm</h2>
      <ul class="mt-2">
        @for (h of history(); track h.id) {
          <li class="flex justify-between gap-3 border-b border-line py-3 last:border-b-0"><span>{{ h.note ?? (entryTypes[h.type] || h.type) }}<small class="block text-muted">{{ h.createdAtUtc | vnDate }}</small></span><b [class.text-success]="h.points > 0">{{ h.points > 0 ? '+' : '' }}{{ h.points }}</b></li>
        } @empty {
          <li class="mt-1 text-muted">Chưa có giao dịch điểm.</li>
        }
      </ul>
    </section>

    @if (sheet()) {
      <div class="ot-sheet-back" role="dialog" aria-modal="true" aria-label="Đổi điểm" otModal (dismiss)="sheet.set(false)">
        <div class="ot-sheet">
          <div class="ot-sheet-head">
            <div><p class="font-heading text-lg font-medium">Đổi điểm lấy ưu đãi</p><p class="text-sm text-muted">Bạn đang có <b class="text-primary">{{ points() }} điểm</b></p></div>
            <button type="button" class="flex size-11 items-center justify-center" aria-label="Đóng" (click)="sheet.set(false)"><lucide-icon [img]="close" [size]="18" /></button>
          </div>
          <div class="ot-sheet-body">
            @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
            @for (r of rewards(); track r.id) {
              @let lack = r.pointsCost - points();
              <div class="ot-ticket" [class.is-off]="lack > 0">
                <div class="ot-ticket-stub"><span class="ot-ticket-amount">{{ short(r) }}</span><small>giảm</small></div>
                <div class="flex min-w-0 flex-1 flex-col gap-1 p-3">
                  <p class="font-medium">{{ r.name }}</p>
                  @if (r.description) { <p class="text-sm text-muted">{{ r.description }}</p> }
                  <p class="text-sm text-muted">{{ terms(r) }} · dùng trong {{ r.validDays }} ngày</p>
                  <div class="mt-1 flex flex-wrap items-center justify-between gap-2">
                    <span class="font-medium text-primary">{{ r.pointsCost }} điểm</span>
                    @if (lack > 0) {
                      <span class="text-sm text-muted">Còn thiếu {{ lack }} điểm</span>
                    } @else {
                      <button type="button" class="ot-btn ot-btn-primary min-h-10 px-5" [disabled]="form.busy()" (click)="redeem(r)">Đổi</button>
                    }
                  </div>
                </div>
              </div>
            } @empty {
              <p class="text-muted">Quán chưa có ưu đãi đổi điểm nào, bạn quay lại sau nhé.</p>
            }
            <p class="text-xs text-muted">Điểm đã đổi không hoàn lại. Mỗi voucher dùng một lần cho đơn đặt món mang về.</p>
          </div>
        </div>
      </div>
    }
  `,
})
export class AccountPoints {
  private readonly api = inject(Api);
  private readonly auth = inject(Auth);
  protected readonly tiers = TIERS;
  protected readonly form = new Submit();
  protected readonly summary = signal<Schemas['LoyaltySummaryDto'] | null>(null);
  protected readonly vouchers = signal<Schemas['AccountVoucherDto'][]>([]);
  protected readonly history = signal<Schemas['LoyaltyEntryDto'][]>([]);
  protected readonly rewards = signal<RedeemReward[]>([]);
  protected readonly sheet = signal(false);
  protected readonly redeemed = signal<string | null>(null);
  protected readonly points = computed(() => this.summary()?.currentPoints ?? 0);
  protected readonly close = X;
  protected readonly short = discountShort;
  protected readonly terms = discountTerms;
  protected readonly sources = VOUCHER_SOURCE;
  protected readonly entryTypes: Record<string, string> = {
    reservation_earn: 'Cộng điểm từ đặt bàn',
    order_earn: 'Cộng điểm từ đơn hàng',
    redeem: 'Đổi điểm lấy voucher',
    expired: 'Điểm hết hạn',
    admin_adjust: 'Quán điều chỉnh điểm',
    revoke: 'Thu hồi điểm',
    debt_repaid: 'Trả nợ điểm',
  };

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getOr<Schemas['LoyaltySummaryDto'] | null>('account/loyalty-points', null).subscribe((s) => this.summary.set(s));
    this.api.getOr<Schemas['AccountVoucherDto'][]>('account/vouchers', []).subscribe((v) => this.vouchers.set(v));
    this.api.getPage<Schemas['LoyaltyEntryDto']>('account/loyalty-points/history', { page: 1, pageSize: 30 }).subscribe({ next: (r) => this.history.set(r.data), error: () => undefined });
    this.api.getOr<RedeemReward[]>('account/rewards', []).subscribe((r) => this.rewards.set(r));
  }

  protected openRewards(): void {
    this.form.error.set(null);
    this.sheet.set(true);
  }

  protected async redeem(reward: RedeemReward): Promise<void> {
    if (await askConfirm(`Dùng ${reward.pointsCost} điểm để đổi "${reward.name}"?`)) {
      this.form.run(this.api.post<Schemas['AccountVoucherDto']>('account/redeem-points', { rewardId: reward.id }), (v) => {
        this.sheet.set(false);
        this.redeemed.set(v.code);
        this.load();
        this.auth.loadProfile().subscribe();
      });
    }
  }
}

@Component({
  selector: 'ot-account-reviews',
  imports: [RouterLink, VnDatePipe, LucideAngularModule],
  template: `
    <h2 class="ot-acc-title">Đánh giá của tôi</h2>
    @if (error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    <section class="ot-panel">
      @for (r of items(); track r.id) {
        <div class="border-b border-line py-4 first:pt-0 last:border-b-0 last:pb-0">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <span class="flex text-accent" [attr.aria-label]="r.rating + ' trên 5 sao'">
              @for (s of stars; track s) { <lucide-icon [img]="star" [size]="16" [attr.fill]="s <= r.rating ? 'currentColor' : 'none'" /> }
            </span>
            <span class="ot-badge" [attr.data-s]="r.status">{{ status[r.status] }}</span>
          </div>
          @if (r.comment) { <p class="mt-2">{{ r.comment }}</p> }
          <small class="text-muted">{{ r.createdAtUtc | vnDate }}</small>
          @if (r.replyContent) { <p class="mt-2 rounded-md bg-surface-2 p-3 text-sm"><span class="font-medium">Quán trả lời:</span> {{ r.replyContent }}</p> }
          @if (r.canEdit) { <button type="button" class="mt-2 block font-medium text-danger" (click)="remove(r.id)">Xóa đánh giá</button> }
        </div>
      } @empty {
        <p class="text-muted">Bạn chưa có đánh giá nào. Bạn có thể đánh giá món trong chi tiết đơn đã hoàn thành.</p>
        <a class="ot-btn ot-btn-outline mt-3" routerLink="../don-hang">Xem đơn hàng</a>
      }
    </section>
  `,
})
export class AccountReviews {
  protected readonly error = signal<string | null>(null);
  private readonly api = inject(Api);
  protected readonly star = Star;
  protected readonly stars = [1, 2, 3, 4, 5];
  protected readonly status: Record<string, string> = { pending: 'Chờ duyệt', approved: 'Đã đăng', rejected: 'Từ chối' };
  protected readonly items = signal<Schemas['MyReviewDto'][]>([]);

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getOr<Schemas['MyReviewDto'][]>('reviews/mine', []).subscribe((i) => this.items.set(i));
  }

  protected async remove(id: number): Promise<void> {
    if (await askConfirm('Xóa đánh giá này?')) {
      this.error.set(null);
      this.api.delete(`reviews/${id}`).subscribe({ next: () => this.load(), error: (e: unknown) => this.error.set(errorText(e)) });
    }
  }
}
