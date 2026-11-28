import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { reloadOnLive } from '../core/live';
import { appLabel } from '../shared/platform';
import { Latest, ORDER_STATUS, Submit, VnDatePipe, VndPipe } from '../shared/ui';
import { askConfirm, askText } from '../../../../customer/src/app/core/dialog';
import { buyerBody, emptyBuyer, InvoiceForm } from '../../../../customer/src/app/shared/invoice-form';

const ORDER_NEXT: Record<string, string[]> = { pending: ['preparing', 'cancelled'], preparing: ['ready', 'cancelled'], ready: ['completed', 'cancelled'], delivering: ['completed', 'cancelled'] };
const CHANNEL: Record<string, string> = { pickup: 'Đặt online', delivery: 'Giao hàng', dine_in: 'Tại bàn', counter: 'Mua tại quầy', platform: 'Đơn app' };

type OrderRow = Schemas['AdminOrderDto'];
type OrderDetail = Schemas['AdminOrderDetailDto'];

// Đơn tại bàn và mua tại quầy xử lý ở màn hình Gọi món (nút "Mở phiếu"), không đổi trạng thái ở đây
const BILL_CHANNELS = new Set(['dine_in', 'counter']);
const OPEN_STATUSES = ['pending', 'preparing', 'ready', 'delivering'];
const HISTORY_STATUSES = ['completed', 'cancelled'];

function isOpenBill(order: Pick<OrderRow, 'status' | 'channel'>): boolean {
  return BILL_CHANNELS.has(order.channel ?? '') && OPEN_STATUSES.includes(order.status);
}

function statusLabel(order: Pick<OrderRow, 'status' | 'deleted'>): string {
  return order.deleted ? 'Đã xóa' : ORDER_STATUS[order.status];
}

