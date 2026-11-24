import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Schemas } from '@ot/api';

import { Api } from '../core/api';
import { Submit, altOf } from '../shared/ui';

const SETTING_GROUPS: Record<string, string> = { restaurant: 'Thông tin quán', social: 'Mạng xã hội', payment: 'Thanh toán', reservation: 'Đặt bàn', order: 'Đặt món', delivery: 'Giao hàng', loyalty: 'Điểm thưởng', voucher: 'Voucher', notification: 'Thông báo', email: 'Email', otp: 'Mã OTP', i18n: 'Ngôn ngữ', seo: 'SEO', security: 'Bảo mật', content: 'Nội dung', media: 'Hình ảnh', report: 'Báo cáo', revenue: 'Doanh thu' };

type Setting = Schemas['SettingDto'];

@Component({
  selector: 'ot-settings',
  imports: [FormsModule],
  template: `
    <div class="a-head"><h1>Cài đặt</h1><div class="a-btns"><button type="button" class="ot-btn ot-btn-outline" (click)="test()">Gửi email thử</button><button type="button" class="ot-btn ot-btn-primary" [disabled]="form.busy()" (click)="save()">Lưu thay đổi</button></div></div>
    @if (form.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (saved()) { <p class="mb-3 font-medium">Đã lưu.</p> }
    @if (notice(); as n) { <p class="mb-3 font-medium">{{ n }}</p> }
    <!-- Mỗi nhóm cài đặt một tab, không dồn mọi nhóm vào một trang dài -->
    <div class="ot-scroll-row mb-4" role="tablist" style="margin-inline: 0; padding-inline: 0">
      @for (g of groups(); track g.key) { <button type="button" role="tab" class="ot-chip" [class.ot-chip-active]="current() === g.key" [attr.aria-selected]="current() === g.key" (click)="tab.set(g.key)">{{ g.title }}</button> }
    </div>
    <form (ngSubmit)="save()">
      @for (g of groups(); track g.key) {
        <section class="a-card max-w-3xl" role="tabpanel" [hidden]="current() !== g.key">
          <header class="a-card-h">{{ g.title }}</header>
          <div class="a-card-b grid gap-4">
            @for (s of g.items; track s.key) {
              @if (s.type === 'bool') {
                <label class="flex items-center gap-2"><input type="checkbox" [name]="s.key" [ngModel]="m[s.key] === 'true'" (ngModelChange)="m[s.key] = $event ? 'true' : 'false'" /> {{ s.description || s.key }}</label>
              } @else if (s.type === 'image') {
                <label class="a-file">{{ s.description || s.key }}@if (s.protected) { <small class="ml-1 font-normal text-muted">(cần xác thực lại)</small> }<span class="ot-btn ot-btn-outline mt-1 flex w-fit">Chọn ảnh</span><input class="sr-only" type="file" accept="image/*" (change)="upload($event, s.key)" /></label>
                @if (m[s.key]) { <p class="-mt-2 text-sm text-muted">Đã có ảnh. <button type="button" class="text-danger" (click)="m[s.key] = ''">Bỏ ảnh</button></p> }
              } @else if (s.type === 'choice') {
                <label>{{ s.description || s.key }}<select class="ot-input" [name]="s.key" [(ngModel)]="m[s.key]">@for (c of s.choices; track c) { <option [value]="c">{{ c }}</option> }</select></label>
              } @else {
                <label>{{ s.description || s.key }}@if (s.protected) { <small class="ml-1 font-normal text-muted">(cần xác thực lại)</small> }<input class="ot-input" [type]="s.type === 'number' || s.type === 'percent' ? 'number' : s.type === 'time' ? 'time' : 'text'" [attr.step]="s.type === 'percent' ? '0.01' : null" [name]="s.key" [(ngModel)]="m[s.key]" /></label>
              }
            }
          </div>
        </section>
      }
    </form>
  `,
})
export class Settings {
  private readonly api = inject(Api);
  protected readonly items = signal<Setting[]>([]);
  protected readonly form = new Submit();
  protected readonly saved = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly groups = computed(() => {
    const map = new Map<string, Setting[]>();
    for (const item of this.items()) {
      const prefix = item.key.split('.')[0];
      map.set(prefix, [...(map.get(prefix) ?? []), item]);
    }

    return [...map].map(([prefix, items]) => ({ key: prefix, title: SETTING_GROUPS[prefix] ?? prefix, items }));
  });
  // Tab đang mở; mở trang bằng ?nhom=delivery để vào thẳng một nhóm
  protected readonly tab = signal(inject(ActivatedRoute).snapshot.queryParamMap.get('nhom') ?? '');
  protected readonly current = computed(() => (this.groups().some((g) => g.key === this.tab()) ? this.tab() : (this.groups()[0]?.key ?? '')));
  protected m: Record<string, string> = {};
  private original: Record<string, string> = {};

  constructor() {
    this.form.run(this.api.get<Setting[]>('settings'), (s) => {
      this.items.set(s);
      for (const x of s) this.m[x.key] = x.value ?? '';
      this.original = { ...this.m };
    });
  }

  protected upload(event: Event, key: string): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) {
      const data = new FormData();
      data.append('file', file);
      data.append('altText', altOf(file));
      this.form.run(this.api.post<{ id: string }>('images', data), (image) => (this.m[key] = image.id));
    }
  }

  // Chỉ gửi khóa có đổi để khóa cần xác thực lại không bị hỏi oan
  protected save(): void {
    this.saved.set(false);
    const values = Object.fromEntries(Object.entries(this.m).map(([k, v]) => [k, String(v ?? '')]).filter(([k, v]) => v !== this.original[k]));
    this.form.run(this.api.put('settings', { values }), () => {
      this.original = { ...this.m };
      this.saved.set(true);
    });
  }

  protected test(): void {
    this.saved.set(false);
    this.form.run(this.api.post('settings/test-email'), () => this.notice.set('Đã gửi email thử.'));
  }
}
