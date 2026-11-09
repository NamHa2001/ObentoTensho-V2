import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { Submit, today, VnDatePipe } from '../shared/ui';
import { askConfirm } from '../../../../customer/src/app/core/dialog';

const DAY_NAMES = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];

// Khoảng qua nửa đêm tính tới ngày hôm sau để so sánh được
export function rangesOverlap(ranges: { open: string; close: string }[]): boolean {
  const minutes = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));
  const spans = ranges.map((r) => [minutes(r.open), minutes(r.close) + (r.close < r.open ? 1440 : 0)]).sort((a, b) => a[0] - b[0]);
  return spans.some((s, i) => i > 0 && s[0] < spans[i - 1][1]);
}

@Component({
  selector: 'ot-hours',
  imports: [FormsModule, VnDatePipe],
  template: `
    <h1>Giờ mở cửa</h1>
    <div class="a-row66">
      <section class="a-card">
        <header class="a-card-h">Giờ thường kỳ</header>
        <form class="a-card-b grid gap-3" (ngSubmit)="saveWeek()">
          @for (d of days(); track d.dayOfWeek) {
            <div class="grid grid-cols-[minmax(0,1fr)_70px] items-start gap-2 sm:grid-cols-[90px_70px_minmax(0,1fr)]"><b class="pt-2.5">{{ names[d.dayOfWeek] }}</b>
              <label class="flex items-center gap-1 pt-2.5 font-normal"><input type="checkbox" [name]="'c' + d.dayOfWeek" [(ngModel)]="d.closed" /> Nghỉ</label>
              @if (!d.closed) {
                <div class="col-span-2 grid gap-2 sm:col-span-1">
                  @for (r of d.ranges; track $index; let i = $index) {
                    <div class="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)_32px] items-center gap-2">
                      <input class="ot-input mt-0" type="time" aria-label="Giờ mở" [name]="'o' + d.dayOfWeek + i" [(ngModel)]="r.open" />
                      <input class="ot-input mt-0" type="time" aria-label="Giờ đóng" [name]="'e' + d.dayOfWeek + i" [(ngModel)]="r.close" />
                      @if (d.ranges.length > 1) { <button type="button" class="text-danger" aria-label="Bỏ khoảng giờ" (click)="d.ranges.splice(i, 1)">✕</button> }
                    </div>
                  }
                  <button type="button" class="a-link justify-self-start" (click)="d.ranges.push({ open: d.ranges.at(-1)?.close ?? '17:00', close: '21:00' })">Thêm khoảng giờ</button>
                </div>
              }
            </div>
          }
          @if (weekForm.error(); as e) { <p class="ot-error">{{ friendly(e) }}</p> }
          @if (auth.has('regular_hours')) { <div><button class="ot-btn ot-btn-primary" [disabled]="weekForm.busy()">Lưu giờ thường kỳ</button></div> }
        </form>
      </section>
      <section class="a-card">
        <header class="a-card-h">Giờ đặc biệt</header>
        <div class="a-card-b">
          @for (s of special(); track s.date) { <div class="a-line"><span>{{ s.date | vnDate }}: {{ s.closed ? 'Nghỉ' : range(s) }} {{ s.note }}</span>@if (auth.has('special_hours')) { <button type="button" class="text-danger" (click)="removeSpecial(s.date)">Xóa</button> }</div> } @empty { <p class="text-muted">Chưa có.</p> }
          @if (auth.has('special_hours')) {
            <form class="a-plain mt-4 grid gap-3" (ngSubmit)="addSpecial()">
              <label>Ngày<input class="ot-input" name="d" type="date" required [(ngModel)]="sp.date" /></label>
              <label class="flex items-center gap-2"><input type="checkbox" name="c" [(ngModel)]="sp.closed" /> Nghỉ cả ngày</label>
              @if (!sp.closed) { <div class="grid grid-cols-2 gap-3"><label>Mở<input class="ot-input" name="o" type="time" [(ngModel)]="sp.open" /></label><label>Đóng<input class="ot-input" name="e" type="time" [(ngModel)]="sp.close" /></label></div> }
              <label>Ghi chú<input class="ot-input" name="n" [(ngModel)]="sp.note" /></label>
              @if (specialForm.error(); as e) { <p class="ot-error">{{ e }}</p> }
              <div><button class="ot-btn ot-btn-outline" [disabled]="specialForm.busy()">Thêm giờ đặc biệt</button></div>
            </form>
          }
        </div>
      </section>
    </div>
  `,
})
export class Hours {
  private readonly api = inject(Api);
  protected readonly auth = inject(Auth);
  protected readonly names = DAY_NAMES;
  protected readonly days = signal<any[]>([]);
  protected readonly special = signal<any[]>([]);
  protected readonly weekForm = new Submit();
  protected readonly specialForm = new Submit();
  protected sp: any = { date: today(), closed: true, open: '10:00', close: '21:00', note: '' };
  private version: string | null = null;

  constructor() {
    this.loadWeek();
    this.loadSpecial();
  }

  private loadWeek(): void {
    this.weekForm.run(this.api.get<any>('opening-hours'), (w) => {
      this.version = w.version;
      this.days.set(w.days.map((d: any) => ({ dayOfWeek: d.dayOfWeek, closed: d.closed, ranges: d.ranges.length > 0 ? d.ranges.map((r: any) => ({ open: r.open.slice(0, 5), close: r.close.slice(0, 5) })) : [{ open: '10:00', close: '21:00' }] })));
    });
  }

  private loadSpecial(): void {
    this.api.get<any[]>('special-hours').subscribe({ next: (s) => this.special.set(s), error: () => undefined });
  }

  protected range(s: any): string {
    return s.ranges.map((r: any) => `${r.open.slice(0, 5)}–${r.close.slice(0, 5)}`).join(', ');
  }

  // API trả mã theo trường; đổi sang câu người dùng đọc được
  protected friendly(error: string): string {
    if (error.startsWith('serviceSlots')) {
      return 'Có ca phục vụ nằm ngoài giờ mở cửa mới. Hãy sửa hoặc tắt ca đó trước, rồi lưu lại.';
    }

    return error.includes('OVERLAP') ? 'Các khoảng giờ đang chồng lên nhau, kể cả phần qua nửa đêm.' : error;
  }

  protected saveWeek(): void {
    const clash = this.days().find((d) => !d.closed && rangesOverlap(d.ranges));
    if (clash) {
      this.weekForm.error.set(`${this.names[clash.dayOfWeek]}: các khoảng giờ đang chồng lên nhau.`);
      return;
    }

    const days = this.days().map((d) => ({ dayOfWeek: d.dayOfWeek, closed: d.closed, ranges: d.closed ? [] : d.ranges.map((r: any) => ({ open: r.open, close: r.close, crossesMidnight: r.close < r.open })) }));
    this.weekForm.run(this.api.put('opening-hours', { days }, this.version), () => this.loadWeek());
  }

  protected addSpecial(): void {
    const body = { closed: this.sp.closed, ranges: this.sp.closed ? [] : [{ open: this.sp.open, close: this.sp.close, crossesMidnight: this.sp.close < this.sp.open }], note: this.sp.note || null };
    this.specialForm.run(this.api.put(`special-hours/${this.sp.date}`, body), () => this.loadSpecial());
  }

  protected async removeSpecial(date: string): Promise<void> {
    if (await askConfirm('Xóa giờ đặc biệt này?')) {
      this.api.delete(`special-hours/${date}`).subscribe(() => this.loadSpecial());
    }
  }
}
