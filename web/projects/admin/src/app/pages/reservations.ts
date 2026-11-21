import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';

import { Api } from '../core/api';
import { reloadOnLive } from '../core/live';
import { Latest, RESERVATION_STATUS, Submit, VnDatePipe, today } from '../shared/ui';
import { askText } from '../../../../customer/src/app/core/dialog';

const RESERVATION_NEXT: Record<string, string[]> = { pending: ['confirmed', 'cancelled'], confirmed: ['checked_in', 'no_show', 'cancelled'], checked_in: ['completed'], no_show: ['checked_in'] };

type ReservationRow = Schemas['AdminReservationDto'];
type ReservationDetail = Schemas['AdminReservationDetailDto'];

@Component({
  selector: 'ot-reservations',
  imports: [FormsModule, RouterLink, VnDatePipe],
  template: `
    <div class="a-head"><h1>Đặt bàn</h1><a class="ot-btn ot-btn-primary" routerLink="/reservations/new">Đặt hộ khách</a></div>
    <div class="a-filters">
      <input class="ot-input" name="q" placeholder="Tìm mã, tên" [(ngModel)]="q" (change)="reload()" />
      <select class="ot-input" name="s" [(ngModel)]="status" (change)="reload()"><option value="">Mọi trạng thái</option>@for (s of statusKeys; track s) { <option [value]="s">{{ labels[s] }}</option> }</select>
      <input class="ot-input" name="from" type="date" [(ngModel)]="from" (change)="reload()" />
      <input class="ot-input" name="to" type="date" [(ngModel)]="to" (change)="reload()" />
    </div>
    @if (list.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    <div class="a-table"><table class="w-full text-sm"><thead><tr class="bg-surface-2 text-left"><th class="p-2">Mã</th><th class="p-2">Khách</th><th class="p-2">Giờ</th><th class="p-2">Số người</th><th class="p-2">Trạng thái</th></tr></thead>
      <tbody>@for (r of rows(); track r.id) {
        <tr class="border-b border-line"><td class="p-2"><a class="text-primary" [routerLink]="['/reservations', r.id]">{{ r.code }}</a></td><td class="p-2">{{ r.guestName }}<small class="block text-muted">{{ r.maskedPhone }}</small></td><td class="p-2 whitespace-nowrap">{{ r.time.slice(0, 5) }} {{ r.date | vnDate }}</td><td class="p-2">{{ r.adultCount + r.childCount }}</td><td class="p-2">{{ labels[r.status] }}</td></tr>
      } @empty { <tr><td colspan="5" class="p-4 text-muted">Không có đặt bàn.</td></tr> }</tbody></table></div>
    @if (rows().length < total()) { <button type="button" class="ot-btn ot-btn-outline mt-3" (click)="more()">Xem thêm</button> }
  `,
})
export class Reservations {
  private readonly api = inject(Api);
  protected readonly labels = RESERVATION_STATUS;
  protected readonly statusKeys = Object.keys(RESERVATION_STATUS);
  protected readonly rows = signal<ReservationRow[]>([]);
  protected readonly total = signal(0);
  protected readonly list = new Latest();
  protected q = '';
  protected status = '';
  protected from = '';
  protected to = '';
  private page = 1;

  constructor() {
    this.reload();
    reloadOnLive(() => this.reload());
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
    this.list.run(this.api.raw<ReservationRow[]>('reservations', { q: this.q, status: this.status, from: this.from, to: this.to, page: this.page, pageSize: 30 }), (r) => {
      this.rows.update((x) => [...x, ...r.data]);
      this.total.set(r.meta?.total ?? 0);
    });
  }
}

