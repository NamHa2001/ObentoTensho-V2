import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ChevronLeft, LucideAngularModule, Minus, Plus } from 'lucide-angular';

import { POS_DRAFT_LINES, POS_MENU, POS_PICKER, PosMenu } from '../shared/pos-menu';
import { APP_GROUPS, discountBody, discountOf, DUPLICATE_CODE, duplicateCode, GROUP_LABEL, PaymentGroup, Platform, PLATFORM_FIELDS, PLATFORM_LABEL, ratePercent, Rates, splitRevenue } from '../shared/platform';
import { Submit, VndPipe } from '../shared/ui';

// docs/srs/23 FR-PLT-01: thu ngân nhập lại đơn khách đặt trên app — nhóm, kênh con, mã đơn app, từng món, giảm giá khách đã áp;
// màn hình tính ngay doanh thu ròng, các khoản trừ và thực nhận theo tỉ lệ hiện tại. Lưu là gửi bếp.
@Component({
  selector: 'ot-platform-order',
  imports: [FormsModule, RouterLink, VndPipe, LucideAngularModule],
  template: `
    <div class="a-head">
      <h1>Nhập đơn app</h1>
      <div class="a-btns a-desk-only"><a class="ot-btn ot-btn-outline" routerLink="/dine-in"><lucide-icon [img]="icons.back" [size]="18" />Sơ đồ bàn</a></div>
    </div>

    <div class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_420px]">
      <section class="a-card">
        <header class="a-card-h">Chọn món</header>
        <div class="a-card-b grid gap-3">
${POS_MENU}
        </div>
      </section>

      <form class="a-card self-start" (ngSubmit)="save()">
        <header class="a-card-h">Đơn app</header>
        <div class="a-card-b grid gap-3">
          <fieldset class="flex flex-wrap gap-4">
            <legend>Tài khoản</legend>
            @for (g of appGroups; track g.group) {
              <label class="flex items-center gap-2 font-normal"><input type="radio" name="group" [value]="g.group" [ngModel]="group()" (ngModelChange)="pickGroup($event)" /> {{ groupLabel[g.group] }}</label>
            }
          </fieldset>
          @if (group() !== 'xanh') {
            <fieldset class="flex flex-wrap gap-4">
              <legend>App</legend>
              @for (p of platforms(); track p) {
                <label class="flex items-center gap-2 font-normal"><input type="radio" name="platform" [value]="p" [ngModel]="platform()" (ngModelChange)="platform.set($event)" /> {{ platformLabel[p] }}</label>
              }
            </fieldset>
          }
          <label>Mã đơn trên app<input class="ot-input" name="code" required maxlength="50" autocomplete="off" placeholder="Ví dụ: GF-123456" [(ngModel)]="code" /></label>

          <div id="pos-draft">
            <h2 class="mb-1 font-medium">Món đã chọn <small class="font-normal text-muted">{{ draftCount() }} món</small></h2>
            @if (draft().length > 0) {
${POS_DRAFT_LINES}
            } @else { <p class="text-sm text-muted">Bấm món ở bảng bên cạnh để thêm.</p> }
          </div>

          <fieldset class="flex flex-wrap gap-4">
            <legend>Giảm giá khách đã áp trên app</legend>
            <label class="flex items-center gap-2 font-normal"><input type="radio" name="mode" value="percent" [ngModel]="mode()" (ngModelChange)="setMode($event)" /> Theo %</label>
            <label class="flex items-center gap-2 font-normal"><input type="radio" name="mode" value="amount" [ngModel]="mode()" (ngModelChange)="setMode($event)" /> Theo số tiền</label>
          </fieldset>
          <label>{{ mode() === 'percent' ? '% giảm' : 'Số tiền giảm' }}
            <input class="ot-input" name="discount" type="number" min="0" [max]="mode() === 'percent' ? 100 : draftTotal()" [step]="mode() === 'percent' ? 0.01 : 1" [ngModel]="discount()" (ngModelChange)="discount.set($event)" />
          </label>

          <dl class="grid gap-1 rounded-md bg-surface-2 p-3 tabular-nums">
            <div class="flex justify-between"><dt>Giá gốc</dt><dd>{{ draftTotal() | vnd }}</dd></div>
            <div class="flex justify-between"><dt>Giảm giá ({{ percentText(off().percent) }})</dt><dd>−{{ off().amount | vnd }}</dd></div>
            <div class="flex justify-between font-medium"><dt>Doanh thu ròng</dt><dd>{{ split().net | vnd }}</dd></div>
            @if (rates(); as r) {
              <div class="flex justify-between text-muted"><dt>Khấu trừ {{ percentText(r.deductionPercent) }}</dt><dd>−{{ split().deduction | vnd }}</dd></div>
              <div class="flex justify-between text-muted"><dt>Thuế GTGT {{ percentText(r.vatPercent) }}</dt><dd>−{{ split().vat | vnd }}</dd></div>
              <div class="flex justify-between text-muted"><dt>Thuế TNCN {{ percentText(r.pitPercent) }}</dt><dd>−{{ split().pit | vnd }}</dd></div>
              <div class="flex justify-between text-lg font-semibold"><dt>Thực nhận</dt><dd>{{ split().received | vnd }}</dd></div>
            }
          </dl>
          @if (overDiscount()) { <p class="ot-error">Giảm giá không được lớn hơn giá gốc.</p> }

          <label>Ghi chú cho bếp (không bắt buộc)<input class="ot-input" name="note" maxlength="500" [(ngModel)]="note" /></label>
          @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
          <button class="ot-btn ot-btn-primary w-full" [disabled]="form.busy() || draft().length === 0 || overDiscount()">Lưu và gửi bếp</button>
        </div>
      </form>
    </div>
${POS_PICKER}
  `,
})
export class PlatformOrderEntry extends PosMenu {
  protected readonly icons = { back: ChevronLeft, minus: Minus, plus: Plus };
  private readonly router = inject(Router);
  protected readonly form = new Submit();
  protected readonly appGroups = APP_GROUPS;
  protected readonly groupLabel = GROUP_LABEL;
  protected readonly platformLabel = PLATFORM_LABEL;
  protected readonly group = signal<Exclude<PaymentGroup, 'in_store'>>('tensho');
  protected readonly platform = signal<Platform>('grab');
  protected readonly mode = signal<'percent' | 'amount'>('percent');
  protected readonly discount = signal(0);
  protected readonly rates = signal<Rates | null>(null);
  protected code = '';
  protected note = '';
  protected readonly platforms = computed(() => APP_GROUPS.find((g) => g.group === this.group())?.platforms ?? []);
  protected readonly off = computed(() => discountOf(this.draftTotal(), this.mode(), this.discount()));
  protected readonly overDiscount = computed(() => this.off().amount > this.draftTotal() || this.off().percent > 100);
  protected readonly split = computed(() => {
    const net = Math.max(0, this.draftTotal() - this.off().amount);
    const r = this.rates();
    return r ? splitRevenue(net, r) : { net, deduction: 0, vat: 0, pit: 0, received: net };
  });

