import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { ChevronRight, LucideAngularModule, TicketPercent, X } from 'lucide-angular';

import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { Cart } from '../../core/cart';
import { AddressPicker, Pin } from '../../shared/address-picker';
import { Submit } from '../../shared/form';
import { Modal } from '../../shared/modal';
import { addDays, localPhone, today, VnDatePipe } from '../../shared/format';
import { buyerBody, emptyBuyer, InvoiceForm } from '../../shared/invoice-form';
import { discountShort, discountTerms, VOUCHER_REASON, VOUCHER_SOURCE, VoucherOption } from '../../shared/voucher';
import { VndPipe } from '../../shared/vnd.pipe';
import { linesOf } from './cart';

const MAX_AUTO_DAYS = 14;

@Component({
  selector: 'ot-checkout',
  imports: [FormsModule, RouterLink, VndPipe, VnDatePipe, LucideAngularModule, InvoiceForm, AddressPicker, Modal],
  template: `
    <div class="ot-page-split">
      <h1>Thanh toán</h1>
      @if (cart.lines().length === 0) {
        <div class="ot-panel mt-5"><p>Giỏ hàng đang trống.</p><a class="ot-btn ot-btn-primary mt-3" routerLink="/menu">Xem thực đơn</a></div>
      } @else {
        <form class="ot-split" (ngSubmit)="submit()">
          <section class="ot-panel">
            <h2>Thông tin nhận món</h2>
            <div class="ot-form-grid mt-4">
              @if (deliveryOn()) {
                <div class="ot-span-2 flex gap-2" role="radiogroup" aria-label="Cách nhận món">
                  <button type="button" class="ot-chip" role="radio" [attr.aria-checked]="!delivering()" [class.ot-chip-active]="!delivering()" (click)="delivering.set(false)">Đến quán lấy</button>
                  <button type="button" class="ot-chip" role="radio" [attr.aria-checked]="delivering()" [class.ot-chip-active]="delivering()" (click)="delivering.set(true)">Giao tận nơi</button>
                </div>
                @if (delivering()) {
                  <ot-address-picker class="ot-span-2" [address]="address" (addressChange)="onAddress($event)" [pin]="pin" (pinChange)="onPin($event)" [center]="origin()" />
                  <div class="ot-span-2 -mt-2 flex flex-wrap items-center gap-3">
                    <button type="button" class="ot-btn ot-btn-outline" [disabled]="shipper.busy() || address.trim().length < 8" (click)="checkShip()">Tính phí ship</button>
                    @if (ship(); as s) { <span>Cách quán {{ s.distanceKm }} km · Phí ship <b>{{ s.fee === 0 ? 'Miễn phí' : (s.fee | vnd) }}</b></span> }
                    @if (shipper.error(); as e) { <span class="ot-error">{{ e }}</span> }
                  </div>
                }
              }
              <label>Họ tên<input class="ot-input" name="name" required [(ngModel)]="model.customerName" /></label>
              <label>Số điện thoại<input class="ot-input" name="phone" type="tel" required [(ngModel)]="model.customerPhone" /></label>
              <label class="ot-span-2">Email<input class="ot-input" name="email" type="email" required [(ngModel)]="model.customerEmail" /></label>
              <label>{{ delivering() ? 'Ngày nhận hàng' : 'Ngày lấy' }}<input class="ot-input" name="date" type="date" required [min]="minDate" [(ngModel)]="model.pickupDate" (ngModelChange)="pickDate()" /></label>
              <!-- Một ô chọn giờ như các app đặt món, chọn sẵn giờ sớm nhất -->
              <label>{{ delivering() ? 'Giờ nhận hàng' : 'Giờ lấy' }}
                <select class="ot-input" name="time" required [disabled]="times().length === 0" [(ngModel)]="model.pickupTime">
                  @if (times().length === 0) { <option value="">{{ loaded() ? 'Không còn giờ trống' : 'Đang tải giờ…' }}</option> }
                  @for (t of times(); track t; let first = $first) { <option [value]="t">{{ t.slice(0, 5) }}{{ first ? ' (sớm nhất)' : '' }}</option> }
                </select>
              </label>
              @if (moved()) { <p class="ot-span-2 -mt-2 text-sm text-muted">Hôm nay đã hết giờ nhận đơn, đã chuyển sang ngày gần nhất quán mở cửa.</p> }
              @if (loaded() && times().length === 0) { <p class="ot-error ot-span-2 -mt-2">{{ model.pickupDate === minDate ? 'Hôm nay đã hết giờ nhận đơn.' : 'Ngày này quán không nhận đơn.' }} Vui lòng chọn ngày khác.</p> }
              <label class="ot-span-2">Hình thức thanh toán
                <select class="ot-input" name="pay" [(ngModel)]="model.paymentMethod">
                  <option value="cash">{{ delivering() ? 'Tiền mặt khi nhận hàng' : 'Tiền mặt khi lấy món' }}</option>
                  <option value="bank_transfer">Chuyển khoản</option>
                </select>
              </label>
              <label class="ot-span-2">Ghi chú cho cả đơn<textarea class="ot-input" name="note" rows="2" placeholder="Ví dụ: lấy thêm đũa, gọi trước khi làm món" [(ngModel)]="model.note"></textarea></label>
              <label class="ot-span-2 flex items-center gap-2 font-normal"><input type="checkbox" name="wantInvoice" [(ngModel)]="wantInvoice" /> Xuất hóa đơn</label>
              @if (wantInvoice) { <div class="ot-span-2"><ot-invoice-form [model]="invoice" /></div> }
            </div>
          </section>
          <aside class="ot-panel">
            @if (quote(); as q) {
              <section>
                <h2>Đơn của bạn</h2>
                @for (row of q.lines; track row.index) {
                  @if (row.line; as l) {
                    <div class="mt-2 flex justify-between gap-3">
                      <span class="min-w-0">{{ l.quantity }} × {{ l.name }}
                        @for (o of l.options; track $index) { <small class="block text-muted">{{ o.groupName }}: {{ o.optionName }}</small> }
                        @if (l.note) { <small class="block text-muted">Ghi chú: {{ l.note }}</small> }
                      </span>
                      <span class="whitespace-nowrap">{{ l.lineTotal | vnd }}</span>
                    </div>
                  }
                }
                <p class="mt-2 flex justify-between border-t border-line pt-2"><span>Tạm tính</span><span>{{ q.subTotal | vnd }}</span></p>
                @if (delivering()) { <p class="flex justify-between"><span>Phí ship</span><span>{{ ship() ? (shipFee() === 0 ? 'Miễn phí' : (shipFee() | vnd)) : 'Chưa tính' }}</span></p> }
                @if (applied(); as v) {
                  @if (v.shippingDiscount > 0) {
                    <p class="flex justify-between"><span>Giảm phí ship ({{ appliedCode() }})</span><span>-{{ v.shippingDiscount | vnd }}</span></p>
                  } @else {
                    <p class="flex justify-between"><span>Voucher {{ appliedCode() }}</span><span>-{{ v.voucherDiscount | vnd }}</span></p>
                  }
                  <p class="flex justify-between font-semibold"><span>Tổng cộng</span><span class="text-primary">{{ v.totalWithVoucher + shipFee() - v.shippingDiscount | vnd }}</span></p>
                } @else {
                  @if (q.memberDiscount > 0) { <p class="flex justify-between"><span>Giảm hạng thành viên</span><span>-{{ q.memberDiscount | vnd }}</span></p> }
                  <p class="flex justify-between font-semibold"><span>Tổng cộng</span><span class="text-primary">{{ q.totalAmount + shipFee() | vnd }}</span></p>
                }
              </section>
            }
            <div class="mt-4 border-t border-line pt-4">
              @if (applied(); as v) {
                <div class="ot-ticket is-picked">
                  <div class="ot-ticket-stub"><lucide-icon [img]="icons.ticket" [size]="18" /><small class="mt-1">-{{ v.voucherDiscount + v.shippingDiscount | vnd }}</small></div>
                  <div class="flex min-w-0 flex-1 items-center justify-between gap-2 p-3">
                    <span class="min-w-0"><b class="block truncate">{{ appliedCode() }}</b><small class="text-muted">Đã áp dụng</small></span>
                    <span class="flex shrink-0 gap-3 text-sm font-medium"><button type="button" class="text-primary" (click)="openPicker()">Đổi</button><button type="button" class="text-muted" (click)="removeVoucher()">Bỏ</button></span>
                  </div>
                </div>
                @if (v.memberIsBetter) { <p class="mt-2 text-sm text-muted">Lưu ý: giảm theo hạng thành viên có lợi hơn ({{ v.totalWithMemberDiscount | vnd }}); mỗi đơn chỉ dùng một loại.</p> }
              } @else {
                <button type="button" class="flex min-h-12 w-full items-center gap-3 rounded-md border border-dashed border-primary px-4 text-left" (click)="openPicker()">
                  <lucide-icon class="shrink-0 text-muted" [img]="icons.ticket" [size]="18" />
                  <span class="min-w-0 flex-1"><span class="block font-medium whitespace-nowrap">Chọn mã giảm giá</span>@if (usableCount() > 0) { <small class="block font-medium text-success">{{ usableCount() }} mã dùng được cho đơn này</small> }</span>
                  <lucide-icon class="shrink-0 text-muted" [img]="icons.next" [size]="18" />
                </button>
              }
            </div>
            @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
            <button class="ot-btn ot-btn-primary mt-4 w-full" [disabled]="form.busy() || !model.pickupTime || (delivering() && !ship())">Đặt món</button>
          </aside>
        </form>
      }
    </div>

    @if (picker()) {
      <div class="ot-sheet-back" role="dialog" aria-modal="true" aria-label="Chọn mã giảm giá" otModal (dismiss)="picker.set(false)">
        <div class="ot-sheet">
          <div class="ot-sheet-head">
            <p class="font-heading text-lg font-medium">Chọn mã giảm giá</p>
            <button type="button" class="flex size-11 items-center justify-center" aria-label="Đóng" (click)="picker.set(false)"><lucide-icon [img]="icons.close" [size]="18" /></button>
          </div>
          <div class="ot-sheet-body">
            <form class="flex gap-2" (ngSubmit)="check(voucher)">
              <input class="ot-input flex-1" name="voucher" placeholder="Nhập mã giảm giá" autocapitalize="characters" aria-label="Mã giảm giá" [(ngModel)]="voucher" />
              <button class="ot-btn ot-btn-outline shrink-0" [disabled]="!voucher.trim() || checker.busy()">Áp dụng</button>
            </form>
            @if (checker.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
            @if (optionsLoading()) {
              <p class="text-muted">Đang tìm mã cho đơn của bạn…</p>
            } @else {
              @for (o of options(); track o.code) {
                <button type="button" class="ot-ticket w-full text-left" [class.is-off]="!o.usable" [class.is-picked]="o.code === appliedCode() && applied()" [disabled]="!o.usable || checker.busy()" (click)="check(o.code)">
                  <span class="ot-ticket-stub"><span class="ot-ticket-amount">{{ short(o) }}</span><small>giảm</small></span>
                  <span class="flex min-w-0 flex-1 flex-col gap-0.5 p-3">
                    <span class="text-xs font-medium text-muted">{{ o.title || sources[o.source] || 'Voucher' }}</span>
                    <b>{{ o.code }}</b>
                    <span class="text-sm text-muted">{{ terms(o) }}@if (o.endDate) { · HSD {{ o.endDate | vnDate }} }</span>
                    @if (o.usable) {
                      <span class="text-sm font-medium text-success">Giảm {{ o.discount | vnd }} cho đơn này</span>
                    } @else {
                      <span class="text-sm text-danger">{{ reasons[o.errorCode ?? ''] || 'Chưa dùng được cho đơn này' }}</span>
                    }
                  </span>
                </button>
              } @empty {
                <p class="text-muted">Bạn chưa có mã nào dùng được. Đổi điểm thưởng lấy voucher trong mục <a class="font-medium text-primary underline" routerLink="/tai-khoan/diem-thuong" (click)="picker.set(false)">Điểm thưởng</a>.</p>
              }
            }
          </div>
        </div>
      </div>
    }
  `,
})
export class Checkout {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected readonly cart = inject(Cart);
  protected readonly minDate = today();
  protected readonly form = new Submit();
  protected readonly checker = new Submit();
  protected readonly times = signal<string[]>([]);
  protected readonly applied = signal<Schemas['VoucherCheckDto'] | null>(null);
  protected readonly appliedCode = signal('');
  protected readonly quote = signal<Schemas['OrderQuoteDto'] | null>(null);
  protected voucher = '';
  protected readonly picker = signal(false);
  protected readonly options = signal<VoucherOption[]>([]);
  protected readonly optionsLoading = signal(false);
  protected readonly usableCount = computed(() => this.options().filter((o) => o.usable).length);
  protected readonly icons = { ticket: TicketPercent, next: ChevronRight, close: X };
  protected readonly short = discountShort;
  protected readonly terms = discountTerms;
  protected readonly sources = VOUCHER_SOURCE;
  protected readonly reasons = VOUCHER_REASON;
  protected readonly moved = signal(false);
  protected readonly loaded = signal(false);
  // Chỉ tự chuyển ngày ở lần mở trang; khách đã tự chọn ngày thì giữ nguyên
  private autoPick = true;
  private tries = 0;
  private readonly settings = toSignal(this.api.getOr<Record<string, string>>('public/settings', {}), { initialValue: {} as Record<string, string> });
  protected readonly deliveryOn = computed(() => this.settings()['delivery.enabled'] === 'true');
  // Vị trí quán ("vĩ độ,kinh độ" trong cài đặt) để mở bản đồ và gợi ý địa chỉ gần quán
  protected readonly origin = computed<Pin | null>(() => {
    const [lat, lng] = (this.settings()['delivery.origin'] ?? '').split(',').map(Number);
    return Number.isFinite(lat) && Number.isFinite(lng) && lat !== 0 ? { lat, lng } : null;
  });
  protected readonly delivering = signal(false);
  protected readonly ship = signal<Schemas['DeliveryQuoteDto'] | null>(null);
  protected readonly shipFee = computed(() => (this.delivering() ? (this.ship()?.fee ?? 0) : 0));
  protected readonly shipper = new Submit();
  protected address = '';
  protected pin: Pin | null = null;
  protected wantInvoice = false;
  protected readonly invoice = emptyBuyer();
  protected model = { customerName: '', customerPhone: '', customerEmail: '', pickupDate: today(), pickupTime: '', paymentMethod: 'cash', note: '' };

