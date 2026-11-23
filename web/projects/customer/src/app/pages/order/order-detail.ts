import { afterNextRender, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { firstValueFrom, Subscription } from 'rxjs';

import { Api } from '../../core/api';
import { askConfirm } from '../../core/dialog';
import { LiveHub } from '../../core/live';
import { errorText, Submit } from '../../shared/form';
import { OrderView, ReservationView } from '../../shared/views';
import { AuthCard } from '../auth/auth-card';
import { takeToken } from '../auth/password';

type Order = Schemas['PublicOrderDto'];

// Xác nhận sau khi đặt và chi tiết đơn trong tài khoản; đơn hoàn thành thì viết được đánh giá
@Component({
  selector: 'ot-order-detail',
  imports: [FormsModule, RouterLink, OrderView],
  template: `
    <div class="ot-page">
      <h1>{{ fresh ? 'Đã nhận đơn của bạn' : 'Chi tiết đơn' }}</h1>
      @if (error(); as e) { <p class="ot-error mt-3">{{ e }}</p> }
      @if (order(); as o) {
        <ot-order-view [order]="o" class="mt-4 block" />
        @if (o.paymentMethod === 'bank_transfer' && o.paymentStatus === 'unpaid' && bank().accountNumber) {
          <div class="ot-card mt-3 p-4">
            <p class="font-medium">Thông tin chuyển khoản</p>
            <p>{{ bank().bankName }} · {{ bank().accountNumber }}</p>
            <p>{{ bank().holder }}</p>
            <p>Nội dung: <b>{{ o.code }}</b></p>
            @if (settings()['payment.qrImageUrl']; as qr) { <img class="mt-2 w-48 rounded-sm" [src]="qr" alt="Mã QR chuyển khoản" /> }
          </div>
        }
        @if (o.canCancel) { <button type="button" class="ot-btn ot-btn-outline mt-3" [disabled]="form.busy()" (click)="cancel(o)">Hủy đơn</button> }
        @if (form.error(); as e) { <p class="ot-error mt-2">{{ e }}</p> }
        @if (o.status === 'completed') {
          <form class="ot-stack" (ngSubmit)="review(o)">
            <h2 class="text-xl">Đánh giá đơn hàng</h2>
            @if (reviewed()) { <p>Cảm ơn bạn đã đánh giá.</p> } @else {
              <label>Số sao
                <select class="ot-input" name="rating" [(ngModel)]="rating">@for (s of [5, 4, 3, 2, 1]; track s) { <option [ngValue]="s">{{ s }} sao</option> }</select>
              </label>
              <label>Nhận xét<textarea class="ot-input" name="comment" rows="3" [(ngModel)]="comment"></textarea></label>
              @if (reviewForm.error(); as e) { <p class="ot-error">{{ e }}</p> }
              <button class="ot-btn ot-btn-primary" [disabled]="reviewForm.busy()">Gửi đánh giá</button>
            }
          </form>
        }
        <a class="ot-btn ot-btn-outline mt-3" routerLink="/tai-khoan/don-hang">Đơn hàng của tôi</a>
      }
    </div>
  `,
})
export class OrderDetail {
  private readonly api = inject(Api);
  private readonly code = inject(ActivatedRoute).snapshot.paramMap.get('code') ?? '';
  protected readonly fresh = inject(ActivatedRoute).snapshot.data['fresh'] === true;
  protected readonly order = signal<Order | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly form = new Submit();
  protected readonly reviewForm = new Submit();
  protected readonly reviewed = signal(false);
  protected rating = 5;
  protected comment = '';
  protected readonly settings = toSignal(this.api.getOr<Record<string, string>>('public/settings', {}), { initialValue: {} as Record<string, string> });
  protected readonly bank = computed(() => ({ bankName: this.settings()['payment.bankName'], accountNumber: this.settings()['payment.accountNumber'], holder: this.settings()['payment.accountHolderName'] }));

  constructor() {
    this.load();
    const hub = new LiveHub({
      url: () => firstValueFrom(this.api.post<{ ticket: string }>('account/realtime-ticket')).then((t) => `/hubs/v1/public?ticket=${encodeURIComponent(t.ticket)}`),
      events: ['order.statusChanged'],
      onEvent: () => this.load(),
      onOpen: (c, reconnected) => {
        if (reconnected) {
          this.load();
        }

        return c.invoke('WatchMyOrder', this.code);
      },
    });
    void hub.start();
    inject(DestroyRef).onDestroy(() => hub.stop());
  }

  private load(): void {
    this.api.get<Order>(`account/orders/${encodeURIComponent(this.code)}`).subscribe({ next: (o) => this.order.set(o), error: (e: unknown) => this.error.set(errorText(e)) });
  }

  protected async cancel(order: Order): Promise<void> {
    if (await askConfirm('Bạn chắc chắn muốn hủy đơn này?')) {
      this.form.run(this.api.post<Order>(`orders/${encodeURIComponent(order.code)}/cancel`, { token: null, reason: null }), (o) => this.order.set(o));
    }
  }

  protected review(order: Order): void {
    this.reviewForm.run(this.api.post('reviews', { orderId: order.id, menuItemId: null, rating: this.rating, comment: this.comment || null }), () => this.reviewed.set(true));
  }
}

// Tra cứu bằng mã + 4 số cuối SĐT
@Component({
  selector: 'ot-lookup',
  imports: [FormsModule, OrderView, ReservationView, AuthCard],
  template: `
    <ot-auth-card heading="Tra cứu" subtitle="Nhập mã đơn hoặc mã đặt bàn cùng 4 số cuối số điện thoại bạn đã dùng khi đặt.">
      <form class="ot-auth-form" (ngSubmit)="submit()">
        <label>Loại
          <select class="ot-input" name="kind" [(ngModel)]="kind">
            <option value="order">Đơn món</option>
            <option value="reservation">Đặt bàn</option>
          </select>
        </label>
        <label>Mã<input class="ot-input" name="code" required [(ngModel)]="code" /></label>
        <label>4 số cuối điện thoại<input class="ot-input" name="last4" inputmode="numeric" maxlength="4" required [(ngModel)]="last4" /></label>
        @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
        <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Tra cứu</button>
      </form>
      @if (order(); as o) { <ot-order-view [order]="o" class="mt-6 block" /> }
      @if (reservation(); as r) { <ot-reservation-view [reservation]="r" class="mt-6 block" /> }
    </ot-auth-card>
  `,
})
export class Lookup {
  private readonly api = inject(Api);
  protected readonly form = new Submit();
  protected readonly order = signal<Order | null>(null);
  protected readonly reservation = signal<Schemas['PublicReservationDto'] | null>(null);
  protected kind: 'order' | 'reservation';
  protected code: string;
  protected last4 = '';

  constructor() {
    const route = inject(ActivatedRoute).snapshot;
    this.kind = route.data['kind'] === 'reservation' ? 'reservation' : 'order';
    this.code = route.queryParamMap.get('code') ?? '';
  }

  protected submit(): void {
    if (!this.code.trim() || !/^\d{4}$/.test(this.last4)) {
      this.form.error.set('Vui lòng nhập mã và đúng 4 số cuối điện thoại.');
      return;
    }

    this.order.set(null);
    this.reservation.set(null);
    const body = { code: this.code.trim(), phoneLast4: this.last4 };
    if (this.kind === 'order') {
      this.form.run(this.api.post<Order>('orders/lookup', body), (o) => this.order.set(o));
    } else {
      this.form.run(this.api.post<Schemas['PublicReservationDto']>('reservations/lookup', body), (r) => this.reservation.set(r));
    }
  }
}

// Liên kết trong email: theo dõi đơn bằng token
@Component({
  selector: 'ot-track',
  imports: [OrderView],
  template: `
    <div class="ot-page">
      <h1>Theo dõi đơn</h1>
      @if (form.error(); as e) { <p class="ot-error mt-3">{{ e }}</p> }
      @if (order(); as o) {
        <ot-order-view [order]="o" class="mt-4 block" />
        @if (o.canCancel) { <button type="button" class="ot-btn ot-btn-outline mt-3" [disabled]="form.busy()" (click)="cancel(o)">Hủy đơn</button> }
      }
    </div>
  `,
})
export class Track {
  private readonly api = inject(Api);
  private readonly token = takeToken(inject(DOCUMENT));
  protected readonly form = new Submit();
  protected readonly order = signal<Order | null>(null);

  constructor() {
    const code = inject(ActivatedRoute).snapshot.queryParamMap.get('code') ?? '';
    // Tải lại do sự kiện realtime không dùng chung Submit với nút hủy, nếu không sẽ bị bỏ qua khi nút đang chờ
    let loading: Subscription | null = null;
    const load = () => {
      loading?.unsubscribe();
      loading = this.api.post<Order>('orders/track', { code, token: this.token }).subscribe({ next: (o) => this.order.set(o), error: (e: unknown) => this.form.error.set(errorText(e)) });
    };
    load();
    const hub = new LiveHub({
      url: () => Promise.resolve('/hubs/v1/public'),
      events: ['order.statusChanged'],
      onEvent: load,
      onOpen: (c) => c.invoke('WatchOrder', code, this.token),
    });
    void hub.start();
    inject(DestroyRef).onDestroy(() => {
      hub.stop();
      loading?.unsubscribe();
    });
  }

  protected async cancel(order: Order): Promise<void> {
    if (await askConfirm('Bạn chắc chắn muốn hủy đơn này?')) {
      this.form.run(this.api.post<Order>(`orders/${encodeURIComponent(order.code)}/cancel`, { token: this.token, reason: null }), (o) => this.order.set(o));
    }
  }
}

// Liên kết trong email: hủy đặt bàn bằng token
@Component({
  selector: 'ot-manage-reservation',
  imports: [ReservationView, RouterLink],
  template: `
    <div class="ot-page">
      <h1>Đặt bàn {{ code }}</h1>
      @if (result(); as r) {
        <ot-reservation-view [reservation]="r" class="mt-4 block" />
      } @else if (!linkOk()) {
        <p class="mt-3">Liên kết không hợp lệ hoặc đã hết hạn. Bạn có thể tra cứu đặt bàn bằng mã và 4 số cuối điện thoại.</p>
        <a class="ot-btn ot-btn-outline mt-4" routerLink="/reservations/lookup">Tra cứu đặt bàn</a>
      } @else {
        <p class="mt-3">Bấm nút dưới đây để hủy lượt đặt bàn này.</p>
        <button type="button" class="ot-btn ot-btn-outline mt-4" [disabled]="form.busy()" (click)="cancel()">Hủy đặt bàn</button>
      }
      @if (form.error(); as e) { <p class="ot-error mt-3">{{ e }}</p> }
    </div>
  `,
})
export class ManageReservation {
  private readonly api = inject(Api);
  private readonly token = takeToken(inject(DOCUMENT));
  protected readonly code = inject(ActivatedRoute).snapshot.queryParamMap.get('code') ?? '';
  // Mã nằm sau dấu # nên máy chủ không thấy; chỉ kết luận liên kết hỏng khi đã chạy ở trình duyệt
  protected readonly linkOk = signal(true);
  protected readonly form = new Submit();
  protected readonly result = signal<Schemas['PublicReservationDto'] | null>(null);

  constructor() {
    afterNextRender(() => this.linkOk.set(!!this.code && !!this.token));
  }

  protected async cancel(): Promise<void> {
    if (await askConfirm('Bạn chắc chắn muốn hủy đặt bàn này?')) {
      this.form.run(this.api.post<Schemas['PublicReservationDto']>(`reservations/${encodeURIComponent(this.code)}/cancel`, { token: this.token, reason: null }), (r) => this.result.set(r));
    }
  }
}
