import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { switchMap } from 'rxjs';

import { Api } from '../core/api';
import { Turnstile } from '../core/turnstile';
import { Submit } from '../shared/form';

@Component({
  selector: 'ot-site-footer',
  imports: [RouterLink, FormsModule],
  host: { class: 'block border-t border-line bg-surface' },
  template: `
    <div class="ot-container grid grid-cols-2 gap-x-6 gap-y-3 py-8 text-sm md:grid-cols-4">
      <div class="col-span-2 md:col-span-4">
        <p class="font-heading text-lg font-extrabold text-primary">{{ settings()['restaurant.name'] || 'Obento Tensho' }}</p>
        @if (settings()['restaurant.address']; as address) { <p class="mt-1 text-muted">{{ address }}</p> }
        @if (settings()['restaurant.phone']; as phone) { <a class="mt-1 inline-flex min-h-11 items-center font-semibold text-primary" [href]="'tel:' + phone">Gọi {{ phone }}</a> }
      </div>
      @for (l of links; track l.path) { <a [routerLink]="l.path" class="flex min-h-11 items-center">{{ l.label }}</a> }
      <form class="col-span-2 md:col-span-4" (ngSubmit)="subscribe()">
        @if (done()) { <p>Vui lòng kiểm tra hộp thư để xác nhận.</p> } @else {
          <label>Nhận tin ưu đãi<input class="ot-input mt-1" name="newsletter" type="email" placeholder="Email của bạn" required [(ngModel)]="email" /></label>
          @if (form.error(); as e) { <p class="ot-error mt-1">{{ e }}</p> }
          <button class="ot-btn ot-btn-outline mt-2" [disabled]="form.busy()">Đăng ký</button>
        }
      </form>
    </div>
  `,
})
export class SiteFooter {
  private readonly api = inject(Api);
  private readonly turnstile = inject(Turnstile);
  protected readonly form = new Submit();
  protected readonly settings = toSignal(this.api.getOr<Record<string, string>>('public/settings', {}), { initialValue: {} as Record<string, string> });
  protected readonly done = signal(false);
  protected email = '';
  protected readonly links = [
    { path: '/gioi-thieu', label: 'Giới thiệu' },
    { path: '/lien-he', label: 'Liên hệ' },
    { path: '/tra-cuu', label: 'Tra cứu đơn' },
    { path: '/dieu-khoan', label: 'Điều khoản' },
    { path: '/chinh-sach-bao-mat', label: 'Chính sách bảo mật' },
    { path: '/chinh-sach-huy-hoan', label: 'Chính sách hủy và hoàn tiền' },
  ];

  protected subscribe(): void {
    const body = { email: this.email, lang: 'vi', website: null };
    this.form.run(this.turnstile.headers().pipe(switchMap((h) => this.api.post('newsletter/subscribe', body, h))), () => this.done.set(true));
  }
}
