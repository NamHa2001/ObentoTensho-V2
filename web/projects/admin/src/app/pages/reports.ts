import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BadgeDollarSign, ClipboardList, TrendingUp, Wallet } from 'lucide-angular';

import { Api, saveBlob } from '../core/api';
import { ChartCard, ChartPoint, dayPoints, namedPoints, StatCard } from '../shared/chart';
import { Latest, Submit, VndPipe, today } from '../shared/ui';

const REPORTS = [{ key: 'revenue', label: 'Doanh thu' }, { key: 'reservations', label: 'Đặt bàn' }, { key: 'orders', label: 'Đơn hàng' }, { key: 'customers', label: 'Khách hàng' }, { key: 'top-dishes', label: 'Món bán chạy' }];

@Component({
  selector: 'ot-reports',
  imports: [FormsModule, ChartCard, StatCard, VndPipe],
  template: `
    <h1>Báo cáo</h1>
    <div class="ot-scroll-row mb-4" style="margin-inline: 0; padding-inline: 0">@for (r of reports; track r.key) { <button type="button" class="ot-chip" [class.ot-chip-active]="report() === r.key" (click)="select(r.key)">{{ r.label }}</button> }</div>
    <div class="a-filters">
      <input class="ot-input" name="f" type="date" aria-label="Từ ngày" [(ngModel)]="from" (change)="load()" />
      <input class="ot-input" name="t" type="date" aria-label="Đến ngày" [(ngModel)]="to" (change)="load()" />
      <button type="button" class="ot-btn ot-btn-outline" (click)="save('xlsx')">Xuất Excel</button>
      <button type="button" class="ot-btn ot-btn-outline" (click)="save('pdf')">Xuất PDF</button>
    </div>
    @if (form.error() || loader.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (d(); as v) {
      @switch (report()) {
        @case ('revenue') {
          <div class="a-row4">
            <ot-stat label="Doanh thu" [value]="vnd(v.net)" [icon]="icons.wallet" [tone]="1" />
            <ot-stat label="Tiền thu" [value]="vnd(v.paid)" [icon]="icons.trend" [tone]="4" />
            <ot-stat label="Hoàn tiền" [value]="vnd(v.refunds)" [icon]="icons.order" [tone]="3" />
            <ot-stat label="Kỳ trước" [value]="vnd(v.previousNet)" [icon]="icons.dollar" [tone]="2" />
          </div>
          <div class="a-row84">
            <ot-chart title="Doanh thu theo kỳ" kind="line" [points]="series(v.series)" [money]="true" />
            <ot-chart title="Theo danh mục" kind="doughnut" [points]="named(v.byCategory)" [money]="true" />
          </div>
        }
        @case ('reservations') {
          <div class="a-row4">
            <ot-stat label="Tổng lượt đặt" [value]="v.total" [icon]="icons.order" [tone]="2" />
            <ot-stat label="Đã đến" [value]="v.arrived" [note]="v.arrivedPercent + '%'" [icon]="icons.trend" [tone]="4" />
            <ot-stat label="Đã hủy" [value]="v.cancelled" [note]="v.cancelledPercent + '%'" [icon]="icons.dollar" [tone]="3" />
            <ot-stat label="Không đến" [value]="v.noShow" [note]="v.noShowPercent + '%'" [icon]="icons.wallet" [tone]="1" />
          </div>
          <div class="a-row66">
            <ot-chart title="Lượt đặt theo giờ" kind="bar" [points]="named(v.byHour)" />
            <ot-chart title="Theo khu vực" kind="doughnut" [points]="named(v.byArea)" />
          </div>
        }
        @case ('orders') {
          <div class="a-row4">
            <ot-stat label="Đơn đã tạo" [value]="v.created" [icon]="icons.order" [tone]="2" />
            <ot-stat label="Hoàn thành" [value]="v.completed" [icon]="icons.trend" [tone]="4" />
            <ot-stat label="Đã hủy" [value]="v.cancelled" [icon]="icons.dollar" [tone]="3" />
            <ot-stat label="Giá trị trung bình" [value]="vnd(v.averageValue)" [icon]="icons.wallet" [tone]="1" />
          </div>
          <div class="a-row66">
            <ot-chart title="Đơn theo giờ" kind="bar" [points]="named(v.byHour)" />
            <ot-chart title="Món bán chạy" kind="hbar" [points]="dishes(v.topDishes)" />
          </div>
        }
        @case ('customers') {
          <div class="a-row4">
            <ot-stat label="Khách mới" [value]="v.newTotal" [icon]="icons.order" [tone]="2" />
            <ot-stat label="Tỷ lệ quay lại" [value]="v.retentionPercent + '%'" [icon]="icons.trend" [tone]="4" />
          </div>
          <div class="a-row84">
            <ot-chart title="Khách mới theo ngày" kind="line" [points]="days(v.newByDay)" />
            <ot-chart title="Theo hạng" kind="doughnut" [points]="tiers(v.tiers)" />
          </div>
          <section class="a-card mb-6">
            <header class="a-card-h">Khách mua nhiều</header>
            <div class="a-card-b">@for (c of v.topCustomers; track c.id) { <div class="a-line"><span>{{ c.fullName }} · {{ c.orders }} đơn</span><b class="tabular-nums">{{ c.spent | vnd }}</b></div> } @empty { <p class="text-muted">Chưa có dữ liệu.</p> }</div>
          </section>
        }
        @default { <div class="a-row66"><ot-chart title="Số lượng bán" kind="hbar" [points]="dishes(list(v))" /><ot-chart title="Doanh thu" kind="doughnut" [points]="dishMoney(list(v))" [money]="true" /></div> }
      }
    }
  `,
})
export class Reports {
  private readonly api = inject(Api);
  protected readonly reports = REPORTS;
  protected readonly days = dayPoints;
  protected readonly named = namedPoints;
  protected readonly report = signal('revenue');
  protected readonly d = signal<any>(null);
  protected readonly form = new Submit();
  protected readonly loader = new Latest();
  protected from = today().slice(0, 8) + '01';
  protected to = today();