@Component({
  selector: 'ot-reservation',
  imports: [FormsModule, VnDatePipe],
  template: `
    @if (form.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (d(); as v) {
      <div class="a-head"><h1>Đặt bàn {{ v.reservation.code }}</h1><span class="a-badge">{{ labels[v.reservation.status] }}</span></div>
      <div class="a-row66">
        <div class="a-col">
          <section class="a-card">
            <header class="a-card-h">Thông tin</header>
            <div class="a-card-b"><dl class="a-kv">
              <dt>Thời gian</dt><dd>{{ v.reservation.time.slice(0, 5) }} {{ v.reservation.date | vnDate }}</dd>
              <dt>Số người</dt><dd>{{ v.reservation.adultCount }} lớn, {{ v.reservation.childCount }} trẻ em</dd>
              <dt>Khách</dt><dd>{{ v.reservation.guestName }}</dd>
              <dt>Điện thoại</dt><dd>{{ contact()?.phone ?? v.reservation.maskedPhone }}</dd>
              <dt>Email</dt><dd>{{ contact()?.email ?? v.reservation.maskedEmail }}</dd>
              <dt>Xác minh</dt><dd>{{ v.reservation.contactConfirmed ? 'Đã xác minh' : 'Chưa xác minh' }} · đã gọi {{ v.reservation.callAttempts }} lần</dd>
              @if (v.reservation.specialRequest) { <dt>Yêu cầu</dt><dd>{{ v.reservation.specialRequest }}</dd> }
            </dl>
            @if (v.reservation.noShowFlag) { <p class="ot-error mt-3">SĐT này từng không đến.</p> }</div>
          </section>
          <section class="a-card">
            <header class="a-card-h">Lịch sử</header>
            <div class="a-card-b">@for (h of v.history; track $index) { <div class="a-line text-sm"><span>{{ labels[h.toStatus] }} @if (h.reason) { ({{ h.reason }}) }</span><span class="whitespace-nowrap text-muted">{{ h.changedAtUtc | vnDate: true }}</span></div> } @empty { <p class="text-muted">Chưa có.</p> }</div>
          </section>
        </div>
        <div class="a-col">
          <section class="a-card">
            <header class="a-card-h">Xử lý</header>
            <div class="a-card-b a-btns">
              @if (!contact()) { <button type="button" class="ot-chip" (click)="reveal(v)">Hiện SĐT</button> }
              @if (v.reservation.status === 'pending') {
                <button type="button" class="ot-chip" (click)="call(v)">Ghi cuộc gọi</button>
                @if (!v.reservation.contactConfirmed) { <button type="button" class="ot-chip" (click)="confirmContact(v)">Đã gọi xác minh</button> }
              }
              @for (s of next(v.reservation.status); track s) { <button type="button" class="ot-chip" (click)="move(v, s)">{{ labels[s] }}</button> }
            </div>
          </section>
          <section class="a-card">
            <header class="a-card-h">Bàn và ghi chú</header>
            <div class="a-card-b grid gap-3">
              <label>Gán bàn<select class="ot-input" name="t" [ngModel]="v.reservation.tableId" (ngModelChange)="assign(v, $event)"><option [ngValue]="null">Chưa gán</option>@for (t of tables(); track t.id) { <option [ngValue]="t.id">{{ t.name }} ({{ t.minCapacity }}–{{ t.maxCapacity }})</option> }</select></label>
              <label>Ghi chú nội bộ<textarea class="ot-input" name="n" rows="3" [(ngModel)]="note"></textarea></label>
              <div><button type="button" class="ot-btn ot-btn-outline" (click)="saveNote(v)">Lưu ghi chú</button></div>
            </div>
          </section>
        </div>
      </div>
    }
  `,
})
export class Reservation {
  private readonly api = inject(Api);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  protected readonly labels = RESERVATION_STATUS;
  protected readonly d = signal<ReservationDetail | null>(null);
  protected readonly contact = signal<Schemas['ReservationContactDto'] | null>(null);
  protected readonly tables = signal<Schemas['TableDto'][]>([]);
  protected readonly form = new Submit();
  protected note = '';

  constructor() {
    this.load();
    this.api.get<Schemas['TableDto'][]>('tables').subscribe({ next: (t) => this.tables.set(t), error: () => undefined });
  }

  private load(): void {
    this.form.run(this.api.get<ReservationDetail>(`reservations/${this.id}`), (v) => {
      this.d.set(v);
      this.note = v.reservation.internalNote ?? '';
    });
  }

  protected next(status: string): string[] {
    return RESERVATION_NEXT[status] ?? [];
  }

