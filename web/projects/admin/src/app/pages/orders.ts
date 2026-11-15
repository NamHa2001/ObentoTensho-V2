import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { reloadOnLive } from '../core/live';
import { Latest, ORDER_STATUS, Submit, VnDatePipe, VndPipe } from '../shared/ui';
import { askConfirm, askText } from '../../../../customer/src/app/core/dialog';
import { buyerBody, emptyBuyer, InvoiceForm } from '../../../../customer/src/app/shared/invoice-form';

const ORDER_NEXT: Record<string, string[]> = { pending: ['preparing', 'cancelled'], preparing: ['ready', 'cancelled'], ready: ['completed', 'cancelled'], delivering: ['completed', 'cancelled'] };
const CHANNEL: Record<string, string> = { pickup: 'Đặt online', delivery: 'Giao hàng', dine_in: 'Tại bàn', counter: 'Mua tại quầy' };

// Đơn giao hàng: sẵn sàng thì chuyển "Đang giao" (không hoàn thành thẳng)
function nextSteps(order: any): string[] {
  const steps = ORDER_NEXT[order.status] ?? [];
  return order.channel === 'delivery' && order.status === 'ready' ? ['delivering', 'cancelled'] : steps;
}

@Component({
  selector: 'ot-orders',
  imports: [FormsModule, RouterLink, VndPipe, VnDatePipe],
  template: `
    <h1>Đơn hàng</h1>
    @if (attention(); as a) {
      @if (a.pendingOverdue.length + a.notPickedUp.length + a.refundPending.length > 0) {
        <p class="ot-error mt-2">Cần xử lý: {{ a.pendingOverdue.length }} chờ quá hạn · {{ a.notPickedUp.length }} chưa lấy · {{ a.refundPending.length }} chưa ghi hoàn tiền</p>
      }
    }
    <div class="a-filters">
      <input class="ot-input" name="q" placeholder="Tìm mã, tên" [(ngModel)]="q" (change)="reload()" />
      <select class="ot-input" name="s" [(ngModel)]="status" (change)="reload()"><option value="">Mọi trạng thái</option>@for (s of statusKeys; track s) { <option [value]="s">{{ labels[s] }}</option> }</select>
      <input class="ot-input" name="from" type="date" [(ngModel)]="from" (change)="reload()" />
      <input class="ot-input" name="to" type="date" [(ngModel)]="to" (change)="reload()" />
    </div>
    @if (list.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    <div class="a-table"><table class="w-full text-sm"><thead><tr class="bg-surface-2 text-left"><th class="p-2">Mã</th><th class="p-2">Khách</th><th class="p-2">Lấy lúc</th><th class="p-2">Tổng</th><th class="p-2">Trạng thái</th><th class="p-2">Thanh toán</th></tr></thead>
      <tbody>@for (o of rows(); track o.id) {
        <tr class="border-b border-line"><td class="p-2"><a class="text-primary" [routerLink]="['/orders', o.id]">{{ o.code }}</a><small class="block text-muted">{{ channel[o.channel] }}</small></td><td class="p-2">{{ o.customerName }}<small class="block text-muted">{{ o.maskedPhone }}</small></td><td class="p-2 whitespace-nowrap">{{ o.pickupTime.slice(0, 5) }} {{ o.pickupDate | vnDate }}</td><td class="p-2">{{ o.totalAmount | vnd }}</td><td class="p-2">{{ labels[o.status] }}</td><td class="p-2">{{ o.paymentStatus === 'paid' ? 'Đã trả' : 'Chưa trả' }}</td></tr>
      } @empty { <tr><td colspan="6" class="p-4 text-muted">Không có đơn.</td></tr> }</tbody></table></div>
    @if (rows().length < total()) { <button type="button" class="ot-btn ot-btn-outline mt-3" (click)="more()">Xem thêm</button> }
  `,
})
export class Orders {
  private readonly api = inject(Api);
  protected readonly channel = CHANNEL;
  protected readonly labels = ORDER_STATUS;
  protected readonly statusKeys = Object.keys(ORDER_STATUS);
  protected readonly rows = signal<any[]>([]);
  protected readonly total = signal(0);
  protected readonly attention = signal<any>(null);
  protected readonly list = new Latest();
  protected q = '';
  protected status = '';
  protected from = '';
  protected to = '';
  private page = 1;

  constructor() {
    this.reload();
    reloadOnLive(() => this.reload());
    this.api.get<any>('orders/attention').subscribe({ next: (a) => this.attention.set(a), error: () => undefined });
  }

  protected reload(): void {
    this.page = 1;
    this.rows.set([]);
    this.fetch();
  }

  protected more(): void {
    this.page += 1;
    this.fetch();
  }

  private fetch(): void {
    this.list.run(this.api.raw<any[]>('orders', { q: this.q, status: this.status, from: this.from, to: this.to, page: this.page, pageSize: 30 }), (r) => {
      this.rows.update((x) => [...x, ...r.data]);
      this.total.set(r.meta?.total ?? 0);
    });
  }
}

