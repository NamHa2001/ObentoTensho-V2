import { Component, DestroyRef, effect, ElementRef, input, signal, viewChild, inject, untracked } from '@angular/core';
import type { ChartConfiguration, TooltipItem } from 'chart.js';

export interface ChartPoint {
  label: string;
  value: number;
}

export type ChartKind = 'line' | 'bar' | 'hbar' | 'doughnut';

const MAX_SLICES = 5;

export const money = (value: number) => `${new Intl.NumberFormat('vi-VN').format(value)}\u00a0₫`;
export const plain = (value: number) => new Intl.NumberFormat('vi-VN').format(value);

// Biểu đồ tròn tối đa 5 phần, phần nhỏ gộp vào "Khác" để không phải sinh thêm màu
export function foldSlices(points: ChartPoint[]): ChartPoint[] {
  const sorted = points.filter((p) => p.value > 0).sort((a, b) => b.value - a.value);
  if (sorted.length <= MAX_SLICES) {
    return sorted;
  }

  const rest = sorted.slice(MAX_SLICES - 1).reduce((sum, p) => sum + p.value, 0);
  return [...sorted.slice(0, MAX_SLICES - 1), { label: 'Khác', value: rest }];
}

export function chartConfig(kind: ChartKind, points: ChartPoint[], format: (v: number) => string, colors: { series: string[]; ink: string; line: string; surface: string }): ChartConfiguration<'line' | 'bar' | 'doughnut'> {
  const data = kind === 'doughnut' ? foldSlices(points) : points;
  const round = kind === 'doughnut';
  const ticks = { color: colors.ink, font: { size: 12 }, maxRotation: 0, autoSkipPadding: 14 };
  const valueAxis = { beginAtZero: true, grid: { color: colors.line }, border: { display: false }, ticks: { ...ticks, callback: (v: string | number) => format(Number(v)), maxTicksLimit: 6 } };
  const labelAxis = { grid: { display: false }, border: { color: colors.line }, ticks };
  return {
    type: kind === 'hbar' ? 'bar' : kind,
    data: {
      labels: data.map((p) => p.label),
      datasets: [{
        data: data.map((p) => p.value),
        borderColor: round ? colors.surface : colors.series[0],
        backgroundColor: round ? colors.series.slice(0, data.length) : kind === 'line' ? `${colors.series[0]}1f` : colors.series[0],
        borderWidth: 2,
        borderRadius: kind === 'bar' || kind === 'hbar' ? 4 : 0,
        maxBarThickness: 26,
        tension: 0.3,
        fill: kind === 'line',
        pointRadius: 3,
        pointHoverRadius: 5,
        pointBackgroundColor: colors.series[0],
      }],
    },
    options: {
      indexAxis: kind === 'hbar' ? 'y' : 'x',
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      cutout: round ? '68%' : undefined,
      interaction: { mode: round ? 'point' : 'index', intersect: round },
      scales: round ? {} : kind === 'hbar' ? { x: valueAxis, y: labelAxis } : { x: labelAxis, y: valueAxis },
      plugins: {
        legend: { display: round, position: 'bottom', labels: { color: colors.ink, boxWidth: 12, boxHeight: 12, padding: 14 } },
        tooltip: { displayColors: round, callbacks: { label: (c: TooltipItem<'line' | 'bar' | 'doughnut'>) => format(Number(c.raw)) } },
      },
    },
  };
}

@Component({
  selector: 'ot-chart',
  host: { class: 'block h-full' },
  template: `
    <section class="a-card flex h-full flex-col">
      <header class="a-card-h"><span>{{ title() }}</span>@if (points().length > 0) { <button type="button" class="a-link" (click)="table.set(!table())">{{ table() ? 'Xem biểu đồ' : 'Xem bảng' }}</button> }</header>
      <div class="a-card-b flex-1">
        <div class="a-chart" [class.overflow-y-auto]="table()">
          @if (points().length === 0) {
            <div class="a-chart-empty">Chưa có dữ liệu.</div>
          } @else if (table()) {
            <table><tbody>@for (p of points(); track $index) { <tr><td>{{ p.label }}</td><td class="text-right tabular-nums">{{ show(p.value) }}</td></tr> }</tbody></table>
          }
          <canvas #canvas [class.hidden]="table() || points().length === 0" role="img" [attr.aria-label]="title()"></canvas>
        </div>
      </div>
    </section>
  `,
})
export class ChartCard {
  readonly title = input.required<string>();
  readonly kind = input<ChartKind>('line');
  readonly points = input.required<ChartPoint[]>();
  readonly money = input(false);
  protected readonly table = signal(false);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private chart: { destroy(): void } | null = null;
  private run = 0;

  constructor() {
    effect(() => {
      const points = this.points();
      const kind = this.kind();
      const element = this.canvas().nativeElement;
      untracked(() => void this.draw(element, kind, points));
    });
    inject(DestroyRef).onDestroy(() => {
      this.run += 1;
      this.chart?.destroy();
    });
  }

  protected show(value: number): string {
    return this.money() ? money(value) : plain(value);
  }

  private async draw(element: HTMLCanvasElement, kind: ChartKind, points: ChartPoint[]): Promise<void> {
    const run = ++this.run;
    const { default: Chart } = await import('chart.js/auto');
    if (run !== this.run) {
      return;
    }

    this.chart?.destroy();
    this.chart = null;
    if (points.length === 0) {
      return;
    }

    const css = getComputedStyle(document.documentElement);
    const token = (name: string) => css.getPropertyValue(name).trim();
    const colors = { series: [1, 2, 3, 4, 5].map((i) => token(`--ot-chart-${i}`)), ink: token('--ot-text-muted'), line: token('--ot-border'), surface: token('--ot-surface') };
    this.chart = new Chart(element, chartConfig(kind, points, (v) => this.show(v), colors));
  }
}

@Component({
  selector: 'ot-stat',
  host: { class: 'block' },
  template: `
    <div class="a-card a-stat" [style.--a-tone]="'var(--ot-chart-' + tone() + ')'">
      <div class="min-w-0">
        <p class="a-stat-label">{{ label() }}</p>
        <p class="a-stat-value">{{ value() }}</p>
        @if (note()) { <p class="a-stat-note">{{ note() }}</p> }
      </div>
    </div>
  `,
})
export class StatCard {
  readonly label = input.required<string>();
  readonly value = input.required<string | number>();
  readonly note = input<string | null>(null);
  readonly tone = input(1);
}

export const dayPoints = (rows: { date: string; value: number }[] | null | undefined): ChartPoint[] => (rows ?? []).map((r) => ({ label: `${r.date.slice(8, 10)}/${r.date.slice(5, 7)}`, value: r.value }));
export const namedPoints = (rows: { name: string; value: number }[] | null | undefined): ChartPoint[] => (rows ?? []).map((r) => ({ label: r.name, value: r.value }));
