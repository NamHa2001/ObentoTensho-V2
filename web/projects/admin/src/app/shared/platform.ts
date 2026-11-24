import { Component, inject, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Schemas } from '@ot/api';

import { Api } from '../core/api';
import { Submit, VndPipe } from './ui';

export type PaymentGroup = Schemas['PaymentGroupRowDto']['group'];
export type Platform = Schemas['PlatformOrderDto']['platform'];
export type Rates = Schemas['RevenueRatesDto'];
type PlatformOrder = Schemas['PlatformOrderDto'];

// docs/srs/23 BR-PLT-01: nhóm thanh toán và kênh con cố định
export const GROUP_LABEL: Record<PaymentGroup, string> = { in_store: 'Tại quán', tensho: 'Tensho', hima: 'Hima', xanh: 'Xanh' };
export const PLATFORM_LABEL: Record<Platform, string> = { grab: 'Grab', shopee: 'Shopee', be: 'Be', xanh: 'Xanh' };
export const APP_GROUPS: { group: Exclude<PaymentGroup, 'in_store'>; platforms: Platform[] }[] = [
  { group: 'tensho', platforms: ['grab', 'shopee'] },
  { group: 'hima', platforms: ['grab', 'shopee', 'be'] },
  { group: 'xanh', platforms: ['xanh'] },
];

// Xanh là app riêng không có kênh con: chỉ hiện tên nhóm
export function appLabel(group: PaymentGroup, platform: Platform | null | undefined): string {
  return !platform || group === 'xanh' ? GROUP_LABEL[group] : `${PLATFORM_LABEL[platform]} ${GROUP_LABEL[group]}`;
}

export function ratePercent(value: number): string {
  return `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value)}%`;
}

export interface Split {
  net: number;
  deduction: number;
  vat: number;
  pit: number;
  received: number;
}

// BR-PLT-05: mỗi khoản làm tròn về đồng (≥ 0,5 lên), thực nhận = ròng − các khoản đã làm tròn; giống cách server tính
export function splitRevenue(net: number, rates: Rates): Split {
  const part = (p: number) => Math.round((net * p) / 100);
  const deduction = part(rates.deductionPercent);
  const vat = part(rates.vatPercent);
  const pit = part(rates.pitPercent);
  return { net, deduction, vat, pit, received: net - deduction - vat - pit };
}

// BR-PLT-07: nhập % thì số tiền = giá gốc × %; nhập số tiền thì % = số tiền / giá gốc (1 chữ số lẻ)
export function discountOf(subTotal: number, mode: 'percent' | 'amount', value: number): { amount: number; percent: number } {
  const v = Number(value) || 0;
  if (mode === 'amount') {
    return { amount: v, percent: subTotal > 0 ? Math.round((v * 1000) / subTotal) / 10 : 0 };
  }

  return { amount: Math.round((subTotal * v) / 100), percent: v };
}

export function discountBody(mode: 'percent' | 'amount', value: number): { discountPercent: number | null; discountAmount: number | null } {
  const v = Number(value) || 0;
  return mode === 'amount' ? { discountPercent: null, discountAmount: v } : { discountPercent: v, discountAmount: null };
}

export const PLATFORM_FIELDS: Record<string, string> = { externalordercode: 'Mã đơn app', discountpercent: '% giảm', discountamount: 'Số tiền giảm', platform: 'Kênh', paymentgroup: 'Nhóm', items: 'Món' };

// Trùng mã đơn app cùng nhóm, cùng kênh (BR-PLT-06): báo câu dễ hiểu thay vì lỗi chung
export function duplicateCode(e: unknown): boolean {
  const errors = ((e as HttpErrorResponse).error as { errors?: Record<string, string[]> } | null)?.errors;
  return !!errors && Object.entries(errors).some(([k, v]) => k.toLowerCase().endsWith('externalordercode') && v.includes('DUPLICATE'));
}

export const DUPLICATE_CODE = 'Mã đơn này đã được nhập.';

// Quản lý sửa mã đơn app hoặc giảm giá; đơn đã hoàn thành được tính lại theo tỉ lệ đã chốt trên đơn
@Component({
  selector: 'ot-platform-edit',
  imports: [FormsModule, VndPipe],
  template: `
    <div class="a-drawer" role="dialog" aria-modal="true">
      <form class="a-drawer-panel" (ngSubmit)="save()">
        <div class="a-drawer-h">Sửa đơn {{ label() }}</div>
        <div class="a-drawer-b">
          <p class="text-sm text-muted">Giá gốc {{ order().subTotal | vnd }}</p>
          <label>Mã đơn app<input class="ot-input" name="code" required maxlength="50" [(ngModel)]="code" /></label>
          <fieldset class="flex gap-4">
            <legend>Giảm giá trên app</legend>
            <label class="flex items-center gap-2 font-normal"><input type="radio" name="mode" value="percent" [(ngModel)]="mode" /> Theo %</label>
            <label class="flex items-center gap-2 font-normal"><input type="radio" name="mode" value="amount" [(ngModel)]="mode" /> Theo số tiền</label>
          </fieldset>
          <label>{{ mode === 'percent' ? '% giảm' : 'Số tiền giảm' }}<input class="ot-input" name="discount" type="number" min="0" [max]="mode === 'percent' ? 100 : order().subTotal" [step]="mode === 'percent' ? 0.01 : 1" [(ngModel)]="discount" /></label>
          @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
        </div>
        <div class="a-drawer-f">
          <button type="button" class="ot-btn ot-btn-outline" (click)="closed.emit()">Đóng</button>
          <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Lưu</button>
        </div>
      </form>
    </div>
  `,
})
export class PlatformEdit {
  private readonly api = inject(Api);
  readonly order = input.required<{ id: string; group: PaymentGroup; platform: Platform | null; externalOrderCode: string; subTotal: number; discountPercent: number | null; discount: number }>();
  readonly saved = output<PlatformOrder>();
  readonly closed = output();
  protected readonly form = new Submit();
  protected code = '';
  protected mode: 'percent' | 'amount' = 'percent';
  protected discount = 0;

  constructor() {
    this.form.labels = PLATFORM_FIELDS;
    queueMicrotask(() => {
      const o = this.order();
      this.code = o.externalOrderCode;
      this.discount = o.discountPercent ?? 0;
    });
  }

  protected label(): string {
    return appLabel(this.order().group, this.order().platform);
  }

  protected save(): void {
    this.form.run(
      this.api.put<PlatformOrder>(`platform-orders/${this.order().id}`, { externalOrderCode: this.code.trim(), ...discountBody(this.mode, this.discount) }),
      (o) => this.saved.emit(o),
      (e) => duplicateCode(e) && (this.form.error.set(DUPLICATE_CODE), true),
    );
  }
}