@Component({
  selector: 'ot-order',
  imports: [FormsModule, VndPipe, VnDatePipe, InvoiceForm],
  template: `
    @if (form.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (d(); as v) {
      <div class="a-head"><h1>Đơn {{ v.order.code }}</h1><span class="a-badge">{{ labels[v.order.status] }}</span></div>
      <div class="a-row84">
        <section class="a-card">
          <header class="a-card-h">Món đã đặt</header>
          <div class="a-card-b">
            @for (i of v.items; track $index) { <div class="a-line"><span>{{ i.quantity }} × {{ i.name }}@for (o of i.options; track $index) { <small class="block text-muted">{{ o.groupName }}: {{ o.optionName }}</small> }@if (i.note) { <small class="block">{{ i.note }}</small> }</span><span class="whitespace-nowrap tabular-nums">{{ i.lineTotal | vnd }}</span></div> }
            <div class="a-line"><span>Tạm tính</span><span class="tabular-nums">{{ v.subTotal | vnd }}</span></div>
            @if (v.memberDiscount > 0) { <div class="a-line"><span>Giảm hạng</span><span class="tabular-nums">-{{ v.memberDiscount | vnd }}</span></div> }
            @if (v.voucherDiscount > 0) { <div class="a-line"><span>Voucher</span><span class="tabular-nums">-{{ v.voucherDiscount | vnd }}</span></div> }
            @if (v.order.channel === 'delivery') { <div class="a-line"><span>Phí ship</span><span class="tabular-nums">{{ v.deliveryFee | vnd }}</span></div> }
            <div class="a-line font-bold"><span>Tổng cộng</span><span class="text-primary tabular-nums">{{ v.order.totalAmount | vnd }}</span></div>
            @if (v.note) { <p class="mt-2 text-muted">Ghi chú: {{ v.note }}</p> }
          </div>
        </section>
        <div class="a-col">
          <section class="a-card">
            <header class="a-card-h">Thông tin</header>
            <div class="a-card-b"><dl class="a-kv">
              <dt>Khách</dt><dd>{{ v.order.customerName }}</dd>
              <dt>Điện thoại</dt><dd>{{ contact()?.phone ?? v.order.maskedPhone }}</dd>
              <dt>Email</dt><dd>{{ contact()?.email ?? v.order.maskedEmail }}</dd>
              <dt>Kênh</dt><dd>{{ channel[v.order.channel] }}</dd>
              <dt>{{ v.order.channel === 'delivery' ? 'Giờ giao' : 'Giờ lấy' }}</dt><dd>{{ v.order.pickupTime.slice(0, 5) }} {{ v.order.pickupDate | vnDate }}</dd>
              @if (v.deliveryAddress) {
                <dt>Giao tới</dt><dd>{{ v.deliveryAddress }} <small class="block text-muted">{{ v.deliveryDistanceKm }} km</small><a class="text-primary" target="_blank" rel="noopener" [href]="mapLink(v)">Mở Google Maps</a>@if (v.deliveryLatitude) { <small class="block text-muted">Khách đã ghim vị trí trên bản đồ</small> }</dd>
              }
              <dt>Thanh toán</dt><dd>{{ v.order.paymentMethod === 'cash' ? 'Tiền mặt' : 'Chuyển khoản' }} · {{ v.order.paymentStatus === 'paid' ? 'Đã trả' : 'Chưa trả' }}</dd>
            </dl></div>
          </section>
          <section class="a-card">
            <header class="a-card-h">Xử lý</header>
            <div class="a-card-b a-btns">
              @if (!contact()) { <button type="button" class="ot-chip" (click)="reveal(v)">Hiện SĐT</button> }
              @for (s of next(v.order); track s) { <button type="button" class="ot-chip" (click)="move(v, s)">{{ labels[s] }}</button> }
              @if (v.order.status === 'ready' && auth.has('orders_cancel')) { <button type="button" class="ot-chip" (click)="noPickup(v)">Khách không đến lấy</button> }
              @if (v.order.needsContactConfirm && !v.order.contactConfirmed) { <button type="button" class="ot-chip" (click)="confirmContact(v)">Đã gọi xác nhận</button> }
              @if (v.order.paymentStatus === 'unpaid' && v.order.status !== 'cancelled') { <button type="button" class="ot-chip" (click)="pay(v)">Đã thu tiền</button> }
              @if (v.order.refundPending) { <button type="button" class="ot-chip" (click)="refund(v)">Ghi hoàn tiền</button> }
              @if (v.order.status !== 'cancelled') { <button type="button" class="ot-chip" (click)="startInvoice()">Yêu cầu xuất hóa đơn</button> }
            </div>
            @if (invoiceSent()) { <p class="px-4 pb-3 text-sm font-semibold">Đã gửi yêu cầu xuất hóa đơn cho kế toán.</p> }
          </section>
          <section class="a-card">
            <header class="a-card-h">Lịch sử</header>
            <div class="a-card-b">@for (h of v.history; track $index) { <div class="a-line text-sm"><span>{{ labels[h.toStatus] }} @if (h.reason) { ({{ h.reason }}) }</span><span class="whitespace-nowrap text-muted">{{ h.changedAtUtc | vnDate: true }}</span></div> } @empty { <p class="text-muted">Chưa có.</p> }</div>
          </section>
        </div>
      </div>
    }
    @if (invoicing()) {
      <div class="a-drawer" role="dialog" aria-modal="true">
        <form class="a-drawer-panel" (ngSubmit)="sendInvoice()">
          <div class="a-drawer-h">Yêu cầu xuất hóa đơn</div>
          <div class="a-drawer-b">
            <ot-invoice-form [model]="buyer" />
            @if (invoiceForm.error(); as e) { <p class="ot-error">{{ e }}</p> }
          </div>
          <div class="a-drawer-f">
            <button type="button" class="ot-btn ot-btn-outline" (click)="invoicing.set(false)">Đóng</button>
            <button class="ot-btn ot-btn-primary" [disabled]="invoiceForm.busy()">Gửi kế toán</button>
          </div>
        </form>
      </div>
    }
  `,
})
export class Order {
  private readonly api = inject(Api);
  protected readonly auth = inject(Auth);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  protected readonly labels = ORDER_STATUS;
  protected readonly channel = CHANNEL;
  protected readonly d = signal<any>(null);
  protected readonly contact = signal<any>(null);
  protected readonly form = new Submit();
  protected readonly invoiceForm = new Submit();
  protected readonly invoicing = signal(false);
  protected readonly invoiceSent = signal(false);
  protected buyer = emptyBuyer();

