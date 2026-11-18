import { Component, inject, signal } from '@angular/core';
import { Schemas } from '@ot/api';
import { CalendarCheck, CalendarRange, Inbox, ShoppingBag, Ticket, TrendingUp, Users, Wallet } from 'lucide-angular';

import { Api } from '../core/api';
import { reloadOnLive } from '../core/live';
import { ChartCard, ChartPoint, dayPoints, namedPoints, StatCard } from '../shared/chart';
import { errorText, ORDER_STATUS, RESERVATION_STATUS } from '../shared/ui';

type DashboardData = Schemas['ManagerDashboardDto'] | Schemas['ReceptionDashboardDto'];

@Component({
  selector: 'ot-dashboard',
  imports: [ChartCard, StatCard],
  template: `
    <h1>Bảng tin</h1>
    @if (error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (d(); as v) {
      @let m = manager(v);
      @if (m) {
        <div class="a-row4">
          <ot-stat label="Doanh thu hôm nay" [value]="vnd(m.revenueToday.current)" [note]="delta(m.revenueToday)" [icon]="icons.money" [tone]="1" />
          <ot-stat label="Tuần này" [value]="vnd(m.revenueWeek.current)" [note]="delta(m.revenueWeek)" [icon]="icons.range" [tone]="2" />
          <ot-stat label="Tháng này" [value]="vnd(m.revenueMonth.current)" [note]="delta(m.revenueMonth)" [icon]="icons.trend" [tone]="4" />
          <ot-stat label="Khách mới tháng này" [value]="m.newCustomersThisMonth" [icon]="icons.users" [tone]="3" />
        </div>
      }
      <div class="a-row4">
        @if (m) { <ot-stat label="Đơn hôm nay" [value]="sum(m.ordersToday)" [icon]="icons.order" [tone]="1" /> }
        <ot-stat label="Đặt bàn hôm nay" [value]="sum(v.reservationsToday)" [icon]="icons.booking" [tone]="2" />
        @if (m) {
          <ot-stat label="Tin liên hệ chưa đọc" [value]="m.unreadContacts" [icon]="icons.inbox" [tone]="3" />
          <ot-stat label="Voucher sắp hết hạn" [value]="m.expiringVouchers.length" [icon]="icons.ticket" [tone]="5" />
        }
      </div>
      @if (m) {
        <div class="a-row84">
          <ot-chart title="Doanh thu theo ngày" kind="line" [points]="days(m.revenueByDay)" [money]="true" />
          <ot-chart title="Doanh thu theo danh mục" kind="doughnut" [points]="named(m.revenueByCategory)" [money]="true" />
        </div>
      }
      <div class="a-row66">
        <ot-chart title="Lượt đặt bàn theo ngày" kind="bar" [points]="days(v.reservationsByDay)" />
        <ot-chart title="Đặt bàn hôm nay theo trạng thái" kind="doughnut" [points]="byStatus(v.reservationsToday, rs)" />
      </div>
      @if (m) {
        <div class="a-row66">
          <ot-chart title="Món bán chạy" kind="hbar" [points]="named(m.topItems)" />
          <ot-chart title="Đơn hôm nay theo trạng thái" kind="doughnut" [points]="byStatus(m.ordersToday, os)" />
        </div>
      }
    }
  `,
})
export class Dashboard {
  protected readonly rs = RESERVATION_STATUS;
  protected readonly os = ORDER_STATUS;
  protected readonly d = signal<DashboardData | null>(null);
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

  // Quản lý nhận thêm doanh thu, đơn, món bán chạy; lễ tân chỉ có phần đặt bàn
  protected manager(v: DashboardData): Schemas['ManagerDashboardDto'] | null {
    return 'revenueToday' in v ? v : null;
  }

  constructor() {
    this.load();
    reloadOnLive(() => this.load());
  }

  private load(): void {
    this.api.get<DashboardData>('dashboard').subscribe({ next: (v) => this.d.set(v), error: (e: unknown) => this.error.set(errorText(e)) });
  }
}
