import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Schemas } from '@ot/api';

import { Api, saveBlob } from '../core/api';
import { ChartCard, ChartPoint, dayPoints, namedPoints, StatCard } from '../shared/chart';
import { Latest, Submit, VndPipe, today } from '../shared/ui';
import { PaymentGroups } from './payment-groups';

const CHANNELS: Partial<Record<string, string>> = { pickup: 'Đặt online đến lấy', dine_in: 'Tại bàn', delivery: 'Giao hàng', counter: 'Mua tại quầy', platform: 'Đơn từ app' };

// Mỗi loại báo cáo trả dữ liệu khác nhau; giữ kèm loại để mẫu biết chắc đang hiển thị kiểu nào
type ReportData =
  | { kind: 'revenue'; data: Schemas['RevenueReportDto'] }
  | { kind: 'reservations'; data: Schemas['ReservationReportDto'] }
  | { kind: 'orders'; data: Schemas['OrderReportDto'] }
  | { kind: 'customers'; data: Schemas['CustomerReportDto'] }
  | { kind: 'top-dishes'; data: Schemas['TopDishDto'][] }
  | { kind: 'payment-groups'; data: Schemas['PaymentGroupReportDto'] };
type ReportKind = ReportData['kind'];

const REPORTS: { key: ReportKind; label: string }[] = [{ key: 'revenue', label: 'Doanh thu' }, { key: 'reservations', label: 'Đặt bàn' }, { key: 'orders', label: 'Đơn hàng' }, { key: 'customers', label: 'Khách hàng' }, { key: 'top-dishes', label: 'Món bán chạy' }, { key: 'payment-groups', label: 'Nhóm thanh toán' }];

// Kỳ báo cáo từ API dạng yyyy-MM-dd hoặc yyyy-MM, hiện theo kiểu ngày Việt Nam như các chỗ khác
function periodLabel(period: string): string {
  const [y, m, d] = period.split('-');
  return d ? `${d}/${m}` : m ? `${m}/${y}` : period;
}