  constructor() {
    this.load();
  }

  private load(): void {
    this.form.run(this.api.get<any>(`orders/${this.id}`), (v) => this.d.set(v));
  }

  protected startInvoice(): void {
    this.buyer = emptyBuyer();
    this.invoiceForm.error.set(null);
    this.invoicing.set(true);
  }

  protected sendInvoice(): void {
    this.invoiceForm.run(this.api.post(`orders/${this.id}/invoice-request`, buyerBody(this.buyer)), () => {
      this.invoicing.set(false);
      this.invoiceSent.set(true);
    });
  }

  // Chỉ hiện bước mà vai trò này được làm và đã đủ điều kiện (đơn cần gọi xác nhận thì gọi trước)
  protected next(order: any): string[] {
    const blocked = order.status === 'pending' && order.needsContactConfirm && !order.contactConfirmed;
    return nextSteps(order).filter((s) => (s === 'cancelled' ? this.auth.has('orders_cancel') : s === 'completed' || s === 'delivering' ? this.auth.has('orders_cashier') : this.auth.has('orders_kitchen') && !blocked));
  }

  // Khách ghim vị trí thì chỉ đường tới đúng điểm ghim; không thì tìm theo địa chỉ chữ
  protected mapLink(v: any): string {
    const to = v.deliveryLatitude != null ? `${v.deliveryLatitude},${v.deliveryLongitude}` : v.deliveryAddress;
    return `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(to)}`;
  }

  protected async noPickup(v: any): Promise<void> {
    if (await askConfirm('Ghi nhận khách không đến lấy và hủy đơn?')) {
      this.form.run(this.api.patch(`orders/${v.order.id}/status`, { expectedStatus: v.order.status, status: 'cancelled', reason: 'Khách không đến lấy', noPickup: true }, v.order.version), () => this.load());
    }
  }

  protected reveal(v: any): void {
    this.form.run(this.api.post(`orders/${v.order.id}/reveal-contact`), (c) => this.contact.set(c));
  }

  protected async move(v: any, status: string): Promise<void> {
    let reason: string | null = null;
    if (status === 'cancelled') {
      reason = await askText('Lý do hủy');
      if (!reason) return;
    }

    this.form.run(this.api.patch(`orders/${v.order.id}/status`, { expectedStatus: v.order.status, status, reason }, v.order.version), () => this.load());
  }

  protected async confirmContact(v: any): Promise<void> {
    const note = await askText('Ghi chú cuộc gọi');
    if (note !== null) {
      this.form.run(this.api.patch(`orders/${v.order.id}/contact-confirmed`, { note }, v.order.version), () => this.load());
    }
  }

  protected pay(v: any): void {
    this.form.run(this.api.patch(`orders/${v.order.id}/payment`, {}, v.order.version), () => this.load());
  }

  // Bấm Hủy trong hộp thoại (askText trả null) thì không ghi gì
  protected async refund(v: any): Promise<void> {
    const note = await askText('Ghi chú hoàn tiền');
    if (note !== null) {
      this.form.run(this.api.patch(`orders/${v.order.id}/refund`, { note }, v.order.version), () => this.load());
    }
  }
}
