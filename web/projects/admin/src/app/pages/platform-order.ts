import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ChevronLeft, LucideAngularModule } from 'lucide-angular';

import { Api } from '../core/api';
import { DraftList, PosDraft, PosMenu } from '../shared/pos-menu';
import { APP_GROUPS, appLabel, discountBody, discountOf, DUPLICATE_CODE, duplicateCode, GROUP_LABEL, PaymentGroup, Platform, PLATFORM_FIELDS, PLATFORM_LABEL, ratePercent, Rates, splitRevenue } from '../shared/platform';
import { Submit, VndPipe } from '../shared/ui';

type Step = 'info' | 'items' | 'pay';

const STEPS: { key: Step; label: string }[] = [
  { key: 'info', label: '1. Thông tin' },
  { key: 'items', label: '2. Món' },
  { key: 'pay', label: '3. Giảm giá' },
];

// docs/srs/23 FR-PLT-01: thu ngân nhập lại đơn khách đặt trên app theo ba bước (thông tin đơn, chọn món, giảm giá rồi lưu);
// thanh dưới cùng luôn hiện tóm tắt và thực nhận tính theo tỉ lệ hiện tại. Lưu là gửi bếp.
// Không bọc trang trong <form>: bảng chọn tùy chọn của bộ chọn món có form riêng
@Component({
  selector: 'ot-platform-order',
  imports: [FormsModule, RouterLink, VndPipe, LucideAngularModule, PosMenu, PosDraft],
  template: `
    <div class="a-head">
      <h1>Nhập đơn app</h1>
      <div class="a-btns a-desk-only"><a class="ot-btn ot-btn-outline" routerLink="/dine-in"><lucide-icon [img]="icons.back" [size]="18" />Gọi món tại bàn</a></div>
    </div>

    <div class="a-steps-tabs" role="tablist" aria-label="Các bước nhập đơn app">
      @for (s of steps; track s.key) {
        <button type="button" role="tab" class="ot-chip" [class.ot-chip-active]="step() === s.key" [attr.aria-selected]="step() === s.key" (click)="go(s.key)">
          {{ s.label }}@if (s.key === 'items' && draft.count() > 0) { ({{ draft.count() }}) }
        </button>
      }
    </div>

    <section class="a-card" role="tabpanel" [hidden]="step() !== 'info'">
      <header class="a-card-h">Thông tin đơn</header>
      <div class="a-card-b grid gap-5 md:grid-cols-2">
        <div class="grid content-start gap-5">
          <fieldset>
            <legend>Tài khoản</legend>
            <div class="a-choice" role="radiogroup" aria-label="Tài khoản">
              @for (g of appGroups; track g.group) {
                <button type="button" role="radio" class="ot-chip" [class.ot-chip-active]="group() === g.group" [attr.aria-checked]="group() === g.group" (click)="pickGroup(g.group)">{{ groupLabel[g.group] }}</button>
              }
            </div>
          </fieldset>
          @if (group() !== 'xanh') {
            <fieldset>
              <legend>App</legend>
              <div class="a-choice" role="radiogroup" aria-label="App">
                @for (p of platforms(); track p) {
                  <button type="button" role="radio" class="ot-chip" [class.ot-chip-active]="platform() === p" [attr.aria-checked]="platform() === p" (click)="platform.set(p)">{{ platformLabel[p] }}</button>
                }
              </div>
            </fieldset>
          }
        </div>
        <div class="grid content-start gap-5">
          <label>Mã đơn trên app
            <input class="ot-input" name="code" maxlength="50" autocomplete="off" placeholder="Ví dụ: GF-123456" [(ngModel)]="code" (keydown.enter)="next()" />
            @if (codeError(); as e) { <span class="ot-error mt-1 block">{{ e }}</span> }
          </label>
          <label>Ghi chú cho bếp (không bắt buộc)<input class="ot-input" name="note" maxlength="500" [(ngModel)]="note" /></label>
        </div>
      </div>
    </section>

    <section class="a-card" role="tabpanel" [hidden]="step() !== 'items'">
      <header class="a-card-h"><span>Chọn món</span><small class="font-normal text-muted">Bấm món để thêm vào đơn</small></header>
      <div class="a-card-b"><ot-pos-menu (picked)="draft.add($event)" /></div>
    </section>

    <div class="grid items-start gap-4 lg:grid-cols-2" role="tabpanel" [hidden]="step() !== 'pay'">
      <section class="a-card">
        <header class="a-card-h"><span>Món đã chọn</span><small class="font-normal text-muted">{{ draft.count() }} món</small></header>
        <div class="a-card-b">
          @if (draft.lines().length > 0) { <ot-pos-draft [list]="draft" /> }
          @else { <p class="text-muted">Chưa chọn món. <button type="button" class="a-link" (click)="go('items')">Chọn món</button></p> }
        </div>
      </section>
      <section class="a-card">
        <header class="a-card-h">Giảm giá khách đã áp trên app</header>
        <div class="a-card-b grid gap-5">
          <div class="grid gap-4 sm:grid-cols-2">
            <fieldset>
              <legend>Nhập theo</legend>
              <div class="a-choice" role="radiogroup" aria-label="Nhập giảm giá theo">
                <button type="button" role="radio" class="ot-chip" [class.ot-chip-active]="mode() === 'percent'" [attr.aria-checked]="mode() === 'percent'" (click)="setMode('percent')">Phần trăm</button>
                <button type="button" role="radio" class="ot-chip" [class.ot-chip-active]="mode() === 'amount'" [attr.aria-checked]="mode() === 'amount'" (click)="setMode('amount')">Số tiền</button>
              </div>
            </fieldset>
            <label>{{ mode() === 'percent' ? 'Giảm (%)' : 'Giảm (₫)' }}
              <input class="ot-input" name="discount" type="number" inputmode="decimal" min="0" [max]="mode() === 'percent' ? 100 : draft.total()" [step]="mode() === 'percent' ? 0.01 : 1" [ngModel]="discount()" (ngModelChange)="discount.set($event)" />
            </label>
          </div>
          <dl class="a-sum">
            <div><dt>Giá gốc</dt><dd>{{ draft.total() | vnd }}</dd></div>
            <div><dt>Giảm giá ({{ percentText(off().percent) }})</dt><dd>−{{ off().amount | vnd }}</dd></div>
            <div class="a-sum-strong"><dt>Doanh thu ròng</dt><dd>{{ split().net | vnd }}</dd></div>
            @if (rates(); as r) {
              <div class="a-sum-muted"><dt>Khấu trừ {{ percentText(r.deductionPercent) }}</dt><dd>−{{ split().deduction | vnd }}</dd></div>
              <div class="a-sum-muted"><dt>Thuế GTGT {{ percentText(r.vatPercent) }}</dt><dd>−{{ split().vat | vnd }}</dd></div>
              <div class="a-sum-muted"><dt>Thuế TNCN {{ percentText(r.pitPercent) }}</dt><dd>−{{ split().pit | vnd }}</dd></div>
              <div class="a-sum-total"><dt>Thực nhận</dt><dd>{{ split().received | vnd }}</dd></div>
            }
          </dl>
          @if (overDiscount()) { <p class="ot-error">Giảm giá không được lớn hơn giá gốc.</p> }
        </div>
      </section>
    </div>

    <div class="a-steps-bar">
      <div class="a-steps-sum">
        <b>{{ appName() }}</b>@if (code.trim()) { · {{ code.trim() }} }
        <span class="block text-sm text-muted">{{ draft.count() }} món · thực nhận {{ split().received | vnd }}</span>
        @if (form.error(); as e) { <span class="ot-error mt-1 block" role="alert">{{ e }}</span> }
      </div>
      <div class="a-btns">
        @if (step() !== 'info') { <button type="button" class="ot-btn ot-btn-outline" (click)="back()">Quay lại</button> }
        <button type="button" class="ot-btn ot-btn-primary" [disabled]="form.busy() || (step() === 'pay' && (draft.lines().length === 0 || overDiscount()))" (click)="next()">{{ step() === 'pay' ? 'Lưu và gửi bếp' : 'Tiếp tục' }}</button>
      </div>
    </div>
  `,
})
export class PlatformOrderEntry {
  protected readonly icons = { back: ChevronLeft };
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected readonly draft = new DraftList();
  protected readonly form = new Submit();
  protected readonly steps = STEPS;
  protected readonly appGroups = APP_GROUPS;
  protected readonly groupLabel = GROUP_LABEL;
  protected readonly platformLabel = PLATFORM_LABEL;
  protected readonly step = signal<Step>('info');
  protected readonly group = signal<Exclude<PaymentGroup, 'in_store'>>('tensho');
  protected readonly platform = signal<Platform>('grab');
  protected readonly mode = signal<'percent' | 'amount'>('percent');
  protected readonly discount = signal(0);
  protected readonly rates = signal<Rates | null>(null);
  protected readonly codeError = signal<string | null>(null);
  protected code = '';
  protected note = '';
  protected readonly platforms = computed(() => APP_GROUPS.find((g) => g.group === this.group())?.platforms ?? []);
  protected readonly appName = computed(() => appLabel(this.group(), this.platform()));
  protected readonly off = computed(() => discountOf(this.draft.total(), this.mode(), this.discount()));
  protected readonly overDiscount = computed(() => this.off().amount > this.draft.total() || this.off().percent > 100);
  protected readonly split = computed(() => {
    const net = Math.max(0, this.draft.total() - this.off().amount);
    const r = this.rates();
    return r ? splitRevenue(net, r) : { net, deduction: 0, vat: 0, pit: 0, received: net };
  });