@Component({
  selector: 'ot-reports',
  imports: [FormsModule, ChartCard, StatCard, VndPipe, PaymentGroups],
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
    @if (d(); as r) {
      @switch (r.kind) {
        @case ('revenue') {
          @let v = r.data;
          <div class="a-row4">
            <ot-stat label="Doanh thu" [value]="vnd(v.net)" [tone]="1" />
            <ot-stat label="Tiền thu" [value]="vnd(v.paid)" [tone]="4" />
            <ot-stat label="Hoàn tiền" [value]="vnd(v.refunds)" [tone]="3" />
            <ot-stat label="Kỳ trước" [value]="vnd(v.previousNet)" [tone]="2" />
          </div>
          <div class="a-row84">
            <ot-chart title="Doanh thu theo kỳ" kind="line" [points]="series(v.series)" [money]="true" />
            <ot-chart title="Theo danh mục" kind="doughnut" [points]="named(v.byCategory)" [money]="true" />
          </div>
          @if (v.byChannel?.length) {
            <section class="a-card mt-4">
              <header class="a-card-h"><span>Theo kênh bán</span><button type="button" class="a-link" (click)="saveChannels()">Xuất Excel theo kênh</button></header>
              <div class="a-table"><table class="w-full text-sm">
                <thead><tr class="bg-surface-2 text-left"><th class="p-2">Kênh</th><th class="p-2 text-right">Số đơn</th><th class="p-2 text-right">Tiền thu</th><th class="p-2 text-right">Hoàn tiền</th><th class="p-2 text-right">Doanh thu</th></tr></thead>
                <tbody>
                  @for (c of v.byChannel; track c.channel) {
                    <tr class="border-t border-line"><td class="p-2">{{ channelLabel(c.channel) }}</td><td class="p-2 text-right tabular-nums">{{ c.orders }}</td><td class="p-2 text-right tabular-nums">{{ c.paid | vnd }}</td><td class="p-2 text-right tabular-nums">{{ c.refunds | vnd }}</td><td class="p-2 text-right font-medium tabular-nums">{{ c.net | vnd }}</td></tr>
                  }
                </tbody>
              </table></div>
              <p class="px-4 py-3 text-sm text-muted">Trong doanh thu giao hàng có {{ v.shippingCollected | vnd }} tiền ship thực thu (đã trừ mã giảm ship).</p>
            </section>
          }
        }
        @case ('reservations') {
          @let v = r.data;
          <div class="a-row4">
            <ot-stat label="Tổng lượt đặt" [value]="v.total" [tone]="2" />
            <ot-stat label="Đã đến" [value]="v.arrived" [note]="v.arrivedPercent + '%'" [tone]="4" />
            <ot-stat label="Đã hủy" [value]="v.cancelled" [note]="v.cancelledPercent + '%'" [tone]="3" />
            <ot-stat label="Không đến" [value]="v.noShow" [note]="v.noShowPercent + '%'" [tone]="1" />
          </div>
          <div class="a-row66">
            <ot-chart title="Lượt đặt theo giờ" kind="bar" [points]="named(v.byHour)" />
            <ot-chart title="Theo khu vực" kind="doughnut" [points]="named(v.byArea)" />
          </div>
        }
        @case ('orders') {
          @let v = r.data;
          <div class="a-row4">
            <ot-stat label="Đơn đã tạo" [value]="v.created" [tone]="2" />
            <ot-stat label="Hoàn thành" [value]="v.completed" [tone]="4" />
            <ot-stat label="Đã hủy" [value]="v.cancelled" [tone]="3" />
            <ot-stat label="Giá trị trung bình" [value]="vnd(v.averageValue)" [tone]="1" />
          </div>
          <div class="a-row66">
            <ot-chart title="Đơn theo giờ" kind="bar" [points]="named(v.byHour)" />
            <ot-chart title="Món bán chạy" kind="hbar" [points]="dishes(v.topDishes)" />
          </div>
        }
        @case ('customers') {
          @let v = r.data;
          <div class="a-row4">
            <ot-stat label="Khách mới" [value]="v.newTotal" [tone]="2" />
            <ot-stat label="Tỷ lệ quay lại" [value]="v.retentionPercent + '%'" [tone]="4" />
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
        @case ('payment-groups') { <ot-payment-groups [data]="r.data" [from]="from" [to]="to" /> }
        @case ('top-dishes') { <div class="a-row66"><ot-chart title="Số lượng bán" kind="hbar" [points]="dishes(r.data)" /><ot-chart title="Doanh thu" kind="doughnut" [points]="dishMoney(r.data)" [money]="true" /></div> }
      }
    }
  `,
})
export class Reports {
  private readonly api = inject(Api);
  protected readonly reports = REPORTS;
  protected readonly days = dayPoints;
  protected readonly named = namedPoints;
  protected readonly report = signal<ReportKind>('revenue');
  protected readonly d = signal<ReportData | null>(null);
  protected readonly form = new Submit();
  protected readonly loader = new Latest();
  protected from = today().slice(0, 8) + '01';
  protected to = today();

  constructor() {
    this.load();
  }

  protected select(key: ReportKind): void {
    this.report.set(key);
    this.d.set(null);
    this.load();
  }


  // Kênh mới server thêm sau mà giao diện chưa có nhãn thì hiện mã kênh
  protected channelLabel(channel: string): string {
    return CHANNELS[channel] ?? channel;
  }

  protected vnd(value: number): string {
    return `${new Intl.NumberFormat('vi-VN').format(value)}\u00a0₫`;
  }

  protected dishes(rows: Schemas['TopDishDto'][]): ChartPoint[] {
    return rows.map((r) => ({ label: r.name, value: r.quantity }));
  }

  protected dishMoney(rows: Schemas['TopDishDto'][]): ChartPoint[] {
    return rows.map((r) => ({ label: r.name, value: r.revenue }));
  }

  protected tiers(rows: Schemas['TierCountDto'][]): ChartPoint[] {
    return rows.map((r) => ({ label: r.tier, value: r.count }));
  }

  protected series(rows: Schemas['RevenuePointDto'][]): ChartPoint[] {
    return rows.map((r) => ({ label: periodLabel(r.period), value: r.net }));
  }

  protected load(): void {
    const kind = this.report();
    this.d.set(null);
    // Đường dẫn reports/<loại> quyết định dạng dữ liệu, nên gắn loại vào ngay lúc nhận
    this.loader.run(this.api.get<ReportData['data']>(`reports/${kind}`, { from: this.from, to: this.to, groupBy: kind === 'revenue' || kind === 'payment-groups' ? 'day' : null, limit: kind === 'revenue' || kind === 'reservations' || kind === 'payment-groups' ? null : 20 }), (data) => this.d.set({ kind, data } as ReportData));
  }

  protected saveChannels(): void {
    this.form.run(this.api.download('reports/channels/export', { from: this.from, to: this.to, format: 'xlsx' }), (r) => saveBlob(r, 'bao-cao-theo-kenh.xlsx'));
  }

  protected save(format: string): void {
    this.form.run(this.api.download(`reports/${this.report()}/export`, { from: this.from, to: this.to, format }), (r) => saveBlob(r, `bao-cao.${format}`));
  }
}