  constructor() {
    const profile = inject(Auth).profile();
    this.model.customerName = profile?.fullName ?? '';
    this.model.customerPhone = localPhone(profile?.phoneNumber);
    this.model.customerEmail = profile?.email ?? '';
    this.loadTimes();
    effect(() => {
      this.shipFee();
      untracked(() => this.applied()?.shippingDiscount && this.removeVoucher());
    });
    effect(() => {
      const items = linesOf(this.cart);
      untracked(() => this.applied.set(null));
      if (items.length > 0) {
        this.api.post<Schemas['OrderQuoteDto']>('orders/quote', { items, lang: 'vi' }).subscribe({ next: (q) => this.quote.set(q), error: () => undefined });
        untracked(() => this.loadOptions());
      }
    });
  }

  // Phí ship do server tính từ quãng đường thật (tới điểm ghim nếu có); lúc đặt đơn server tính lại lần nữa
  protected checkShip(): void {
    const address = this.address.trim();
    if (address.length >= 8 && !this.shipper.busy()) {
      this.shipper.run(this.api.post<Schemas['DeliveryQuoteDto']>('orders/delivery-quote', { address, latitude: this.pin?.lat ?? null, longitude: this.pin?.lng ?? null }), (s) => this.ship.set(s));
    }
  }