  constructor() {
    super();
    this.form.labels = PLATFORM_FIELDS;
    this.loadMenu();
    this.api.get<Rates>('platform-orders/rates').subscribe({ next: (r) => this.rates.set(r), error: () => this.form.error.set('Không tải được tỉ lệ khấu trừ, thuế.') });
  }

  // Đổi nhóm thì chọn sẵn app đầu tiên của nhóm (Xanh chỉ có một)
  protected pickGroup(group: Exclude<PaymentGroup, 'in_store'>): void {
    this.group.set(group);
    this.platform.set(this.platforms()[0]);
  }

  protected setMode(mode: 'percent' | 'amount'): void {
    this.mode.set(mode);
    this.discount.set(0);
  }

  protected percentText(value: number): string {
    return ratePercent(value);
  }

  protected save(): void {
    if (!this.code.trim()) {
      this.form.error.set('Mã đơn app: chưa nhập');
      return;
    }

    const body = {
      paymentGroup: this.group(),
      platform: this.platform(),
      externalOrderCode: this.code.trim(),
      ...discountBody(this.mode(), this.discount()),
      note: this.note.trim() || null,
      items: this.draftItems(),
    };
    this.form.run(
      this.api.post('platform-orders', body),
      () => void this.router.navigateByUrl('/dine-in'),
      (e) => duplicateCode(e) && (this.form.error.set(DUPLICATE_CODE), true),
    );
  }
}
