import { DOCUMENT } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Schemas } from '@ot/api';
import { BadgeDollarSign, ClipboardList, TrendingUp, Wallet } from 'lucide-angular';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { Subscription } from 'rxjs';

import { Api, saveBlob } from '../core/api';
import { Auth } from '../core/auth';
import { StatCard } from '../shared/chart';
import { ORDER_STATUS, RESERVATION_STATUS, Submit, TIERS, VnDatePipe, errorText } from '../shared/ui';
import { askText } from '../../../../customer/src/app/core/dialog';

type CustomerRow = Schemas['CustomerListItemDto'];
type CustomerDetail = Schemas['CustomerDetailDto'];

@Component({
  selector: 'ot-customers',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="a-head"><h1>Khách hàng</h1>@if (auth.has('customer_export')) { <button type="button" class="ot-btn ot-btn-outline" (click)="exportAll()">Xuất danh sách</button> }</div>
    @if (notice(); as n) { <p class="mb-3 font-semibold">{{ n }}</p> }
    <div class="a-filters"><input class="ot-input" name="q" placeholder="Tìm tên, email, SĐT" [(ngModel)]="q" (change)="reload()" /></div>
    @if (error(); as e) { <p class="ot-error mt-2">{{ e }}</p> }
    <div class="a-table"><table class="w-full text-sm"><tr class="bg-surface-2 text-left"><th class="p-2">Họ tên</th><th class="p-2">Email</th><th class="p-2">SĐT</th><th class="p-2">Hạng</th><th class="p-2">Điểm</th><th class="p-2">Hoạt động</th></tr>
      @for (c of rows(); track c.id) { <tr class="border-b border-line"><td class="p-2"><a class="text-primary" [routerLink]="['/customers', c.id]">{{ c.fullName }}</a></td><td class="p-2">{{ c.emailMasked }}</td><td class="p-2">{{ c.phoneMasked }}</td><td class="p-2">{{ tiers[c.memberTier] }}</td><td class="p-2">{{ c.currentPoints }}</td><td class="p-2">{{ c.isActive ? 'Có' : 'Khóa' }}</td></tr> }</table></div>
    @if (rows().length < total()) { <button type="button" class="ot-btn ot-btn-outline mt-3" (click)="more()">Xem thêm</button> }
  `,
})
export class Customers {
  private readonly api = inject(Api);
  protected readonly auth = inject(Auth);
  protected readonly tiers = TIERS;
  protected readonly notice = signal<string | null>(null);
  protected readonly rows = signal<CustomerRow[]>([]);
  protected readonly total = signal(0);
  protected readonly error = signal<string | null>(null);
  protected q = '';
  private page = 1;

  constructor() {
    this.reload();
  }

  protected async exportAll(): Promise<void> {
    const reason = await askText('Lý do xuất danh sách khách hàng');
    if (reason) {
      this.api.post('customers/export', { reason }).subscribe({ next: () => this.notice.set('Đang tạo tệp. Liên kết tải sẽ gửi tới email của bạn.'), error: (e: unknown) => this.error.set(errorText(e)) });
    }
  }

  protected reload(): void {
    this.page = 1;
    this.rows.set([]);
    this.fetch();
  }

  private loading: Subscription | null = null;

  protected more(): void {
    this.page += 1;
    this.fetch();
  }

  // Tìm mới hủy lần tải trước để kết quả cũ về muộn không chen vào danh sách
  private fetch(): void {
    this.loading?.unsubscribe();
    this.loading = this.api.raw<CustomerRow[]>('customers', { q: this.q, page: this.page, pageSize: 30 }).subscribe({
      next: (r) => {
        this.rows.update((x) => [...x, ...r.data]);
        this.total.set(r.meta?.total ?? 0);
      },
      error: (e: unknown) => this.error.set(errorText(e)),
    });
  }
}

@Component({
  selector: 'ot-customer',
  imports: [VnDatePipe, StatCard],
  template: `
    @if (form.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (d(); as c) {
      <div class="a-head"><h1>{{ c.fullName }}</h1><span class="a-badge" [class.a-badge-good]="c.isActive" [class.a-badge-bad]="!c.isActive">{{ c.isActive ? 'Hoạt động' : 'Đã khóa' }}</span></div>
      <div class="a-row4">
        <ot-stat label="Hạng" [value]="tiers[c.memberTier]" [icon]="icons.trend" [tone]="3" />
        <ot-stat label="Điểm hiện có" [value]="c.currentPoints" [icon]="icons.wallet" [tone]="1" />
        <ot-stat label="Tích lũy" [value]="c.accumulatedPoints" [icon]="icons.dollar" [tone]="2" />
        <ot-stat label="Nợ điểm" [value]="c.pointsDebt" [icon]="icons.order" [tone]="5" />
      </div>
      <div class="a-row66">
        <section class="a-card">
          <header class="a-card-h">Thông tin</header>
          <div class="a-card-b"><dl class="a-kv"><dt>Email</dt><dd>{{ c.email }}</dd><dt>Điện thoại</dt><dd>{{ c.phoneNumber }}</dd><dt>Ngày tạo</dt><dd>{{ c.createdAtUtc | vnDate }}</dd></dl>
            <div class="a-btns mt-4">
              <button type="button" class="ot-chip" (click)="toggle(c)">{{ c.isActive ? 'Khóa' : 'Mở khóa' }}</button>
              <button type="button" class="ot-chip" (click)="adjust(c)">Điều chỉnh điểm</button>
              <button type="button" class="ot-chip" (click)="remove(c)">Yêu cầu xóa tài khoản</button>
            </div>
          </div>
        </section>
        <section class="a-card">
          <header class="a-card-h">Voucher</header>
          <div class="a-card-b">@for (v of c.vouchers; track v.id) { <div class="a-line"><span>{{ v.code }}</span><span class="text-muted">{{ v.source }}</span></div> } @empty { <p class="text-muted">Chưa có.</p> }</div>
        </section>
      </div>
      <div class="a-row66">
        <section class="a-card">
          <header class="a-card-h">Đơn gần đây</header>
          <div class="a-card-b">@for (o of c.recentOrders; track o.id) { <div class="a-line"><span>{{ o.orderCode }}</span><span class="text-muted">{{ orderLabels[o.status] }} · {{ o.createdAtUtc | vnDate }}</span></div> } @empty { <p class="text-muted">Chưa có.</p> }</div>
        </section>
        <section class="a-card">
          <header class="a-card-h">Đặt bàn gần đây</header>
          <div class="a-card-b">@for (r of c.recentReservations; track r.id) { <div class="a-line"><span>{{ r.reservationCode }}</span><span class="text-muted">{{ reservationLabels[r.status] }} · {{ r.reservedDate | vnDate }}</span></div> } @empty { <p class="text-muted">Chưa có.</p> }</div>
        </section>
      </div>
    }
  `,
})
export class Customer {
  private readonly api = inject(Api);
  protected readonly icons = { trend: TrendingUp, wallet: Wallet, dollar: BadgeDollarSign, order: ClipboardList };
  protected readonly tiers = TIERS;
  protected readonly orderLabels = ORDER_STATUS;
  protected readonly reservationLabels = RESERVATION_STATUS;
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  protected readonly d = signal<CustomerDetail | null>(null);
  protected readonly form = new Submit();

  constructor() {
    this.load();
  }

  private load(): void {
    this.form.run(this.api.get<CustomerDetail>(`customers/${this.id}`), (c) => this.d.set(c));
  }

  protected async toggle(c: CustomerDetail): Promise<void> {
    const reason = await askText(c.isActive ? 'Lý do khóa' : 'Lý do mở khóa');
    if (reason) this.form.run(this.api.patch(`customers/${c.id}/status`, { isActive: !c.isActive, reason }), () => this.load());
  }

  protected async adjust(c: CustomerDetail): Promise<void> {
    const points = Number(await askText('Số điểm (âm để trừ)'));
    const reason = points ? await askText('Lý do') : null;
    if (points && reason) this.form.run(this.api.patch(`customers/${c.id}/loyalty-points`, { points, reason }), () => this.load());
  }

  protected async remove(c: CustomerDetail): Promise<void> {
    const reason = await askText('Lý do xóa tài khoản');
    if (reason) this.form.run(this.api.post(`customers/${c.id}/deletion`, { reason }), () => this.load());
  }
}

// Liên kết trong email "tệp xuất đã sẵn sàng": token nằm sau dấu #
@Component({
  selector: 'ot-export-download',
  template: `
    <h1>Tải tệp xuất</h1>
    @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
    @if (done()) { <p>Đã tải tệp về máy.</p> }
    @if (form.busy()) { <p>Đang tải…</p> }
  `,
})
export class ExportDownload {
  protected readonly form = new Submit();
  protected readonly done = signal(false);

  constructor() {
    const doc = inject(DOCUMENT);
    const match = /[#&]token=([^&]+)/.exec(doc.location.hash);
    history.replaceState(null, '', doc.location.pathname);
    this.form.run(inject(Api).postBlob('exports/download', { token: match ? decodeURIComponent(match[1]) : '' }), (r) => {
      saveBlob(r, 'tep-xuat.xlsx');
      this.done.set(true);
    });
  }
}