  // Đã ghim thì phí tính theo điểm ghim, sửa chữ (số nhà, ghi chú hẻm) không làm đổi phí
  protected onAddress(value: string): void {
    this.address = value;
    if (!this.pin) {
      this.ship.set(null);
    }
  }

  protected onPin(pin: Pin | null): void {
    this.pin = pin;
    this.ship.set(null);
    this.checkShip();
  }

  protected pickDate(): void {
    this.autoPick = false;
    this.moved.set(false);
    this.loadTimes();
  }

  protected loadTimes(): void {
    this.model.pickupTime = '';
    this.loaded.set(false);
    const date = this.model.pickupDate;
    this.api.getOr<Schemas['PickupTimesDto'] | null>('orders/pickup-times', null, { date }).subscribe((p) => {
      // Khách đã đổi ngày trong lúc chờ: kết quả này của ngày cũ
      if (date !== this.model.pickupDate) {
        return;
      }

      const times = p?.times ?? [];
      if (this.autoPick && times.length === 0 && this.tries < MAX_AUTO_DAYS) {
        this.tries += 1;
        this.moved.set(true);
        this.model.pickupDate = addDays(this.model.pickupDate, 1);
        this.loadTimes();
        return;
      }

      this.autoPick = false;
      this.times.set(times);
      this.model.pickupTime = times[0] ?? '';
      this.loaded.set(true);
    });
  }

