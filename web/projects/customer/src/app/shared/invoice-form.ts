import { Component, input } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface InvoiceBuyer {
  buyerType: 'individual' | 'company';
  buyerName: string;
  nationalId: string;
  taxCode: string;
  address: string;
  email: string;
  phone: string;
  note: string;
}

export function emptyBuyer(): InvoiceBuyer {
  return { buyerType: 'individual', buyerName: '', nationalId: '', taxCode: '', address: '', email: '', phone: '', note: '' };
}

// Gửi API: ô trống thành null, bỏ trường không thuộc loại người mua đã chọn
export function buyerBody(b: InvoiceBuyer): Record<string, string | null> {
  const v = (s: string) => s.trim() || null;
  const company = b.buyerType === 'company';
  return {
    buyerType: b.buyerType,
    buyerName: b.buyerName.trim(),
    nationalId: company ? null : v(b.nationalId),
    taxCode: company ? v(b.taxCode) : null,
    address: v(b.address),
    email: v(b.email),
    phone: v(b.phone),
    note: v(b.note),
  };
}

// Thông tin xuất hóa đơn (mọi ô bắt buộc), dùng chung cho trang đặt món của khách và bước thanh toán tại bàn của quán
@Component({
  selector: 'ot-invoice-form',
  imports: [FormsModule],
  template: `
    @let b = model();
    <div class="grid gap-3">
      <div class="flex flex-wrap gap-4" role="radiogroup" aria-label="Loại hóa đơn">
        <label class="flex items-center gap-2 font-normal"><input type="radio" value="individual" [ngModelOptions]="{ standalone: true }" [(ngModel)]="b.buyerType" /> Cá nhân</label>
        <label class="flex items-center gap-2 font-normal"><input type="radio" value="company" [ngModelOptions]="{ standalone: true }" [(ngModel)]="b.buyerType" /> Doanh nghiệp</label>
      </div>
      @if (b.buyerType === 'company') {
        <label>Tên công ty<input class="ot-input" maxlength="200" autocomplete="organization" required [ngModelOptions]="{ standalone: true }" [(ngModel)]="b.buyerName" /></label>
        <label>Mã số thuế<input class="ot-input" inputmode="numeric" maxlength="14" required placeholder="10 số, chi nhánh thêm -xxx" [ngModelOptions]="{ standalone: true }" [(ngModel)]="b.taxCode" /></label>
        <label>Địa chỉ công ty<input class="ot-input" maxlength="300" autocomplete="street-address" required [ngModelOptions]="{ standalone: true }" [(ngModel)]="b.address" /></label>
        <label>Email nhận hóa đơn<input class="ot-input" type="email" maxlength="150" required [ngModelOptions]="{ standalone: true }" [(ngModel)]="b.email" /></label>
      } @else {
        <label>Họ tên người mua<input class="ot-input" maxlength="200" autocomplete="name" required [ngModelOptions]="{ standalone: true }" [(ngModel)]="b.buyerName" /></label>
        <label>Số CCCD<input class="ot-input" inputmode="numeric" maxlength="12" required placeholder="12 số" [ngModelOptions]="{ standalone: true }" [(ngModel)]="b.nationalId" /></label>
        <label>Số điện thoại<input class="ot-input" type="tel" required [ngModelOptions]="{ standalone: true }" [(ngModel)]="b.phone" /></label>
        <label>Email nhận hóa đơn<input class="ot-input" type="email" maxlength="150" required [ngModelOptions]="{ standalone: true }" [(ngModel)]="b.email" /></label>
      }
    </div>
  `,
})
export class InvoiceForm {
  readonly model = input.required<InvoiceBuyer>();
}