  constructor() {
    this.load();
  }

  protected select(key: string): void {
    this.report.set(key);
    this.d.set(null);
    this.load();
  }

  protected readonly icons = { wallet: Wallet, trend: TrendingUp, order: ClipboardList, dollar: BadgeDollarSign };

  protected vnd(value: number): string {
    return `${new Intl.NumberFormat('vi-VN').format(value)}\u00a0₫`;
  }

  protected dishes(rows: any[]): ChartPoint[] {
    return (rows ?? []).map((r) => ({ label: r.name, value: r.quantity }));
  }

  protected dishMoney(rows: any[]): ChartPoint[] {
    return (rows ?? []).map((r) => ({ label: r.name, value: r.revenue }));
  }

  protected tiers(rows: any[]): ChartPoint[] {
    return (rows ?? []).map((r) => ({ label: r.tier, value: r.count }));
  }

  protected series(rows: any[]): ChartPoint[] {
    return rows.map((r) => ({ label: r.period, value: r.net }));
  }

  protected list(v: any): any[] {
    return Array.isArray(v) ? v : (v.items ?? []);
  }

  protected load(): void {
    this.d.set(null);
    this.loader.run(this.api.get<any>(`reports/${this.report()}`, { from: this.from, to: this.to, groupBy: this.report() === 'revenue' ? 'day' : null, limit: this.report() === 'revenue' || this.report() === 'reservations' ? null : 20 }), (v) => this.d.set(v));
  }

  protected save(format: string): void {
    this.form.run(this.api.download(`reports/${this.report()}/export`, { from: this.from, to: this.to, format }), (r) => saveBlob(r, `bao-cao.${format}`));
  }
}