  protected reveal(v: ReservationDetail): void {
    this.form.run(this.api.post<Schemas['ReservationContactDto']>(`reservations/${v.reservation.id}/reveal-contact`), (c) => this.contact.set(c));
  }

  // Bấm Hủy trong hộp thoại (askText trả null) thì không ghi gì
  protected async call(v: ReservationDetail): Promise<void> {
    const note = await askText('Ghi chú cuộc gọi');
    if (note !== null) {
      this.form.run(this.api.post(`reservations/${v.reservation.id}/call-attempts`, { note }, v.reservation.version), () => this.load());
    }
  }

  protected async confirmContact(v: ReservationDetail): Promise<void> {
    const note = await askText('Ghi chú');
    if (note !== null) {
      this.form.run(this.api.post(`reservations/${v.reservation.id}/contact-confirmation`, { note }, v.reservation.version), () => this.load());
    }
  }

  protected async move(v: ReservationDetail, status: string): Promise<void> {
    let reason: string | null = null;
    if (status === 'cancelled') {
      reason = await askText('Lý do hủy');
      if (!reason) return;
    }

    this.form.run(this.api.patch(`reservations/${v.reservation.id}/status`, { expectedStatus: v.reservation.status, status, reason }, v.reservation.version), () => this.load());
  }

  protected assign(v: ReservationDetail, tableId: number | null): void {
    if (tableId !== null) {
      this.form.run(this.api.patch(`reservations/${v.reservation.id}/assign-table`, { tableId }, v.reservation.version), () => this.load());
    }
  }

  protected saveNote(v: ReservationDetail): void {
    this.form.run(this.api.patch(`reservations/${v.reservation.id}/note`, { note: this.note || null }, v.reservation.version), () => this.load());
  }
}

@Component({
  selector: 'ot-reservation-new',
  imports: [FormsModule],
  template: `
    <h1>Đặt bàn hộ khách</h1>
    <form class="ot-stack max-w-md" (ngSubmit)="save()">
      <label>Ngày<input class="ot-input" name="d" type="date" required [(ngModel)]="m.date" /></label>
      <label>Giờ<input class="ot-input" name="t" type="time" required [(ngModel)]="m.time" /></label>
      <label>Người lớn<input class="ot-input" name="a" type="number" min="1" required [(ngModel)]="m.adultCount" /></label>
      <label>Trẻ em<input class="ot-input" name="c" type="number" min="0" required [(ngModel)]="m.childCount" /></label>
      <label>Họ tên<input class="ot-input" name="n" required [(ngModel)]="m.guestName" /></label>
      <label>Số điện thoại<input class="ot-input" name="p" type="tel" required [(ngModel)]="m.guestPhone" /></label>
      <label>Email<input class="ot-input" name="e" type="email" [(ngModel)]="m.guestEmail" /></label>
      <label>Nguồn<select class="ot-input" name="s" [(ngModel)]="m.source"><option value="phone">Điện thoại</option><option value="walk_in">Khách tới quán</option></select></label>
      <label>Yêu cầu<textarea class="ot-input" name="r" rows="2" [(ngModel)]="m.specialRequest"></textarea></label>
      <label>Lý do vượt sức chứa (nếu có)<input class="ot-input" name="o" [(ngModel)]="m.overrideCapacityReason" /></label>
      <label class="flex items-center gap-2"><input type="checkbox" name="i" [(ngModel)]="m.checkInNow" /> Khách đã đến</label>
      @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
      <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Tạo</button>
    </form>
  `,
})
export class ReservationNew {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected readonly form = new Submit();
  protected m: Schemas['StaffCreateReservationRequest'] = { date: today(), time: '', adultCount: 2, childCount: 0, guestName: '', guestPhone: '', guestEmail: '', specialRequest: '', internalNote: null, source: 'phone', checkInNow: false, overrideCapacityReason: '' };

  protected save(): void {
    const body = { ...this.m, guestEmail: this.m.guestEmail || null, specialRequest: this.m.specialRequest || null, overrideCapacityReason: this.m.overrideCapacityReason || null };
    this.form.run(this.api.post<ReservationRow>('reservations', body), (r) => void this.router.navigate(['/reservations', r.id]));
  }
}