function isoDay(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

// Đơn giao hàng: sẵn sàng thì chuyển "Đang giao" (không hoàn thành thẳng)
function nextSteps(order: Pick<OrderRow, 'status' | 'channel'>): string[] {
  if (BILL_CHANNELS.has(order.channel ?? '')) return [];
  const steps = ORDER_NEXT[order.status] ?? [];
  return order.channel === 'delivery' && order.status === 'ready' ? ['delivering', 'cancelled'] : steps;
}

@Component({
  selector: 'ot-orders',
  imports: [FormsModule, RouterLink, VndPipe, VnDatePipe],
  template: `
    <h1>Đơn hàng</h1>
    @if (attention(); as a) {
      @if (a.pendingOverdue.length + a.notPickedUp.length > 0) {
        <p class="ot-error mt-2">Cần xử lý: {{ a.pendingOverdue.length }} chờ quá hạn · {{ a.notPickedUp.length }} chưa lấy</p>
      }
    }
    <div class="ot-scroll-row my-3" role="tablist" aria-label="Nhóm đơn" style="margin-inline: 0; padding-inline: 0">
      <button type="button" role="tab" class="ot-chip" [class.ot-chip-active]="view() === 'open'" [attr.aria-selected]="view() === 'open'" (click)="setView('open')">Đang xử lý</button>
      <button type="button" role="tab" class="ot-chip" [class.ot-chip-active]="view() === 'history'" [attr.aria-selected]="view() === 'history'" (click)="setView('history')">Lịch sử</button>
    </div>
    <div class="a-filters">
      <input class="ot-input" name="q" placeholder="Tìm mã, tên" [(ngModel)]="q" (change)="reload()" />
      <select class="ot-input" name="s" [(ngModel)]="status" (change)="reload()"><option value="">Mọi trạng thái</option>@for (s of statusKeys(); track s) { <option [value]="s">{{ labels[s] }}</option> }</select>
      @if (view() === 'history') {
        <input class="ot-input" name="from" type="date" aria-label="Từ ngày" [(ngModel)]="from" (change)="reload()" />
        <input class="ot-input" name="to" type="date" aria-label="Đến ngày" [(ngModel)]="to" (change)="reload()" />
      }
    </div>
    @if (list.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (act.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    <div class="a-table hidden md:block"><table class="w-full text-sm"><thead><tr class="bg-surface-2 text-left"><th class="p-2">Mã</th><th class="p-2">Khách</th><th class="p-2">Lấy lúc</th><th class="p-2">Tổng</th><th class="p-2">Trạng thái</th><th class="p-2">Thanh toán</th><th class="p-2"><span class="sr-only">Thao tác</span></th></tr></thead>
      <tbody>@for (o of rows(); track o.id) {
        <tr class="border-b border-line" [class.text-muted]="o.deleted"><td class="p-2"><a class="text-primary" [routerLink]="['/orders', o.id]">{{ o.code }}</a><small class="block text-muted">{{ channel[o.channel] }}</small></td><td class="p-2">{{ o.customerName }}<small class="block text-muted">{{ o.maskedPhone }}</small></td><td class="p-2 whitespace-nowrap">{{ o.pickupTime.slice(0, 5) }} {{ o.pickupDate | vnDate }}</td><td class="p-2" [class.line-through]="o.deleted">{{ o.totalAmount | vnd }}</td><td class="p-2">@if (o.deleted) { <span class="a-badge">Đã xóa</span> } @else { {{ labels[o.status] }} }</td><td class="p-2">{{ o.paymentStatus === 'paid' ? 'Đã trả' : 'Chưa trả' }}</td>
          <td class="p-2 whitespace-nowrap text-right">
            @if (openBill(o)) { <a class="ot-chip" [routerLink]="['/dine-in', o.id]">Mở phiếu</a> }
            @if (o.status === 'completed' && canDelete()) { <button type="button" class="ot-chip" (click)="remove(o)">Xóa</button> }
          </td></tr>
      } @empty { <tr><td colspan="7" class="p-4 text-muted">Không có đơn.</td></tr> }</tbody></table></div>
    <!-- Điện thoại: mỗi đơn một thẻ, không kéo ngang -->
    <ul class="a-card md:hidden">
      @for (o of rows(); track o.id) {
        <li class="a-order-item" [class.text-muted]="o.deleted">
          <div class="flex items-baseline justify-between gap-3">
            <a class="font-medium text-primary" [routerLink]="['/orders', o.id]">{{ o.code }}</a>
            <span class="shrink-0 font-medium tabular-nums" [class.line-through]="o.deleted">{{ o.totalAmount | vnd }}</span>
          </div>
          <p class="text-sm">{{ o.customerName }} · {{ channel[o.channel] }}</p>
          <p class="text-sm text-muted">{{ o.pickupTime.slice(0, 5) }} {{ o.pickupDate | vnDate }} · @if (o.deleted) { <span class="a-badge">Đã xóa</span> } @else { {{ labels[o.status] }} } · {{ o.paymentStatus === 'paid' ? 'Đã trả' : 'Chưa trả' }}</p>
          @if (openBill(o)) { <a class="ot-btn ot-btn-outline w-full" [routerLink]="['/dine-in', o.id]">Mở phiếu</a> }
          @if (o.status === 'completed' && canDelete()) { <button type="button" class="ot-btn ot-btn-outline w-full" (click)="remove(o)">Xóa bill</button> }
        </li>
      } @empty { <li class="a-order-item text-muted">Không có đơn.</li> }
    </ul>
    @if (rows().length < total()) { <button type="button" class="ot-btn ot-btn-outline mt-3" (click)="more()">Xem thêm</button> }
  `,
})
export class Orders {
  private readonly api = inject(Api);
  private readonly auth = inject(Auth);
  protected readonly channel = CHANNEL;
  protected readonly labels = ORDER_STATUS;
  protected readonly openBill = isOpenBill;
  // Đang xử lý: đơn còn mở, cũ nhất trước. Lịch sử: đã hoàn tất, đã hủy, đã xóa; mặc định 7 ngày gần nhất
  protected readonly view = signal<'open' | 'history'>('open');
  protected readonly statusKeys = computed(() => (this.view() === 'open' ? OPEN_STATUSES : HISTORY_STATUSES));
  protected readonly canDelete = computed(() => this.auth.has('orders_void'));
  protected readonly rows = signal<OrderRow[]>([]);
  protected readonly total = signal(0);
  protected readonly attention = signal<Schemas['OrderAttentionDto'] | null>(null);
  protected readonly list = new Latest();
  protected readonly act = new Submit();
  protected q = '';
  protected status = '';
  protected from = '';
  protected to = '';
  private page = 1;

  constructor() {
    this.reload();
    reloadOnLive(() => this.reload());
    this.api.get<Schemas['OrderAttentionDto']>('orders/attention').subscribe({ next: (a) => this.attention.set(a), error: () => undefined });
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

  protected setView(view: 'open' | 'history'): void {
    this.view.set(view);
    this.status = '';
    const today = new Date();
    this.to = view === 'history' ? isoDay(today) : '';
    this.from = view === 'history' ? isoDay(new Date(today.getFullYear(), today.getMonth(), today.getDate() - 6)) : '';
    this.reload();
  }

  // Chủ quán 2026-11-26: xóa bill đã hoàn thành vì nhập sai — bắt buộc lý do, dòng vẫn giữ với nhãn "Đã xóa"
  protected async remove(o: OrderRow): Promise<void> {
    const reason = await askText(`Lý do xóa bill ${o.code}`);
    if (reason) {
      this.act.run(this.api.post(`orders/${o.id}/delete`, { reason }), () => this.reload());
    }
  }

  private fetch(): void {
    const open = this.view() === 'open';
    this.list.run(this.api.raw<OrderRow[]>('orders', { q: this.q, status: this.status, from: open ? '' : this.from, to: open ? '' : this.to, open, page: this.page, pageSize: 30 }), (r) => {
      this.rows.update((x) => [...x, ...r.data]);
      this.total.set(r.meta?.total ?? 0);
    });
  }
}

@Component({
  selector: 'ot-order',
  imports: [FormsModule, RouterLink, VndPipe, VnDatePipe, InvoiceForm],
  template: `
    @if (form.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (d(); as v) {
      <div class="a-head"><h1>Đơn {{ v.order.code }}</h1><span class="a-badge">{{ status(v.order) }}</span></div>
      <div class="a-row84">
        <section class="a-card">
          <header class="a-card-h">Món đã đặt</header>
          <div class="a-card-b">
            @for (i of v.items; track $index) { <div class="a-line"><span>{{ i.quantity }} × {{ i.name }}@for (o of i.options; track $index) { <small class="block text-muted">{{ o.groupName }}: {{ o.optionName }}</small> }@if (i.note) { <small class="block">{{ i.note }}</small> }</span><span class="whitespace-nowrap tabular-nums">{{ i.lineTotal | vnd }}</span></div> }
            <div class="a-line"><span>Tạm tính</span><span class="tabular-nums">{{ v.subTotal | vnd }}</span></div>
            @if (v.memberDiscount > 0) { <div class="a-line"><span>Giảm hạng</span><span class="tabular-nums">-{{ v.memberDiscount | vnd }}</span></div> }
            @if (v.voucherDiscount > 0) { <div class="a-line"><span>Voucher</span><span class="tabular-nums">-{{ v.voucherDiscount | vnd }}</span></div> }
            @if (v.platformDiscount > 0) { <div class="a-line"><span>Giảm giá trên app</span><span class="tabular-nums">-{{ v.platformDiscount | vnd }}</span></div> }
            @if (v.order.channel === 'delivery') { <div class="a-line"><span>Phí ship</span><span class="tabular-nums">{{ v.deliveryFee | vnd }}</span></div> }
            @if (v.shippingDiscount > 0) { <div class="a-line"><span>Giảm phí ship</span><span class="tabular-nums">-{{ v.shippingDiscount | vnd }}</span></div> }
            <div class="a-line font-semibold"><span>Tổng cộng</span><span class="text-primary tabular-nums">{{ v.order.totalAmount | vnd }}</span></div>
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
              @if (v.order.paymentGroup !== 'in_store') {
                <dt>Thanh toán</dt><dd>Qua app {{ appName(v.order) }} · mã đơn app {{ v.order.externalOrderCode }}</dd>
              } @else {
                <dt>Thanh toán</dt><dd>{{ v.order.paymentMethod === 'cash' ? 'Tiền mặt' : 'Chuyển khoản' }} · {{ v.order.paymentStatus === 'paid' ? 'Đã trả' : 'Chưa trả' }}</dd>
              }
            </dl></div>
          </section>
          <section class="a-card">
            <header class="a-card-h">Xử lý</header>
            <div class="a-card-b a-btns">
              @if (!contact()) { <button type="button" class="ot-chip" (click)="reveal(v)">Hiện SĐT</button> }
              @if (openBill(v.order)) { <a class="ot-chip" [routerLink]="['/dine-in', v.order.id]">Mở phiếu</a> }
              @if (v.order.status === 'completed' && auth.has('orders_void')) { <button type="button" class="ot-chip" (click)="remove(v)">Xóa bill</button> }
              @for (s of next(v.order); track s) { <button type="button" class="ot-chip" (click)="move(v, s)">{{ labels[s] }}</button> }
              @if (v.order.status === 'ready' && auth.has('orders_cancel')) { <button type="button" class="ot-chip" (click)="noPickup(v)">Khách không đến lấy</button> }
              @if (v.order.needsContactConfirm && !v.order.contactConfirmed) { <button type="button" class="ot-chip" (click)="confirmContact(v)">Đã gọi xác nhận</button> }
              @if (v.order.paymentStatus === 'unpaid' && v.order.status !== 'cancelled' && !openBill(v.order)) { <button type="button" class="ot-chip" (click)="pay(v)">Đã thu tiền</button> }
              @if (v.order.status !== 'cancelled') { <button type="button" class="ot-chip" (click)="startInvoice()">Yêu cầu xuất hóa đơn</button> }
            </div>
            @if (invoiceSent()) { <p class="px-4 pb-3 text-sm font-medium">Đã gửi yêu cầu xuất hóa đơn cho kế toán.</p> }
          </section>
          <section class="a-card">
            <header class="a-card-h">Lịch sử</header>
            <div class="a-card-b">@for (h of v.history; track $index) { <div class="a-line text-sm"><span>{{ h.fromStatus === 'completed' && h.toStatus === 'cancelled' ? 'Đã xóa' : labels[h.toStatus] }} @if (h.reason) { ({{ h.reason }}) }</span><span class="whitespace-nowrap text-muted">{{ h.changedAtUtc | vnDate: true }}</span></div> } @empty { <p class="text-muted">Chưa có.</p> }</div>
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
  protected readonly openBill = isOpenBill;
  protected readonly status = statusLabel;
  protected readonly d = signal<OrderDetail | null>(null);

  protected appName(o: Schemas['AdminOrderDto']): string {
    return appLabel(o.paymentGroup ?? 'in_store', o.platform);
  }
  protected readonly contact = signal<Schemas['OrderContactDto'] | null>(null);
  protected readonly form = new Submit();
  protected readonly invoiceForm = new Submit();
  protected readonly invoicing = signal(false);
  protected readonly invoiceSent = signal(false);
  protected buyer = emptyBuyer();

  constructor() {
    this.load();
  }

  private load(): void {
    this.form.run(this.api.get<OrderDetail>(`orders/${this.id}`), (v) => this.d.set(v));
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
  protected next(order: Pick<OrderRow, 'status' | 'channel' | 'needsContactConfirm' | 'contactConfirmed'>): string[] {
    const blocked = order.status === 'pending' && order.needsContactConfirm && !order.contactConfirmed;
    return nextSteps(order).filter((s) => (s === 'cancelled' ? this.auth.has('orders_cancel') : s === 'completed' || s === 'delivering' ? this.auth.has('orders_cashier') : this.auth.has('orders_kitchen') && !blocked));
  }

  // Khách ghim vị trí thì chỉ đường tới đúng điểm ghim; không thì tìm theo địa chỉ chữ
  protected mapLink(v: OrderDetail): string {
    const to = v.deliveryLatitude != null ? `${v.deliveryLatitude},${v.deliveryLongitude}` : (v.deliveryAddress ?? '');
    return `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(to)}`;
  }

  protected async noPickup(v: OrderDetail): Promise<void> {
    if (await askConfirm('Ghi nhận khách không đến lấy và hủy đơn?')) {
      this.form.run(this.api.patch(`orders/${v.order.id}/status`, { expectedStatus: v.order.status, status: 'cancelled', reason: 'Khách không đến lấy', noPickup: true }, v.order.version), () => this.load());
    }
  }

  protected reveal(v: OrderDetail): void {
    this.form.run(this.api.post<Schemas['OrderContactDto']>(`orders/${v.order.id}/reveal-contact`), (c) => this.contact.set(c));
  }

  protected async move(v: OrderDetail, status: string): Promise<void> {
    let reason: string | null = null;
    if (status === 'cancelled') {
      reason = await askText('Lý do hủy');
      if (!reason) return;
    }

    this.form.run(this.api.patch(`orders/${v.order.id}/status`, { expectedStatus: v.order.status, status, reason }, v.order.version), () => this.load());
  }

  protected async confirmContact(v: OrderDetail): Promise<void> {
    const note = await askText('Ghi chú cuộc gọi');
    if (note !== null) {
      this.form.run(this.api.patch(`orders/${v.order.id}/contact-confirmed`, { note }, v.order.version), () => this.load());
    }
  }

  protected pay(v: OrderDetail): void {
    this.form.run(this.api.patch(`orders/${v.order.id}/payment`, {}, v.order.version), () => this.load());
  }

  // Bấm Hủy trong hộp thoại (askText trả null) thì không ghi gì; lý do bắt buộc để quản lý hỏi lại
  protected async remove(v: OrderDetail): Promise<void> {
    const reason = await askText(`Lý do xóa bill ${v.order.code}`);
    if (reason) {
      this.form.run(this.api.post(`orders/${v.order.id}/delete`, { reason }), () => this.load());
    }
  }
}
