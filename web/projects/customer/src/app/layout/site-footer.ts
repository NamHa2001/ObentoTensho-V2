import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LucideAngularModule, MapPin, Phone } from 'lucide-angular';

import { switchMap } from 'rxjs';

import { Api } from '../core/api';
import { Turnstile } from '../core/turnstile';
import { Submit } from '../shared/form';

@Component({
  selector: 'ot-site-footer',
  imports: [RouterLink, FormsModule, LucideAngularModule],
  host: { class: 'ot-footer block' },
  template: `
    <div class="ot-container grid grid-cols-2 gap-x-6 gap-y-8 py-10 md:py-14 lg:grid-cols-[minmax(0,4fr)_minmax(0,2fr)_minmax(0,3fr)_minmax(0,3fr)] lg:gap-x-10">
      <div class="col-span-2 lg:col-span-1">
        <a routerLink="/" class="flex items-center gap-3">
          @if (settings()['restaurant.logoUrl']; as url) { <img class="size-12 rounded-full object-cover" [src]="url" alt="" /> } @else { <span class="flex size-12 shrink-0 items-center justify-center rounded-full bg-primary font-serif text-2xl leading-none text-on-primary">天</span> }
          <span class="font-heading text-[22px] font-semibold">{{ name() }}</span>
        </a>
        <p class="ot-footer-muted mt-3">Cơm hộp Nhật, nấu mới mỗi ngày</p>
        <div class="mt-4 grid gap-1">
          @if (settings()['restaurant.address']; as address) { <p class="ot-footer-muted flex items-start gap-2 py-1"><lucide-icon class="mt-0.5 shrink-0" [img]="icons.pin" [size]="18" /><span>{{ address }}</span></p> }
          @if (settings()['restaurant.phone']; as phone) { <a class="flex min-h-11 items-center gap-2 font-semibold md:min-h-9" [href]="'tel:' + phone"><lucide-icon class="shrink-0" [img]="icons.phone" [size]="18" />{{ phone }}</a> }
        </div>
        @if (socials().length > 0) {
          <div class="mt-3 flex flex-wrap gap-2">
            @for (s of socials(); track s.label) { <a class="ot-footer-social" [href]="s.url" target="_blank" rel="noopener">{{ s.label }}</a> }
          </div>
        }
      </div>

      @for (group of groups; track group.title) {
        <nav [attr.aria-label]="group.title">
          <h2 class="ot-footer-title">{{ group.title }}</h2>
          <ul class="mt-2">
            @for (l of group.links; track l.path) { <li><a [routerLink]="l.path" class="ot-footer-link">{{ l.label }}</a></li> }
          </ul>
        </nav>
      }

      <form class="col-span-2 lg:col-span-1" (ngSubmit)="subscribe()">
        <h2 class="ot-footer-title">Nhận tin ưu đãi</h2>
        @if (done()) {
          <p class="ot-footer-muted mt-3">Vui lòng kiểm tra hộp thư để xác nhận.</p>
        } @else {
          <p class="ot-footer-muted mt-3 text-sm">Món mới và khuyến mãi gửi thẳng tới email của bạn.</p>
          <div class="mt-3 flex flex-wrap gap-2">
            <label class="sr-only" for="ot-newsletter">Email của bạn</label>
            <input id="ot-newsletter" class="ot-input mt-0 min-w-0 flex-[1_1_160px]" name="newsletter" type="email" placeholder="Email của bạn" autocomplete="email" required [(ngModel)]="email" />
            <button class="ot-btn ot-btn-primary grow-0" [disabled]="form.busy()">Đăng ký</button>
          </div>
          @if (form.error(); as e) { <p class="ot-footer-error mt-2 text-sm" role="alert">{{ e }}</p> }
        }
      </form>
    </div>

    <div class="border-t border-white/15">
      <div class="ot-container ot-footer-muted flex flex-wrap items-center justify-between gap-x-6 gap-y-1 py-4 text-sm">
        <p>© {{ year }} {{ name() }}</p>
        <p>Nấu mới mỗi ngày tại quán</p>
      </div>
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
  protected readonly icons = { pin: MapPin, phone: Phone };
  protected readonly year = new Date().getFullYear();
  protected readonly name = computed(() => this.settings()['restaurant.name'] || 'Obento Tensho');
  protected readonly socials = computed(() => {
    const all = this.settings();
    return [['Facebook', 'social.facebook'], ['Zalo', 'social.zalo'], ['Instagram', 'social.instagram'], ['TikTok', 'social.tiktok'], ['YouTube', 'social.youtube']]
      .filter(([, key]) => all[key])
      .map(([label, key]) => ({ label, url: all[key] }));
  });
  protected readonly groups = [
    { title: 'Nhà hàng', links: [{ path: '/menu', label: 'Thực đơn' }, { path: '/khuyen-mai', label: 'Khuyến mãi' }, { path: '/khong-gian', label: 'Không gian' }, { path: '/tin-tuc', label: 'Tin tức' }, { path: '/gioi-thieu', label: 'Giới thiệu' }] },
    { title: 'Hỗ trợ', links: [{ path: '/tra-cuu', label: 'Tra cứu đơn' }, { path: '/lien-he', label: 'Liên hệ' }, { path: '/dieu-khoan', label: 'Điều khoản' }, { path: '/chinh-sach-bao-mat', label: 'Chính sách bảo mật' }, { path: '/chinh-sach-huy-hoan', label: 'Hủy và hoàn tiền' }] },
  ];

  protected subscribe(): void {
    const body = { email: this.email, lang: 'vi', website: null };
    this.form.run(this.turnstile.headers().pipe(switchMap((h) => this.api.post('newsletter/subscribe', body, h))), () => this.done.set(true));
  }
}
