import { Component, input } from '@angular/core';
import { Schemas } from '@ot/api';

import { ORDER_STATUS, RESERVATION_STATUS, VnDatePipe } from './format';
import { VndPipe } from './vnd.pipe';

@Component({
  selector: 'ot-order-view',
  imports: [VndPipe, VnDatePipe],
  template: `
    <div class="ot-card p-4">
      <p class="text-sm text-muted">Mã đơn</p>
      <p class="text-xl font-medium">{{ order().code }}</p>
      <p class="mt-2"><b>{{ statuses[order().status] }}</b> · {{ order().paymentMethod === 'cash' ? 'Tiền mặt' : 'Chuyển khoản' }} · {{ order().paymentStatus === 'paid' ? 'Đã thanh toán' : 'Chưa thanh toán' }}</p>
      <p class="mt-1">Lấy lúc {{ order().pickupTime.slice(0, 5) }}, {{ order().pickupDate | vnDate }}</p>
      <ul class="mt-3 border-t border-line">
        @for (line of order().items; track $index) {
          <li class="flex justify-between gap-3 border-b border-line py-2">
            <span>{{ line.quantity }} × {{ line.name }}
              @for (o of line.options; track $index) { <small class="block text-muted">{{ o.groupName }}: {{ o.optionName }}</small> }
            </span>
            <span class="whitespace-nowrap">{{ line.lineTotal | vnd }}</span>
          </li>
        }
      </ul>
      <p class="mt-2 flex justify-between"><span>Tạm tính</span><span>{{ order().subTotal | vnd }}</span></p>
      @if (order().memberDiscount > 0) { <p class="flex justify-between"><span>Giảm hạng thành viên</span><span>-{{ order().memberDiscount | vnd }}</span></p> }
      @if (order().voucherDiscount > 0) { <p class="flex justify-between"><span>Voucher</span><span>-{{ order().voucherDiscount | vnd }}</span></p> }
      <p class="mt-1 flex justify-between font-semibold"><span>Tổng cộng</span><span class="text-primary">{{ order().totalAmount | vnd }}</span></p>
      @if (order().note) { <p class="mt-2 text-sm text-muted">Ghi chú: {{ order().note }}</p> }
    </div>
  `,
})
export class OrderView {
  readonly order = input.required<Schemas['PublicOrderDto']>();
  protected readonly statuses = ORDER_STATUS;
}

@Component({
  selector: 'ot-reservation-view',
  imports: [VnDatePipe],
  template: `
    <div class="ot-card p-4">
      <p class="text-sm text-muted">Mã đặt bàn</p>
      <p class="text-xl font-medium">{{ reservation().code }}</p>
      <p class="mt-2"><b>{{ statuses[reservation().status] }}</b></p>
      <p class="mt-1">{{ reservation().time.slice(0, 5) }}, {{ reservation().date | vnDate }} · {{ reservation().adultCount }} người lớn, {{ reservation().childCount }} trẻ em</p>
      <p class="mt-1">{{ reservation().guestName }} · {{ reservation().maskedPhone }}</p>
      @if (reservation().specialRequest) { <p class="mt-2 text-sm text-muted">Yêu cầu: {{ reservation().specialRequest }}</p> }
    </div>
  `,
})
export class ReservationView {
  readonly reservation = input.required<Schemas['PublicReservationDto']>();
  protected readonly statuses = RESERVATION_STATUS;
}