  constructor() {
    this.form.labels = PLATFORM_FIELDS;
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

  // Sang bước sau chỉ khi bước trước đủ: có mã đơn app, có món
  protected go(step: Step): void {
    this.form.error.set(null);
    if (step !== 'info' && !this.code.trim()) {
      this.codeError.set('Nhập mã đơn trên app trước.');
      this.step.set('info');
      return;
    }

    this.codeError.set(null);
    if (step === 'pay' && this.draft.lines().length === 0) {
      this.form.error.set('Chọn ít nhất một món.');
      this.step.set('items');
      return;
    }

    this.step.set(step);
    window.scrollTo({ top: 0 });
  }

  protected back(): void {
    this.go(this.step() === 'pay' ? 'items' : 'info');
  }

  protected next(): void {
    if (this.step() !== 'pay') {
      this.go(this.step() === 'info' ? 'items' : 'pay');
      return;
    }

    const body = {
      paymentGroup: this.group(),
      platform: this.platform(),
      externalOrderCode: this.code.trim(),
      ...discountBody(this.mode(), this.discount()),
      note: this.note.trim() || null,
      items: this.draft.items(),
    };
    this.form.run(
      this.api.post('platform-orders', body),
      () => void this.router.navigateByUrl('/dine-in?tab=app'),
      (e) => duplicateCode(e) && (this.form.error.set(DUPLICATE_CODE), true),
    );
  }
}
