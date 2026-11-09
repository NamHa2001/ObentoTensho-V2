import { Component, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';

import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { Cart } from '../../core/cart';
import { Submit } from '../../shared/form';
import { today } from '../../shared/format';
import { VndPipe } from '../../shared/vnd.pipe';
import { linesOf } from './cart';

@Component({
  selector: 'ot-checkout',
  imports: [FormsModule, RouterLink, VndPipe],
  template: `
    <div class="ot-page">
      <h1>Thanh toán</h1>
      @if (cart.lines().length === 0) {
        <p class="mt-3">Giỏ hàng đang trống.</p><a class="ot-btn ot-btn-primary mt-3" routerLink="/menu">Xem thực đơn</a>
      } @else {
        <form class="ot-stack" (ngSubmit)="submit()">
          @if (quote(); as q) {
            <section class="ot-card p-4">
              <h2 class="text-lg">Đơn của bạn</h2>
              @for (row of q.lines; track row.index) {
                @if (row.line; as l) { <p class="mt-2 flex justify-between gap-3"><span>{{ l.quantity }} × {{ l.name }}</span><span class="whitespace-nowrap">{{ l.lineTotal | vnd }}</span></p> }
              }
              <p class="mt-2 flex justify-between border-t border-line pt-2"><span>Tạm tính</span><span>{{ q.subTotal | vnd }}</span></p>
              @if (applied(); as v) {
                <p class="flex justify-between"><span>Voucher {{ appliedCode() }}</span><span>-{{ v.voucherDiscount | vnd }}</span></p>
                <p class="flex justify-between font-bold"><span>Tổng cộng</span><span class="text-primary">{{ v.totalWithVoucher | vnd }}</span></p>
              } @else {
                @if (q.memberDiscount > 0) { <p class="flex justify-between"><span>Giảm hạng thành viên</span><span>-{{ q.memberDiscount | vnd }}</span></p> }
                <p class="flex justify-between font-bold"><span>Tổng cộng</span><span class="text-primary">{{ q.totalAmount | vnd }}</span></p>
              }
            </section>
          }
          <label>Họ tên<input class="ot-input" name="name" required [(ngModel)]="model.customerName" /></label>
          <label>Số điện thoại<input class="ot-input" name="phone" type="tel" required [(ngModel)]="model.customerPhone" /></label>
          <label>Email<input class="ot-input" name="email" type="email" required [(ngModel)]="model.customerEmail" /></label>
          <label>Ngày lấy<input class="ot-input" name="date" type="date" required [min]="minDate" [(ngModel)]="model.pickupDate" (ngModelChange)="loadTimes()" /></label>
          <fieldset>
            <legend>Giờ lấy</legend>
            @if (times().length === 0) { <p class="ot-error mt-1">Ngày này không còn giờ lấy.</p> }
            <div class="mt-2 flex flex-wrap gap-2">
              @for (t of times(); track t) {
                <button type="button" class="ot-chip" [class.ot-chip-active]="model.pickupTime === t" (click)="model.pickupTime = t">{{ t.slice(0, 5) }}</button>
              }
            </div>
          </fieldset>
          <label>Thanh toán
            <select class="ot-input" name="pay" [(ngModel)]="model.paymentMethod">
              <option value="cash">Tiền mặt khi lấy món</option>
              <option value="bank_transfer">Chuyển khoản</option>
            </select>
          </label>
          <label>Ghi chú<textarea class="ot-input" name="note" rows="2" [(ngModel)]="model.note"></textarea></label>
          <div>
            <label>Mã voucher<input class="ot-input" name="voucher" autocapitalize="characters" [(ngModel)]="voucher" (ngModelChange)="applied.set(null)" /></label>
            <button type="button" class="ot-btn ot-btn-outline mt-2" [disabled]="!voucher || checker.busy()" (click)="check()">Áp dụng</button>
            @if (checker.error(); as e) { <p class="ot-error mt-1" role="alert">{{ e }}</p> }
            @if (applied(); as v) {
              <p class="mt-1">Đã áp dụng mã {{ appliedCode() }}.
                @if (v.memberIsBetter) { <small class="block text-muted">Lưu ý: giảm theo hạng thành viên có lợi hơn ({{ v.totalWithMemberDiscount | vnd }}); mỗi đơn chỉ dùng một loại.</small> }
              </p>
            }
          </div>
          @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
          <button class="ot-btn ot-btn-primary" [disabled]="form.busy() || !model.pickupTime">Đặt món</button>
        </form>
      }
    </div>
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
  protected model = { customerName: '', customerPhone: '', customerEmail: '', pickupDate: today(), pickupTime: '', paymentMethod: 'cash', note: '' };

  constructor() {
    const profile = inject(Auth).profile();
    this.model.customerName = profile?.fullName ?? '';
    this.model.customerPhone = profile?.phoneNumber ?? '';
    this.model.customerEmail = profile?.email ?? '';
    this.loadTimes();
    effect(() => {
      const items = linesOf(this.cart);
      untracked(() => this.applied.set(null));
      if (items.length > 0) {
        this.api.post<Schemas['OrderQuoteDto']>('orders/quote', { items, lang: 'vi' }).subscribe({ next: (q) => this.quote.set(q), error: () => undefined });
      }
    });
  }

  protected loadTimes(): void {
    this.model.pickupTime = '';
    this.api.getOr<Schemas['PickupTimesDto'] | null>('orders/pickup-times', null, { date: this.model.pickupDate }).subscribe((p) => this.times.set(p?.times ?? []));
  }

  protected check(): void {
    this.applied.set(null);
    const code = this.voucher.trim();
    this.checker.run(this.api.post<Schemas['VoucherCheckDto']>('vouchers/validate', { code, items: linesOf(this.cart) }), (v) => {
      this.appliedCode.set(code);
      this.applied.set(v);
    });
  }

  protected submit(): void {
    const body = { ...this.model, note: this.model.note || null, items: linesOf(this.cart), lang: 'vi', voucherCode: this.applied() ? this.appliedCode() : null };
    this.form.run(this.api.post<Schemas['CreatedOrderDto']>('orders', body), (created) => {
      this.cart.clear();
      void this.router.navigate(['/xac-nhan', created.order.code]);
    });
  }
}
