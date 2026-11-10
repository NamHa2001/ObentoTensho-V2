import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { switchMap } from 'rxjs';

import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { LiveHub } from '../../core/live';
import { Turnstile } from '../../core/turnstile';
import { errorCode, Submit } from '../../shared/form';
import { addDays, localPhone, today, VnDatePipe } from '../../shared/format';
import { ReservationView } from '../../shared/views';

const MAX_AUTO_DAYS = 14;

@Component({
  selector: 'ot-reservation',
  imports: [FormsModule, RouterLink, ReservationView, VnDatePipe],
  template: `
    <div class="ot-page-split">
      <h1>Đặt bàn</h1>
      <p class="ot-page-sub">Chọn ngày, giờ và số khách. Nhà hàng sẽ gọi điện xác nhận lại với bạn.</p>
      @if (created(); as r) {
        <div class="ot-panel mt-5"><ot-reservation-view [reservation]="r" class="block" />
        <p class="mt-3">Nhà hàng sẽ gọi điện xác nhận lại giờ với bạn.</p>
        <a class="ot-btn ot-btn-outline mt-3" routerLink="/tai-khoan/dat-ban">Xem đặt bàn của tôi</a></div>
      } @else {
        <form class="ot-split" (ngSubmit)="submit()">
          <div class="grid gap-4">
            <section class="ot-panel">
              <h2>Chọn ngày và giờ</h2>
              <div class="ot-stack">
              <label>Ngày<input class="ot-input" name="date" type="date" required [min]="minDate" [(ngModel)]="model.date" (ngModelChange)="pickDate()" /></label>
              @if (availability(); as a) {
                @if (moved()) {
                  <p class="text-sm text-muted">Hôm nay đã hết giờ nhận đặt bàn, đã chuyển sang ngày gần nhất còn chỗ.</p>
                }
                @if (!a.open) {
                  <p class="ot-error">Quán nghỉ ngày này, vui lòng chọn ngày khác.</p>
                } @else if (!hasTimes(a)) {
                  <p class="ot-error">{{ model.date === minDate ? 'Hôm nay đã hết giờ nhận đặt bàn.' : 'Ngày này đã kín chỗ.' }} Vui lòng chọn ngày khác.</p>
                }
                @for (slot of a.slots; track slot.slotId) {
                  <fieldset>
                    <legend>Ca {{ slot.start.slice(0, 5) }} – {{ slot.end.slice(0, 5) }} · còn {{ slot.remainingReservations }} bàn</legend>
                    <div class="mt-2 flex flex-wrap gap-2">
                      @for (t of slot.times; track t) {
                        <button type="button" class="ot-chip" [class.ot-chip-active]="model.time === t" (click)="model.time = t">{{ t.slice(0, 5) }}</button>
                      }
                    </div>
                  </fieldset>
                }
              }
              </div>
            </section>
            <section class="ot-panel">
              <h2>Thông tin khách</h2>
              <div class="ot-form-grid mt-4">
              <label>Người lớn<input class="ot-input" name="adults" type="number" min="1" required [(ngModel)]="model.adultCount" /></label>
              <label>Trẻ em<input class="ot-input" name="children" type="number" min="0" required [(ngModel)]="model.childCount" /></label>
              <label class="ot-span-2">Họ tên<input class="ot-input" name="name" required [(ngModel)]="model.guestName" /></label>
              <label>Số điện thoại liên hệ<input class="ot-input" name="phone" type="tel" required [(ngModel)]="model.guestPhone" /></label>
              <label class="ot-span-2">Email<input class="ot-input" name="email" type="email" required [(ngModel)]="model.guestEmail" /></label>
              <label class="ot-span-2">Yêu cầu thêm<textarea class="ot-input" name="note" rows="3" [(ngModel)]="model.specialRequest"></textarea></label>
              </div>
            </section>
          </div>
          <aside class="ot-panel">
            <h2>Lượt đặt của bạn</h2>
            <div class="mt-3 divide-y divide-line">
              <p class="ot-sum"><span class="text-muted">Ngày</span><b>{{ model.date | vnDate }}</b></p>
              <p class="ot-sum"><span class="text-muted">Giờ</span><b>{{ model.time ? model.time.slice(0, 5) : 'Chưa chọn' }}</b></p>
              <p class="ot-sum"><span class="text-muted">Số khách</span><b>{{ model.adultCount }} người lớn · {{ model.childCount }} trẻ em</b></p>
            </div>
            @if (needOtp()) {
              <label>Mã 6 số đã gửi tới email<input class="ot-input" name="otp" inputmode="numeric" autocomplete="one-time-code" maxlength="6" [(ngModel)]="otp" /></label>
            }
            @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
            <button class="ot-btn ot-btn-primary mt-4 w-full" [disabled]="form.busy() || !model.time">Đặt bàn</button>
          </aside>
        </form>
      }
    </div>
  `,
})
export class Reservation {
  private readonly api = inject(Api);
  private readonly turnstile = inject(Turnstile);
  private hub: LiveHub | null = null;
  protected readonly minDate = today();
  protected readonly form = new Submit();
  protected readonly availability = signal<Schemas['AvailabilityDto'] | null>(null);
  protected readonly created = signal<Schemas['PublicReservationDto'] | null>(null);
  protected readonly needOtp = signal(false);
  protected readonly moved = signal(false);
  // Chỉ tự chuyển ngày ở lần mở trang; khách đã tự chọn ngày thì giữ nguyên
  private autoPick = true;
  private tries = 0;
  protected otp = '';
  protected model = { date: today(), time: '', adultCount: 2, childCount: 0, guestName: '', guestPhone: '', guestEmail: '', specialRequest: '' };

