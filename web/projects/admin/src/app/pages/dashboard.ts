import { Component, inject, signal } from '@angular/core';
import { CalendarCheck, CalendarRange, Inbox, ShoppingBag, Ticket, TrendingUp, Users, Wallet } from 'lucide-angular';

import { Api } from '../core/api';
import { reloadOnLive } from '../core/live';
import { ChartCard, ChartPoint, dayPoints, namedPoints, StatCard } from '../shared/chart';
import { errorText, ORDER_STATUS, RESERVATION_STATUS } from '../shared/ui';

@Component({
  selector: 'ot-dashboard',
  imports: [ChartCard, StatCard],
  template: `
    <h1>Bảng tin</h1>
    @if (error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (d(); as v) {
      @if (v.revenueToday) {
        <div class="a-row4">
          <ot-stat label="Doanh thu hôm nay" [value]="vnd(v.revenueToday.current)" [note]="delta(v.revenueToday)" [icon]="icons.money" [tone]="1" />
          <ot-stat label="Tuần này" [value]="vnd(v.revenueWeek.current)" [note]="delta(v.revenueWeek)" [icon]="icons.range" [tone]="2" />
          <ot-stat label="Tháng này" [value]="vnd(v.revenueMonth.current)" [note]="delta(v.revenueMonth)" [icon]="icons.trend" [tone]="4" />
          <ot-stat label="Khách mới tháng này" [value]="v.newCustomersThisMonth" [icon]="icons.users" [tone]="3" />
        </div>
      }
      <div class="a-row4">
        @if (v.ordersToday) { <ot-stat label="Đơn hôm nay" [value]="sum(v.ordersToday)" [icon]="icons.order" [tone]="1" /> }
        <ot-stat label="Đặt bàn hôm nay" [value]="sum(v.reservationsToday)" [icon]="icons.booking" [tone]="2" />
        @if (v.unreadContacts !== undefined) { <ot-stat label="Tin liên hệ chưa đọc" [value]="v.unreadContacts" [icon]="icons.inbox" [tone]="3" /> }
        @if (v.expiringVouchers) { <ot-stat label="Voucher sắp hết hạn" [value]="v.expiringVouchers.length" [icon]="icons.ticket" [tone]="5" /> }
      </div>
      @if (v.revenueByDay) {
        <div class="a-row84">
          <ot-chart title="Doanh thu theo ngày" kind="line" [points]="days(v.revenueByDay)" [money]="true" />
          <ot-chart title="Doanh thu theo danh mục" kind="doughnut" [points]="named(v.revenueByCategory)" [money]="true" />
        </div>
      }
      <div class="a-row66">
        <ot-chart title="Lượt đặt bàn theo ngày" kind="bar" [points]="days(v.reservationsByDay)" />
        <ot-chart title="Đặt bàn hôm nay theo trạng thái" kind="doughnut" [points]="byStatus(v.reservationsToday, rs)" />
      </div>
      @if (v.ordersToday) {
        <div class="a-row66">
          <ot-chart title="Món bán chạy" kind="hbar" [points]="top(v.topItems)" />
          <ot-chart title="Đơn hôm nay theo trạng thái" kind="doughnut" [points]="byStatus(v.ordersToday, os)" />
        </div>
      }
    }
  `,
})
export class Dashboard {
  protected readonly rs = RESERVATION_STATUS;
  protected readonly os = ORDER_STATUS;
  protected readonly d = signal<any>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly days = dayPoints;
  protected readonly named = namedPoints;
  protected readonly icons = { money: Wallet, range: CalendarRange, trend: TrendingUp, users: Users, order: ShoppingBag, booking: CalendarCheck, inbox: Inbox, ticket: Ticket };
  private readonly api = inject(Api);

  protected vnd(value: number): string {
    return `${new Intl.NumberFormat('vi-VN').format(value)}\u00a0₫`;
  }

  protected sum(rows: { count: number }[] | null | undefined): number {
    return (rows ?? []).reduce((total, r) => total + r.count, 0);
  }

  protected delta(period: { current: number; previous: number }): string | null {
    if (!period.previous) {
      return null;
    }

    const percent = Math.round(((period.current - period.previous) / period.previous) * 100);
    return `${percent >= 0 ? '+' : ''}${percent}% so với kỳ trước`;
  }

  protected byStatus(rows: { status: string; count: number }[] | null | undefined, labels: Record<string, string>): ChartPoint[] {
    return (rows ?? []).map((r) => ({ label: labels[r.status] ?? r.status, value: r.count }));
  }

  protected top(rows: { name: string; quantity: number }[] | null | undefined): ChartPoint[] {
    return (rows ?? []).map((r) => ({ label: r.name, value: r.quantity }));
  }

  constructor() {
    this.load();
    reloadOnLive(() => this.load());
  }

  private load(): void {
    this.api.get<any>('dashboard').subscribe({ next: (v) => this.d.set(v), error: (e: unknown) => this.error.set(errorText(e)) });
  }
}
