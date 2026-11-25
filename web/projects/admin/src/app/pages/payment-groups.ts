import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { StatCard } from '../shared/chart';
import { appLabel, GROUP_LABEL, PaymentGroup, PLATFORM_LABEL, PlatformEdit } from '../shared/platform';
import { VndPipe } from '../shared/ui';

type Report = Schemas['PaymentGroupReportDto'];
type Amounts = Schemas['PaymentAmountsDto'];
type GroupOrder = Schemas['PaymentGroupOrderDto'];
type Tab = 'overview' | PaymentGroup;

function vnd(value: number): string {
  return `${new Intl.NumberFormat('vi-VN').format(value)} ₫`;
}

// Bảng tiền một nhóm hoặc kênh con: mọi thẻ cùng thứ tự dòng nên các thẻ đặt cạnh nhau thẳng hàng
@Component({
  selector: 'ot-amounts',
  imports: [VndPipe],
  template: `
    <dl class="a-sum" [class.a-sum-plain]="plain()">
      @if (!compact()) {
        <div><dt>Giá gốc</dt><dd>{{ a().subTotal | vnd }}</dd></div>
        <div><dt>Giảm giá</dt><dd>{{ minus(a().discount) }}{{ a().discount | vnd }}</dd></div>
      }
      <div class="a-sum-strong"><dt>Doanh thu ròng</dt><dd>{{ a().netRevenue | vnd }}</dd></div>
      @if (compact()) {
        <div class="a-sum-muted"><dt>Khấu trừ và thuế</dt><dd>{{ minus(a().deductionAmount) }}{{ a().deductionAmount + a().vatAmount + a().pitAmount | vnd }}</dd></div>
      } @else {
        <div class="a-sum-muted"><dt>Khấu trừ</dt><dd>{{ minus(a().deductionAmount) }}{{ a().deductionAmount | vnd }}</dd></div>
        <div class="a-sum-muted"><dt>Thuế GTGT</dt><dd>{{ minus(a().vatAmount) }}{{ a().vatAmount | vnd }}</dd></div>
        <div class="a-sum-muted"><dt>Thuế TNCN</dt><dd>{{ minus(a().pitAmount) }}{{ a().pitAmount | vnd }}</dd></div>
      }
      <div class="a-sum-total"><dt>Thực nhận</dt><dd>{{ a().receivedAmount | vnd }}</dd></div>
    </dl>
  `,
})
export class AmountsList {
  readonly a = input.required<Pick<Amounts, 'subTotal' | 'discount' | 'netRevenue' | 'deductionAmount' | 'vatAmount' | 'pitAmount' | 'receivedAmount'>>();
  readonly compact = input(false);
  readonly plain = input(false);

  // Khoản trừ bằng 0 thì không ghi dấu trừ
  protected minus(value: number): string {
    return value ? '−' : '';
  }
}