  constructor() {
    const profile = inject(Auth).profile();
    this.model.guestName = profile?.fullName ?? '';
    this.model.guestPhone = localPhone(profile?.phoneNumber);
    this.model.guestEmail = profile?.email ?? '';
    this.loadSlots();
    inject(DestroyRef).onDestroy(() => this.hub?.stop());
  }

  protected pickDate(): void {
    this.autoPick = false;
    this.moved.set(false);
    this.loadSlots();
  }

  protected hasTimes(a: Schemas['AvailabilityDto']): boolean {
    return a.slots.some((s) => s.times.length > 0);
  }

  protected loadSlots(): void {
    this.model.time = '';
    this.refreshSlots();
    this.hub?.stop();
    const date = this.model.date;
    this.hub = new LiveHub({
      url: () => Promise.resolve('/hubs/v1/public'),
      events: ['slot.availabilityChanged'],
      onEvent: () => this.refreshSlots(),
      onOpen: (c, reconnected) => {
        if (reconnected) {
          this.refreshSlots();
        }

        return c.invoke('WatchAvailability', date);
      },
    });
    void this.hub.start();
  }

  private refreshSlots(): void {
    const date = this.model.date;
    this.api.getOr<Schemas['AvailabilityDto'] | null>('reservations/availability', null, { date }).subscribe((a) => {
      // Khách đã đổi ngày trong lúc chờ: kết quả này của ngày cũ
      if (date !== this.model.date) {
        return;
      }

      if (this.autoPick && a && !this.hasTimes(a) && this.tries < MAX_AUTO_DAYS) {
        this.tries += 1;
        this.moved.set(true);
        this.model.date = addDays(this.model.date, 1);
        this.loadSlots();
        return;
      }

      this.autoPick = false;
      this.availability.set(a);
    });
  }

  protected submit(): void {
    const body = { ...this.model, specialRequest: this.model.specialRequest || null, lang: 'vi', otpCode: this.needOtp() ? this.otp : null };
    this.form.run(
      this.api.post<Schemas['PublicReservationDto']>('reservations', body),
      (r) => this.created.set(r),
      (e) => {
        if (errorCode(e) !== 'OTP_REQUIRED') {
          return false;
        }

        this.needOtp.set(true);
        const otpBody = { purpose: 'reservation_contact', email: this.model.guestEmail, lang: 'vi' };
        this.turnstile.headers().pipe(switchMap((h) => this.api.post('otp/send', otpBody, h))).subscribe({ error: () => undefined });
        this.form.error.set('Cần xác minh: vui lòng nhập mã vừa gửi tới email rồi bấm Đặt bàn.');
        return true;
      },
    );
  }
}
