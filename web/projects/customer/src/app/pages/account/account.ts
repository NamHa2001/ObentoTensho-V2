import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Schemas } from '@ot/api';
import { switchMap } from 'rxjs';

import { Api, Page } from '../../core/api';
import { Auth } from '../../core/auth';
import { askConfirm } from '../../core/dialog';
import { errorText, Submit } from '../../shared/form';
import { ORDER_STATUS, RESERVATION_STATUS, TIERS, VnDatePipe, localPhone } from '../../shared/format';
import { VndPipe } from '../../shared/vnd.pipe';

const MAX_AVATAR_BYTES = 5 * 1024 * 1024;

@Component({
  selector: 'ot-account',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="ot-page ot-page-wide">
      <h1>Tài khoản</h1>
      <nav class="ot-scroll-row mt-3" aria-label="Tài khoản">
        @for (t of tabs; track t.path) { <a class="ot-chip" [routerLink]="t.path" routerLinkActive="ot-chip-active">{{ t.label }}</a> }
        <button type="button" class="ot-chip" (click)="logout()">Đăng xuất</button>
      </nav>
      <div class="mt-4"><router-outlet /></div>
    </div>
  `,
})
export class Account {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);
  protected readonly tabs = [
    { path: 'thong-tin', label: 'Thông tin' },
    { path: 'dat-ban', label: 'Đặt bàn' },
    { path: 'don-hang', label: 'Đơn hàng' },
    { path: 'diem-thuong', label: 'Điểm thưởng' },
    { path: 'danh-gia', label: 'Đánh giá' },
  ];

  protected logout(): void {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/'));
  }
}

@Component({
  selector: 'ot-account-profile',
  imports: [FormsModule, RouterLink, VnDatePipe],
  template: `
    @if (auth.profile(); as p) {
      <div class="ot-stack mt-0">
        @if (!p.isPhoneVerified) { <p class="ot-error">Số điện thoại chưa xác minh. <a class="underline" routerLink="/xac-minh-sdt">Xác minh ngay</a></p> }
        @if (!p.isEmailVerified) { <p class="ot-error">Email chưa xác thực, hãy kiểm tra hộp thư. <button type="button" class="underline" [disabled]="resent()" (click)="resend(p.email)">{{ resent() ? 'Đã gửi lại' : 'Gửi lại thư' }}</button></p> }
        @if (p.deletionScheduledAtUtc) {
          <p class="ot-error">Tài khoản sẽ bị xóa ngày {{ p.deletionScheduledAtUtc | vnDate }}. <button type="button" class="underline" (click)="cancelDeletion()">Hủy xóa</button></p>
        }
        <p>Hạng {{ tiers[p.memberTier] }} · {{ p.currentPoints }} điểm</p>
        @if (avatar(); as src) { <img class="size-20 rounded-full object-cover" [src]="src" alt="Ảnh đại diện" /> }
        <div>
          <p class="font-medium">Ảnh đại diện</p>
          <div class="mt-2 flex flex-wrap items-center gap-3">
            <label class="ot-btn ot-btn-outline cursor-pointer">{{ avatar() ? 'Đổi ảnh' : 'Chọn ảnh' }}<input class="sr-only" type="file" accept="image/*" (change)="upload($event)" /></label>
            @if (avatar()) { <button type="button" class="ot-btn ot-btn-outline" (click)="removeAvatar()">Xóa ảnh</button> }
          </div>
          <p class="mt-1 text-sm text-muted">Ảnh JPG hoặc PNG, tối đa 5 MB.</p>
        </div>
        @if (avatarForm.error(); as e) { <p class="ot-error">{{ e }}</p> }

        <form class="ot-stack" (ngSubmit)="save()">
          <h2 class="text-xl">Thông tin</h2>
          <p>Email: {{ p.email }}@if (p.pendingEmail) { <small class="block text-muted">Đang đổi sang {{ p.pendingEmail }}</small> }</p>
          <label>Họ tên<input class="ot-input" name="name" required [(ngModel)]="model.fullName" /></label>
          <label>Số điện thoại<input class="ot-input" name="phone" type="tel" [(ngModel)]="model.phoneNumber" /></label>
          <label>Ngày sinh<input class="ot-input" name="dob" type="date" [(ngModel)]="model.dateOfBirth" /></label>
          @if (profileForm.error(); as e) { <p class="ot-error">{{ e }}</p> }
          @if (saved()) { <p>Đã lưu.</p> }
          <button class="ot-btn ot-btn-primary" [disabled]="profileForm.busy()">Lưu</button>
        </form>

        <form class="ot-stack" (ngSubmit)="changePassword()">
          <h2 class="text-xl">Đổi mật khẩu</h2>
          <label>Mật khẩu hiện tại<input class="ot-input" name="cur" type="password" autocomplete="current-password" required [(ngModel)]="password.currentPassword" /></label>
          <label>Mật khẩu mới<input class="ot-input" name="new" type="password" autocomplete="new-password" required [(ngModel)]="password.newPassword" /></label>
          @if (passwordForm.error(); as e) { <p class="ot-error">{{ e }}</p> }
          @if (passwordDone()) { <p>Đã đổi mật khẩu.</p> }
          <button class="ot-btn ot-btn-outline" [disabled]="passwordForm.busy()">Đổi mật khẩu</button>
        </form>

        <form class="ot-stack" (ngSubmit)="changeEmail()">
          <h2 class="text-xl">Đổi email</h2>
          <label>Email mới<input class="ot-input" name="newEmail" type="email" required [(ngModel)]="email.newEmail" /></label>
          <label>Mật khẩu hiện tại<input class="ot-input" name="emailPass" type="password" required [(ngModel)]="email.currentPassword" /></label>
          @if (emailForm.error(); as e) { <p class="ot-error">{{ e }}</p> }
          @if (emailSent()) { <p>Đã gửi liên kết xác nhận tới email mới.</p> }
          <button class="ot-btn ot-btn-outline" [disabled]="emailForm.busy()">Đổi email</button>
        </form>

        <section class="ot-stack">
          <h2 class="text-xl">Thiết bị đăng nhập</h2>
          @for (s of sessions(); track s.id) {
            <div class="ot-card flex items-center justify-between gap-3 p-3">
              <span class="min-w-0 text-sm">{{ s.userAgent }}<small class="block text-muted">{{ s.lastActiveAtUtc | vnDate: true }}</small></span>
              <button type="button" class="min-h-11 shrink-0 px-2 text-danger" (click)="revoke(s.id)">Đăng xuất</button>
            </div>
          }
          @if (sessionError(); as e) { <p class="ot-error">{{ e }}</p> }
        </section>

        <form class="ot-stack" (ngSubmit)="remove()">
          <h2 class="text-xl">Xóa tài khoản</h2>
          <p class="text-sm text-muted">Tài khoản bị xóa sau 7 ngày; trong thời gian đó bạn có thể hủy.</p>
          <label>Mật khẩu<input class="ot-input" name="delPass" type="password" required [(ngModel)]="deletePassword" /></label>
          @if (deleteForm.error(); as e) { <p class="ot-error">{{ e }}</p> }
          <button class="ot-btn ot-btn-outline" [disabled]="deleteForm.busy()">Yêu cầu xóa</button>
        </form>
      </div>
    }
  `,
})
export class AccountProfile {
  protected readonly sessionError = signal<string | null>(null);
  private readonly api = inject(Api);
  protected readonly auth = inject(Auth);
  protected readonly tiers = TIERS;
  protected readonly profileForm = new Submit();
  protected readonly passwordForm = new Submit();
  protected readonly emailForm = new Submit();
  protected readonly deleteForm = new Submit();
  protected readonly avatarForm = new Submit();
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
    const p = this.auth.profile();
    this.model = { fullName: p?.fullName ?? '', phoneNumber: localPhone(p?.phoneNumber), dateOfBirth: p?.dateOfBirth ?? '' };
    this.loadSessions();
  }

  protected avatar(): string | null {
    return this.auth.profile()?.avatar?.variants.at(-1)?.url ?? null;
  }

  private loadSessions(): void {
    this.api.getOr<Schemas['AccountSessionDto'][]>('account/sessions', []).subscribe((s) => this.sessions.set(s));
  }

  protected save(): void {
    this.saved.set(false);
    const body = { ...this.model, phoneNumber: this.model.phoneNumber || null, dateOfBirth: this.model.dateOfBirth || null, preferredLang: this.auth.profile()?.preferredLang ?? 'vi' };
    this.profileForm.run(this.api.put('account/profile', body).pipe(switchMap(() => this.auth.loadProfile())), () => this.saved.set(true));
  }

  protected changePassword(): void {
    // Đổi mật khẩu thu hồi token cũ, server trả token mới cho phiên này
    this.passwordDone.set(false);
    this.passwordForm.run(this.api.put<Schemas['AccessTokenResponse']>('account/change-password', this.password), (r) => {
      this.auth.token.set(r.accessToken);
      this.password = { currentPassword: '', newPassword: '' };
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
    this.emailForm.run(this.api.post('account/change-email', this.email).pipe(switchMap(() => this.auth.loadProfile())), () => this.emailSent.set(true));
  }

  protected upload(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
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
      this.deleteForm.run(this.api.post('account/delete', { password: this.deletePassword }).pipe(switchMap(() => this.auth.loadProfile())), () => undefined);
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
    @if (error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (items().length === 0) { <p class="text-muted">Chưa có đặt bàn.</p> <a class="ot-btn ot-btn-primary mt-3" routerLink="/dat-ban">Đặt bàn</a> }
    <div class="grid gap-3">
      @for (r of items(); track r.id) {
        <div class="ot-card p-3"><b>{{ r.reservationCode }}</b> · {{ statuses[r.status] }}<p class="text-sm text-muted">{{ r.reservedTime.slice(0, 5) }}, {{ r.reservedDate | vnDate }}</p>
          @if (r.status === 'pending' || r.status === 'confirmed') { <button type="button" class="mt-1 text-danger" (click)="cancel(r.reservationCode)">Hủy đặt bàn</button> }
        </div>
      }
    </div>
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
    @if (error(); as e) { <p class="ot-error mb-3">{{ e }}</p> } @else if (items().length === 0) { <p class="text-muted">Chưa có đơn hàng.</p> }
    <div class="grid gap-3">
      @for (o of items(); track o.code) {
        <a class="ot-card block p-3" [routerLink]="['/tai-khoan/don-hang', o.code]"><b>{{ o.code }}</b> · {{ statuses[o.status] }}<p class="text-sm text-muted">{{ o.createdAtUtc | vnDate }} · {{ o.totalAmount | vnd }}</p></a>
      }
    </div>
    @if (items().length < total()) { <button type="button" class="ot-btn ot-btn-outline mt-3" (click)="more()">Xem thêm</button> }
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
  imports: [VndPipe, VnDatePipe],
  template: `
    @if (summary(); as s) {
      <div class="ot-card p-4">
        <p class="text-2xl font-bold text-primary">{{ s.currentPoints }} điểm</p>
        <p>Hạng {{ tiers[s.tier] }} · giảm {{ s.discountPercent }}%</p>
        @if (s.nextTier) { <p class="text-sm text-muted">Còn {{ s.pointsToNextTier }} điểm để lên hạng {{ tiers[s.nextTier] }}</p> }
        @if (s.expiringPoints > 0) { <p class="text-sm text-muted">{{ s.expiringPoints }} điểm hết hạn ngày {{ s.nextExpiryAtUtc | vnDate }}</p> }
        @if (s.pointsDebt > 0) { <p class="ot-error mt-1">Nợ điểm: {{ s.pointsDebt }}</p> }
        <button type="button" class="ot-btn ot-btn-primary mt-3" [disabled]="form.busy()" (click)="redeem()">Đổi điểm lấy voucher</button>
        @if (form.error(); as e) { <p class="ot-error mt-2">{{ e }}</p> }
      </div>
    }
    <h2 class="mt-5 text-xl">Voucher của tôi</h2>
    @if (vouchers().length === 0) { <p class="mt-1 text-muted">Chưa có voucher.</p> }
    <div class="mt-2 grid gap-3">
      @for (v of vouchers(); track v.code) {
        <div class="ot-card p-3" [class.opacity-50]="!v.usable"><b>{{ v.code }}</b> · giảm @if (v.discountType === 'percent') { {{ v.discountValue }}% } @else { {{ v.discountValue | vnd }} }<p class="text-sm text-muted">Đơn từ {{ v.minOrderValue | vnd }} · hạn {{ v.endDate | vnDate }}</p></div>
      }
    </div>
    <h2 class="mt-5 text-xl">Lịch sử điểm</h2>
    @if (history().length === 0) { <p class="mt-1 text-muted">Chưa có giao dịch.</p> }
    <ul class="mt-2">
      @for (h of history(); track h.id) {
        <li class="flex justify-between gap-3 border-b border-line py-2"><span>{{ h.note ?? h.type }}<small class="block text-muted">{{ h.createdAtUtc | vnDate }}</small></span><b>{{ h.points > 0 ? '+' : '' }}{{ h.points }}</b></li>
      }
    </ul>
  `,
})
export class AccountPoints {
  private readonly api = inject(Api);
  protected readonly tiers = TIERS;
  protected readonly form = new Submit();
  protected readonly summary = signal<Schemas['LoyaltySummaryDto'] | null>(null);
  protected readonly vouchers = signal<Schemas['AccountVoucherDto'][]>([]);
  protected readonly history = signal<Schemas['LoyaltyEntryDto'][]>([]);

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getOr<Schemas['LoyaltySummaryDto'] | null>('account/loyalty-points', null).subscribe((s) => this.summary.set(s));
    this.api.getOr<Schemas['AccountVoucherDto'][]>('account/vouchers', []).subscribe((v) => this.vouchers.set(v));
    this.api.getPage<Schemas['LoyaltyEntryDto']>('account/loyalty-points/history', { page: 1, pageSize: 30 }).subscribe({ next: (r) => this.history.set(r.data), error: () => undefined });
  }

  protected async redeem(): Promise<void> {
    if (await askConfirm('Đổi điểm lấy voucher?')) {
      this.form.run(this.api.post('account/redeem-points'), () => this.load());
    }
  }
}

@Component({
  selector: 'ot-account-reviews',
  imports: [VnDatePipe],
  template: `
    @if (error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (items().length === 0) { <p class="text-muted">Chưa có đánh giá. Bạn có thể đánh giá trong chi tiết đơn đã hoàn thành.</p> }
    <div class="grid gap-3">
      @for (r of items(); track r.id) {
        <div class="ot-card p-3"><b>{{ r.rating }} sao</b> · {{ status[r.status] }}<p>{{ r.comment }}</p><small class="text-muted">{{ r.createdAtUtc | vnDate }}</small>
          @if (r.replyContent) { <p class="mt-1 text-sm">Quán trả lời: {{ r.replyContent }}</p> }
          @if (r.canEdit) { <button type="button" class="mt-1 block text-danger" (click)="remove(r.id)">Xóa</button> }
        </div>
      }
    </div>
  `,
})
export class AccountReviews {
  protected readonly error = signal<string | null>(null);
  private readonly api = inject(Api);
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