  protected openPicker(): void {
    this.checker.error.set(null);
    this.picker.set(true);
    this.loadOptions();
  }

  // Danh sách mã tính theo đúng giỏ hiện tại: mã nào dùng được, giảm bao nhiêu, mã nào chưa và vì sao
  private loadOptions(): void {
    const items = linesOf(this.cart);
    this.optionsLoading.set(true);
    this.api.post<VoucherOption[]>('account/voucher-options', { items, deliveryFee: this.shipFee() || null }).subscribe({
      next: (o) => {
        // Giỏ đã đổi trong lúc chờ: kết quả này của giỏ cũ
        if (JSON.stringify(items) === JSON.stringify(linesOf(this.cart))) {
          this.options.set(o);
          this.optionsLoading.set(false);
        }
      },
      error: () => {
        this.options.set([]);
        this.optionsLoading.set(false);
      },
    });
  }

  protected check(input: string): void {
    const code = input.trim();
    this.checker.run(this.api.post<Schemas['VoucherCheckDto']>('vouchers/validate', { code, items: linesOf(this.cart), deliveryFee: this.shipFee() || null }), (v) => {
      this.appliedCode.set(code);
      this.applied.set(v);
      this.voucher = '';
      this.picker.set(false);
    });
  }

  protected removeVoucher(): void {
    this.applied.set(null);
    this.appliedCode.set('');
  }

  protected submit(): void {
    const body = { ...this.model, note: this.model.note || null, items: linesOf(this.cart), lang: 'vi', voucherCode: this.applied() ? this.appliedCode() : null, invoice: this.wantInvoice ? buyerBody(this.invoice) : null, channel: this.delivering() ? 'delivery' : 'pickup', deliveryAddress: this.delivering() ? this.address.trim() : null, deliveryLatitude: this.delivering() ? (this.pin?.lat ?? null) : null, deliveryLongitude: this.delivering() ? (this.pin?.lng ?? null) : null };
    this.form.run(this.api.post<Schemas['CreatedOrderDto']>('orders', body), (created) => {
      this.cart.clear();
      void this.router.navigate(['/xac-nhan', created.order.code]);
    });
  }
}
