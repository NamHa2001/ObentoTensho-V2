import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Schemas } from '@ot/api';
import { RouterLink } from '@angular/router';

import { Api, saveBlob } from '../core/api';
import { Latest, Submit, VnDatePipe, VndPipe } from '../shared/ui';
import { askText } from '../../../../customer/src/app/core/dialog';

const STATUS: Record<string, string> = { pending: 'Chờ xuất', issued: 'Đã xuất', cancelled: 'Đã hủy' };

// Kế toán: yêu cầu xuất hóa đơn từ mọi kênh. Xuất Excel để lập hóa đơn điện tử trên phần mềm của nhà cung cấp,
// rồi ghi lại số hóa đơn. Số CCCD/SĐT che, "Hiện" có ghi nhật ký.
type InvoiceRequest = Schemas['InvoiceRequestDto'];
type InvoiceContact = Schemas['InvoiceContactDto'];

@Component({
  selector: 'ot-invoices',
  imports: [FormsModule, RouterLink, VndPipe, VnDatePipe],
  template: `
    <div class="a-head">
      <h1>Yêu cầu xuất hóa đơn</h1>
      <div class="a-btns"><button type="button" class="ot-btn ot-btn-outline" [disabled]="form.busy()" (click)="export()">Xuất Excel</button></div>
    </div>
    <p class="mb-3 text-sm text-muted">Phần mềm chưa tự phát hành hóa đơn điện tử: lập hóa đơn trên phần mềm hóa đơn điện tử của quán (MISA, Viettel, VNPT…), rồi bấm "Đã xuất" và nhập số hóa đơn.</p>
    <form class="a-filters mb-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-5" (ngSubmit)="search()">
      <select class="ot-input" name="status" aria-label="Trạng thái" [(ngModel)]="status">
        <option value="">Mọi trạng thái</option>
        @for (s of statuses; track s[0]) { <option [value]="s[0]">{{ s[1] }}</option> }
      </select>
      <input class="ot-input" name="from" type="date" aria-label="Từ ngày" [(ngModel)]="from" />
      <input class="ot-input" name="to" type="date" aria-label="Đến ngày" [(ngModel)]="to" />
      <input class="ot-input" name="q" placeholder="Mã đơn, tên, MST" [(ngModel)]="q" />
      <button class="ot-btn ot-btn-primary">Lọc</button>
    </form>
    @if (form.error() || loader.error(); as e) { <p class="ot-error mb-3" role="alert">{{ e }}</p> }
    <div class="a-table"><table class="w-full text-sm">
      <thead><tr class="bg-surface-2 text-left"><th class="p-2">Ngày</th><th class="p-2">Đơn</th><th class="p-2">Người mua</th><th class="p-2">CCCD / MST</th><th class="p-2">Liên hệ</th><th class="p-2">Tổng</th><th class="p-2">Trạng thái</th><th class="p-2"></th></tr></thead>
      <tbody>
        @for (r of rows(); track r.id) {
          <tr class="border-t border-line align-top">
            <td class="p-2 whitespace-nowrap">{{ r.createdAtUtc | vnDate: true }}</td>
            <td class="p-2">@if (r.channel === 'pickup') { <a class="text-primary" [routerLink]="['/orders', r.orderId]">{{ r.orderCode }}</a> } @else { {{ r.orderCode }} }<small class="block text-muted">{{ r.channel === 'dine_in' ? 'Tại quán' : 'Mang về' }}</small></td>
            <td class="p-2"><b>{{ r.buyerName }}</b><small class="block text-muted">{{ r.buyerType === 'company' ? 'Doanh nghiệp' : 'Cá nhân' }}</small>@if (r.address) { <small class="block">{{ r.address }}</small> }</td>
            <td class="p-2">{{ r.buyerType === 'company' ? r.taxCode : (revealed()[r.id]?.nationalId ?? r.maskedNationalId) }}</td>
            <td class="p-2">@if (r.email) { <span class="block">{{ r.email }}</span> }@if (r.maskedPhone) { <span class="block">{{ revealed()[r.id]?.phone ?? r.maskedPhone }}</span> }</td>
            <td class="p-2 whitespace-nowrap">{{ r.totalAmount | vnd }}</td>
            <td class="p-2"><span class="a-badge">{{ label(r.status) }}</span>@if (r.invoiceNumber) { <small class="block">Số {{ r.invoiceNumber }}</small> }@if (r.cancelReason) { <small class="block text-muted">{{ r.cancelReason }}</small> }</td>
            <td class="p-2 whitespace-nowrap">
              @if (r.buyerType === 'individual' && !revealed()[r.id]) { <button type="button" class="ot-chip" (click)="reveal(r)">Hiện CCCD</button> }
              @if (r.status === 'pending') {
                <button type="button" class="ot-chip" (click)="issue(r)">Đã xuất</button>
                <button type="button" class="ot-chip" (click)="cancel(r)">Hủy</button>
              }
            </td>
          </tr>
        } @empty { <tr><td class="p-4 text-center text-muted" colspan="8">Chưa có yêu cầu nào.</td></tr> }
      </tbody>
    </table></div>
    @if (total() > pageSize) {
      <div class="mt-3 flex items-center justify-end gap-2">
        <button type="button" class="ot-chip" [disabled]="page() === 1" (click)="go(page() - 1)">‹ Trước</button>
        <span>Trang {{ page() }}</span>
        <button type="button" class="ot-chip" [disabled]="page() * pageSize >= total()" (click)="go(page() + 1)">Sau ›</button>
      </div>
    }
  `,
})
export class Invoices {
  private readonly api = inject(Api);
  protected readonly statuses = Object.entries(STATUS);
  protected readonly rows = signal<InvoiceRequest[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly revealed = signal<Record<string, InvoiceContact>>({});
  protected readonly form = new Submit();
  protected readonly loader = new Latest();
  protected readonly pageSize = 20;
  protected status = 'pending';
  protected from = '';
  protected to = '';
  protected q = '';

  constructor() {
    this.load();
  }

  protected label(status: string): string {
    return STATUS[status] ?? status;
  }

  private params(): Record<string, string | number> {
    return { status: this.status, from: this.from, to: this.to, q: this.q.trim() };
  }

  protected search(): void {
    this.page.set(1);
    this.load();
  }

  protected go(page: number): void {
    this.page.set(page);
    this.load();
  }

  private load(): void {
    this.loader.run(this.api.raw<InvoiceRequest[]>('invoice-requests', { ...this.params(), page: this.page(), pageSize: this.pageSize }), (r) => {
      this.rows.set(r.data);
      this.total.set(r.meta?.total ?? r.data.length);
    });
  }

  protected export(): void {
    this.form.run(this.api.download('invoice-requests/export', this.params()), (r) => saveBlob(r, 'yeu-cau-hoa-don.xlsx'));
  }

  protected reveal(r: InvoiceRequest): void {
    this.form.run(this.api.post<InvoiceContact>(`invoice-requests/${r.id}/reveal`), (c) => this.revealed.update((m) => ({ ...m, [r.id]: c })));
  }

  protected async issue(r: InvoiceRequest): Promise<void> {
    const invoiceNumber = await askText(`Số hóa đơn đã xuất cho ${r.buyerName}?`);
    if (invoiceNumber?.trim()) {
      this.form.run(this.api.post(`invoice-requests/${r.id}/issue`, { invoiceNumber }), () => this.load());
    }
  }

  protected async cancel(r: InvoiceRequest): Promise<void> {
    const reason = await askText(`Lý do hủy yêu cầu của ${r.buyerName}?`);
    if (reason?.trim()) {
      this.form.run(this.api.post(`invoice-requests/${r.id}/cancel`, { reason }), () => this.load());
    }
  }
}