// docs/srs/23 FR-PLT-03: báo cáo theo nhóm thanh toán, mỗi nhóm một tab riêng (cả nhóm, từng kênh con, đơn của nhóm)
@Component({
  selector: 'ot-payment-groups',
  imports: [RouterLink, StatCard, VndPipe, AmountsList, PlatformEdit],
  template: `
    <div class="ot-scroll-row mb-4" role="tablist" aria-label="Nhóm thanh toán" style="margin-inline: 0; padding-inline: 0">
      <button type="button" role="tab" class="ot-chip" [class.ot-chip-active]="tab() === 'overview'" [attr.aria-selected]="tab() === 'overview'" (click)="tab.set('overview')">Tổng quan</button>
      @for (g of data().groups; track g.group) {
        <button type="button" role="tab" class="ot-chip" [class.ot-chip-active]="tab() === g.group" [attr.aria-selected]="tab() === g.group" (click)="tab.set(g.group)">{{ groupLabel[g.group] }} ({{ g.amounts.orders }})</button>
      }
    </div>

    @if (tab() === 'overview') {
      <div class="a-row4">
        <ot-stat label="Doanh thu ròng" [value]="money(data().total.netRevenue)" [note]="data().total.orders + ' đơn'" [tone]="2" />
        <ot-stat label="Thực nhận" [value]="money(data().total.receivedAmount)" [tone]="4" />
        <ot-stat label="Khấu trừ và thuế" [value]="money(data().total.deductionAmount + data().total.vatAmount + data().total.pitAmount)" [tone]="3" />
        <ot-stat label="Giảm giá" [value]="money(data().total.discount)" [tone]="1" />
      </div>
      <div class="grid gap-4 md:grid-cols-2">
        @for (g of data().groups; track g.group) {
          <button type="button" class="a-card a-group-card" (click)="tab.set(g.group)">
            <span class="a-card-h"><span>{{ groupLabel[g.group] }}</span><small class="font-normal text-muted">{{ g.amounts.orders }} đơn</small></span>
            <span class="a-card-b"><ot-amounts [a]="g.amounts" [compact]="true" /></span>
          </button>
        }
      </div>
    } @else if (current(); as g) {
      <div class="grid items-start gap-4 md:grid-cols-2 xl:grid-cols-3">
        <section class="a-card">
          <header class="a-card-h"><span>Cả nhóm {{ groupLabel[g.group] }}</span><small class="font-normal text-muted">{{ g.amounts.orders }} đơn</small></header>
          <div class="a-card-b"><ot-amounts [a]="g.amounts" /></div>
        </section>
        @if (g.group !== 'xanh') {
          @for (c of g.channels; track $index) {
            <section class="a-card">
              <header class="a-card-h"><span>{{ channelName(c) }}</span><small class="font-normal text-muted">{{ c.amounts.orders }} đơn</small></header>
              <div class="a-card-b"><ot-amounts [a]="c.amounts" /></div>
            </section>
          }
        }
      </div>

      <section class="a-card mt-4">
        <header class="a-card-h"><span>Đơn của nhóm {{ groupLabel[g.group] }}</span><small class="font-normal text-muted">{{ total() }} đơn</small></header>
        <div class="hidden overflow-x-auto md:block">
          <table class="a-table-tight a-table-lines">
            <thead>
              <tr>
                <th>Thời gian</th><th>Mã đơn</th>@if (g.group !== 'xanh') { <th>Kênh con</th> }
                <th class="text-right">Giá gốc</th><th class="text-right">Giảm giá</th><th class="text-right">Doanh thu ròng</th><th class="text-right">Khấu trừ, thuế</th><th class="text-right">Thực nhận</th>
                @if (canEdit() && g.group !== 'in_store') { <th class="a-actions"><span class="sr-only">Sửa</span></th> }
              </tr>
            </thead>
            <tbody>
              @for (o of rows(); track o.id) {
                <tr [class.text-muted]="o.status !== 'completed'">
                  <td class="whitespace-nowrap tabular-nums">{{ when(o.atUtc) }}</td>
                  <td class="whitespace-nowrap"><a class="text-primary" [routerLink]="['/orders', o.id]">{{ o.code }}</a>@if (o.externalOrderCode) { <small class="block text-muted">App: {{ o.externalOrderCode }}</small> }@if (o.status !== 'completed') { <small class="block">Đã hủy, không tính</small> }</td>
                  @if (g.group !== 'xanh') { <td class="whitespace-nowrap">{{ rowChannel(o) }}</td> }
                  <td class="whitespace-nowrap text-right tabular-nums">{{ o.subTotal | vnd }}</td>
                  <td class="whitespace-nowrap text-right tabular-nums">{{ o.discount | vnd }}@if (o.discountPercent) { <small class="block text-muted">{{ o.discountPercent }}%</small> }</td>
                  <td class="whitespace-nowrap text-right tabular-nums">{{ o.netRevenue | vnd }}</td>
                  <td class="whitespace-nowrap text-right tabular-nums">{{ o.deductionAmount + o.vatAmount + o.pitAmount | vnd }}</td>
                  <td class="whitespace-nowrap text-right font-medium tabular-nums">{{ o.receivedAmount | vnd }}</td>
                  @if (canEdit() && g.group !== 'in_store') { <td class="a-actions">@if (o.status === 'completed') { <button type="button" class="a-link" (click)="editing.set(o)">Sửa</button> }</td> }
                </tr>
              } @empty { <tr><td colspan="9" class="text-muted">Không có đơn trong khoảng ngày này.</td></tr> }
            </tbody>
          </table>
        </div>
        <ul class="md:hidden">
          @for (o of rows(); track o.id) {
            <li class="a-order-item" [class.text-muted]="o.status !== 'completed'">
              <div class="flex items-baseline justify-between gap-3">
                <a class="font-medium text-primary" [routerLink]="['/orders', o.id]">{{ o.code }}</a>
                <span class="shrink-0 text-sm text-muted tabular-nums">{{ when(o.atUtc) }}</span>
              </div>
              <p class="text-sm text-muted">{{ rowChannel(o) }}@if (o.externalOrderCode) { · App: {{ o.externalOrderCode }} }@if (o.status !== 'completed') { · Đã hủy, không tính }</p>
              <ot-amounts [a]="o" [plain]="true" />
              @if (canEdit() && g.group !== 'in_store' && o.status === 'completed') { <button type="button" class="ot-btn ot-btn-outline w-full" (click)="editing.set(o)">Sửa mã, giảm giá</button> }
            </li>
          } @empty { <li class="a-order-item text-muted">Không có đơn trong khoảng ngày này.</li> }
        </ul>
      </section>
      @if (rows().length < total()) { <button type="button" class="ot-btn ot-btn-outline mt-3" (click)="more()">Xem thêm</button> }
      @if (error(); as e) { <p class="ot-error mt-3">{{ e }}</p> }
    }

    @if (editing(); as o) {
      <ot-platform-edit [order]="{ id: o.id, group: o.group, platform: o.platform ?? null, externalOrderCode: o.externalOrderCode ?? '', subTotal: o.subTotal, discountPercent: o.discountPercent ?? null, discount: o.discount }" (saved)="editing.set(null); reload()" (closed)="editing.set(null)" />
    }
  `,
})
export class PaymentGroups {
  private readonly api = inject(Api);
  private readonly auth = inject(Auth);
  readonly data = input.required<Report>();
  readonly from = input.required<string>();
  readonly to = input.required<string>();
  protected readonly groupLabel = GROUP_LABEL;
  protected readonly tab = signal<Tab>('overview');
  protected readonly rows = signal<GroupOrder[]>([]);
  protected readonly total = signal(0);
  protected readonly error = signal<string | null>(null);
  protected readonly editing = signal<GroupOrder | null>(null);
  protected readonly canEdit = computed(() => this.auth.has('orders_void'));
  protected readonly current = computed(() => this.data().groups.find((g) => g.group === this.tab()) ?? null);
  private page = 1;

