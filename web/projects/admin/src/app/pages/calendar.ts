import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';

import { Api } from '../core/api';
import { reloadOnLive } from '../core/live';
import { Latest, RESERVATION_STATUS, today, VnDatePipe } from '../shared/ui';

const WEEK = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

export interface MonthCell {
  date: string;
  day: number;
}

// Lưới tháng bắt đầu từ thứ Hai; chỉ làm việc với chuỗi ngày nên không lệch múi giờ
export function monthCells(month: string): (MonthCell | null)[] {
  const [y, m] = month.split('-').map(Number);
  const first = new Date(Date.UTC(y, m - 1, 1)).getUTCDay();
  const count = new Date(Date.UTC(y, m, 0)).getUTCDate();
  const blanks = Array.from({ length: (first + 6) % 7 }, () => null);
  return [...blanks, ...Array.from({ length: count }, (_, i) => ({ date: `${month}-${String(i + 1).padStart(2, '0')}`, day: i + 1 }))];
}

export function shiftMonth(month: string, delta: number): string {
  const [y, m] = month.split('-').map(Number);
  const d = new Date(Date.UTC(y, m - 1 + delta, 1));
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`;
}

@Component({
  selector: 'ot-calendar',
  imports: [RouterLink, VnDatePipe],
  template: `
    <div class="a-head">
      <h1>Lịch đặt bàn</h1>
      <div class="a-btns items-center">
        <button type="button" class="ot-chip" aria-label="Tháng trước" (click)="go(-1)">‹</button>
        <b class="w-24 text-center">{{ title() }}</b>
        <button type="button" class="ot-chip" aria-label="Tháng sau" (click)="go(1)">›</button>
        <button type="button" class="ot-chip" (click)="thisMonth()">Hôm nay</button>
      </div>
    </div>
    @if (loader.error() || dayLoader.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    <div class="a-row84">
      <section class="a-card a-card-b">
        <div class="a-cal">
          @for (d of week; track d) { <div class="a-cal-head">{{ d }}</div> }
          @for (c of cells(); track $index) {
            @if (c) {
              <button type="button" class="a-cal-day" [class.a-cal-on]="selected() === c.date" [class.a-cal-today]="c.date === now" (click)="select(c.date)">
                <span>{{ c.day }}</span>
                @if (load(c.date); as l) { <small class="a-badge" [class.a-badge-good]="l.tone === 'good'" [class.a-badge-warn]="l.tone === 'warn'" [class.a-badge-bad]="l.tone === 'bad'">{{ l.booked }}/{{ l.max }}</small> }
              </button>
            } @else { <div></div> }
          }
        </div>
        <p class="mt-3 text-sm text-muted">Mỗi ô là số lượt đã đặt trên tổng lượt nhận của ngày. Xanh: còn nhiều, vàng: từ 70%, đỏ: đã đầy.</p>
      </section>
      <section class="a-card">
        <header class="a-card-h">{{ selected() | vnDate }}</header>
        <div class="a-card-b">
          @for (s of slotsOf(); track s.slotId) { <div class="a-line text-sm"><span>Ca {{ s.start.slice(0, 5) }}–{{ s.end.slice(0, 5) }}</span><span class="tabular-nums">{{ s.bookedCount }}/{{ s.maxReservations }} lượt · {{ s.bookedGuests }}@if (s.maxGuests) {/{{ s.maxGuests }}} khách</span></div> } @empty { <p class="text-muted">Ngày này không có ca.</p> }
          <h2 class="mt-4 mb-1">Lượt đặt</h2>
          @for (r of list(); track r.id) {
            <a class="a-line" [routerLink]="['/reservations', r.id]"><span>{{ r.time.slice(0, 5) }} · {{ r.guestName }}<small class="block text-muted">{{ r.adultCount + r.childCount }} khách</small></span><span class="a-badge">{{ labels[r.status] }}</span></a>
          } @empty { <p class="text-muted">Chưa có lượt đặt.</p> }
        </div>
      </section>
    </div>
  `,
})
export class Calendar {
  private readonly api = inject(Api);
  protected readonly week = WEEK;
  protected readonly labels = RESERVATION_STATUS;
  protected readonly now = today();
  protected readonly loader = new Latest();
  protected readonly dayLoader = new Latest();
  protected readonly month = signal(this.now.slice(0, 7));
  protected readonly selected = signal(this.now);
  protected readonly days = signal<Schemas['CalendarDayDto'][]>([]);
  protected readonly list = signal<Schemas['AdminReservationDto'][]>([]);
  protected readonly cells = computed(() => monthCells(this.month()));
  protected readonly title = computed(() => `${this.month().slice(5)}/${this.month().slice(0, 4)}`);
  protected readonly slotsOf = computed(() => this.days().find((d) => d.date === this.selected())?.slots ?? []);

  constructor() {
    this.reload();
    reloadOnLive(() => this.reload());
  }

  protected load(date: string): { booked: number; max: number; tone: string } | null {
    const slots = this.days().find((d) => d.date === date)?.slots;
    if (!slots?.length) {
      return null;
    }

    const booked = slots.reduce((sum, s) => sum + s.bookedCount, 0);
    const max = slots.reduce((sum, s) => sum + s.maxReservations, 0);
    return { booked, max, tone: booked >= max ? 'bad' : booked / max >= 0.7 ? 'warn' : 'good' };
  }

  protected go(delta: number): void {
    this.month.update((m) => shiftMonth(m, delta));
    this.selected.set(`${this.month()}-01`);
    this.reload();
  }

  protected thisMonth(): void {
    this.month.set(this.now.slice(0, 7));
    this.selected.set(this.now);
    this.reload();
  }

  protected select(date: string): void {
    this.selected.set(date);
    this.loadDay();
  }

  private reload(): void {
    const cells = monthCells(this.month()).filter((c) => c !== null);
    this.loader.run(this.api.get<Schemas['CalendarDayDto'][]>('reservations/calendar', { from: cells[0].date, to: cells.at(-1)!.date }), (d) => this.days.set(d));
    this.loadDay();
  }

  private loadDay(): void {
    const date = this.selected();
    this.dayLoader.run(this.api.raw<Schemas['AdminReservationDto'][]>('reservations', { from: date, to: date, page: 1, pageSize: 100 }), (r) => this.list.set(r.data));
  }
}