  constructor() {
    // Đổi tab hoặc khoảng ngày (báo cáo tải lại) thì tải lại đơn của nhóm từ trang đầu
    effect(() => {
      const tab = this.tab();
      this.data();
      if (tab !== 'overview') {
        untracked(() => this.loadRows(tab, true));
      }
    });
  }

  protected money(value: number): string {
    return vnd(value);
  }

  protected channelName(c: Schemas['PaymentChannelRowDto']): string {
    return c.platform ? PLATFORM_LABEL[c.platform] : c.paymentMethod === 'bank_transfer' ? 'Chuyển khoản' : 'Tiền mặt';
  }

  protected rowChannel(o: GroupOrder): string {
    return o.group === 'in_store' ? (o.paymentMethod === 'bank_transfer' ? 'Chuyển khoản' : 'Tiền mặt') : o.platform ? PLATFORM_LABEL[o.platform] : appLabel(o.group, o.platform);
  }

  protected when(utc: string): string {
    return new Date(utc).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
  }

  protected more(): void {
    const tab = this.tab();
    if (tab !== 'overview') {
      this.page += 1;
      this.loadRows(tab, false);
    }
  }

  protected reload(): void {
    const tab = this.tab();
    if (tab !== 'overview') {
      this.loadRows(tab, true);
    }
  }

  // 20 đơn một lần, mới nhất trước
  private loadRows(group: PaymentGroup, reset: boolean): void {
    if (reset) {
      this.page = 1;
      this.rows.set([]);
    }

    this.error.set(null);
    this.api.raw<GroupOrder[]>('reports/payment-groups/orders', { from: this.from(), to: this.to(), group, page: this.page, pageSize: 20 }).subscribe({
      next: (r) => {
        this.rows.update((rows) => [...rows, ...r.data]);
        this.total.set(r.meta?.total ?? r.data.length);
      },
      error: () => this.error.set('Không tải được danh sách đơn.'),
    });
  }
}
