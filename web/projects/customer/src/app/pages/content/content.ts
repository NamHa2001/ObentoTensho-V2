import { Component, computed, DestroyRef, inject, RESPONSE_INIT, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, Meta, SafeResourceUrl, Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { catchError, map, of, switchMap } from 'rxjs';
import { ChevronLeft, LucideAngularModule } from 'lucide-angular';

import { Api } from '../../core/api';
import { Turnstile } from '../../core/turnstile';
import { Submit } from '../../shared/form';
import { Lightbox } from '../../shared/lightbox';
import { imageUrl, VnDatePipe } from '../../shared/format';

@Component({
  selector: 'ot-static-page',
  template: `
    <article class="ot-page">
      @if (page(); as p) {
        <h1>{{ p.title }}</h1>
        <div class="ot-prose" [innerHTML]="p.contentHtml"></div>
      } @else if (loaded()) {
        <h1>{{ fallback }}</h1>
        <p class="mt-3 text-muted">Nội dung đang được cập nhật.</p>
      }
    </article>
  `,
})
export class StaticPage {
  private readonly route = inject(ActivatedRoute).snapshot.data as { slug: string; heading: string };
  protected readonly fallback = this.route.heading;
  protected readonly page = signal<Schemas['PublicStaticPageDto'] | null>(null);
  protected readonly loaded = signal(false);

  constructor() {
    inject(Api).getOr<Schemas['PublicStaticPageDto'] | null>(`public/pages/${this.route.slug}`, null).subscribe((p) => {
      this.page.set(p);
      this.loaded.set(true);
    });
  }
}

@Component({
  selector: 'ot-contact',
  imports: [FormsModule],
  template: `
    <div class="ot-page-split">
      <h1>Liên hệ</h1>
      <div class="ot-split">
        <section class="ot-panel">
          @if (done()) {
            <h2>Đã gửi lời nhắn</h2>
            <p class="mt-2">Cảm ơn bạn. Quán sẽ phản hồi qua email hoặc điện thoại sớm nhất.</p>
          } @else {
            <h2>Gửi lời nhắn</h2>
            <form class="ot-form-grid mt-4" (ngSubmit)="submit()">
              <label>Họ tên<input class="ot-input" name="name" autocomplete="name" required [(ngModel)]="model.fullName" /></label>
              <label>Số điện thoại<input class="ot-input" name="phone" type="tel" autocomplete="tel" required [(ngModel)]="model.phoneNumber" /></label>
              <label class="ot-span-2">Email<input class="ot-input" name="email" type="email" autocomplete="email" required [(ngModel)]="model.email" /></label>
              <label class="ot-span-2">Tiêu đề<input class="ot-input" name="subject" [(ngModel)]="model.subject" /></label>
              <label class="ot-span-2">Nội dung<textarea class="ot-input" name="message" rows="5" required [(ngModel)]="model.message"></textarea></label>
              @if (form.error(); as e) { <p class="ot-error ot-span-2" role="alert">{{ e }}</p> }
              <div class="ot-span-2"><button class="ot-btn ot-btn-primary w-full sm:w-auto sm:px-10" [disabled]="form.busy()">Gửi lời nhắn</button></div>
            </form>
          }
        </section>
        <aside class="ot-panel">
          <h2>Thông tin quán</h2>
          <div class="mt-3 grid gap-3">
            @if (settings()['restaurant.address']; as address) { <p><span class="block text-sm text-muted">Địa chỉ</span>{{ address }}</p> }
            @if (settings()['restaurant.phone']; as phone) { <p><span class="block text-sm text-muted">Điện thoại</span><a class="font-medium text-primary" [href]="'tel:' + phone">{{ phone }}</a></p> }
            @if (settings()['restaurant.email']; as mail) { <p><span class="block text-sm text-muted">Email</span><a class="text-primary" [href]="'mailto:' + mail">{{ mail }}</a></p> }
            @if (!settings()['restaurant.address'] && !settings()['restaurant.phone']) { <p class="text-muted">Thông tin quán đang được cập nhật.</p> }
          </div>
        </aside>
      </div>
    </div>
  `,
})
export class Contact {
  private readonly api = inject(Api);
  private readonly turnstile = inject(Turnstile);
  protected readonly settings = toSignal(this.api.getOr<Record<string, string>>('public/settings', {}), { initialValue: {} as Record<string, string> });
  protected readonly form = new Submit();
  protected readonly done = signal(false);
  protected model = { fullName: '', email: '', phoneNumber: '', subject: '', message: '' };

  protected submit(): void {
    const body = { ...this.model, subject: this.model.subject || null, lang: 'vi', website: null };
    this.form.run(this.turnstile.headers().pipe(switchMap((h) => this.api.post('contact', body, h))), () => this.done.set(true));
  }
}

const EMBED_HOSTS = ['www.youtube.com', 'www.youtube-nocookie.com', 'www.tiktok.com'];

export function isEmbeddable(url: string): boolean {
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'https:' && EMBED_HOSTS.includes(parsed.hostname);
  } catch {
    return false;
  }
}

const GALLERY = [
  { value: '', label: 'Tất cả' },
  { value: 'interior', label: 'Trong quán' },
  { value: 'exterior', label: 'Bên ngoài' },
  { value: 'private', label: 'Phòng riêng' },
  { value: 'food', label: 'Món ăn' },
  { value: 'event', label: 'Sự kiện' },
];

@Component({
  selector: 'ot-gallery',
  imports: [Lightbox],
  template: `
    <div class="ot-page ot-page-wide">
      <h1>Không gian</h1>
      <div class="ot-scroll-row mt-3">
        @for (c of categories; track c.value) {
          <button type="button" class="ot-chip" [class.ot-chip-active]="category() === c.value" (click)="select(c.value)">{{ c.label }}</button>
        }
      </div>
      @if (items().length === 0) { <div class="ot-panel mt-5 text-center"><p class="font-medium">Chưa có hình ảnh</p><p class="mt-1 text-sm text-muted">Quán sẽ cập nhật hình ảnh không gian sớm.</p></div> }
      <div class="mt-4 grid grid-cols-2 gap-3 md:grid-cols-4">
        @for (item of items(); track item.id) {
          @if (item.mediaType === 'video' && item.embedUrl) {
            <iframe class="aspect-video w-full rounded-md" loading="lazy" allowfullscreen [src]="safe(item.embedUrl)" [title]="item.caption ?? 'Video'"></iframe>
          } @else if (url(item); as src) {
            <figure><button type="button" class="block w-full" aria-label="Phóng to ảnh" (click)="open(src)"><img class="aspect-square w-full rounded-md object-cover" loading="lazy" [src]="src" [alt]="item.caption ?? ''" /></button>@if (item.caption) { <figcaption class="mt-1 text-sm text-muted">{{ item.caption }}</figcaption> }</figure>
          }
        }
      </div>
      @if (zoom() !== null) { <ot-lightbox [images]="photos()" [(index)]="zoomIndex" (closed)="zoom.set(null)" /> }
    </div>
  `,
})
export class Gallery {
  private readonly api = inject(Api);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly safeUrls = new Map<string, SafeResourceUrl>();
  private run = 0;
  protected readonly categories = GALLERY;
  protected readonly category = signal('');
  protected readonly items = signal<Schemas['PublicGalleryItemDto'][]>([]);
  protected readonly zoom = signal<string | null>(null);
  protected readonly zoomIndex = signal(0);
  protected readonly photos = computed(() => this.items().map((i) => imageUrl(i.image)).filter((u): u is string => u !== null));

  constructor() {
    this.select('');
  }

  protected open(src: string): void {
    this.zoomIndex.set(this.photos().indexOf(src));
    this.zoom.set(src);
  }

  protected select(value: string): void {
    this.category.set(value);
    const run = ++this.run;
    this.api.getOr<Schemas['PublicGalleryItemDto'][]>('gallery', [], value ? { category: value } : {}).subscribe((i) => {
      if (run === this.run) {
        this.items.set(i);
      }
    });
  }

  protected url(item: Schemas['PublicGalleryItemDto']): string | null {
    return imageUrl(item.image);
  }

  // BR-CNT-35: server đã kiểm địa chỉ; ở đây chặn thêm lần nữa trước khi cho vào iframe
  // Giữ nguyên một đối tượng cho mỗi địa chỉ: tạo mới mỗi lần vẽ làm iframe tải lại video đang xem
  protected safe(url: string): SafeResourceUrl {
    let safe = this.safeUrls.get(url);
    if (!safe) {
      safe = this.sanitizer.bypassSecurityTrustResourceUrl(isEmbeddable(url) ? url : 'about:blank');
      this.safeUrls.set(url, safe);
    }

    return safe;
  }
}

@Component({
  selector: 'ot-promotions',
  imports: [RouterLink, VnDatePipe],
  template: `
    <div class="ot-page ot-page-wide">
      <h1>Khuyến mãi</h1>
      @if (items().length === 0) { <div class="ot-panel mt-5 text-center"><p class="font-medium">Hiện chưa có khuyến mãi</p><p class="mt-1 text-sm text-muted">Đăng ký nhận tin ở cuối trang để biết ưu đãi mới sớm nhất.</p></div> }
      <div class="mt-4 grid grid-cols-1 gap-3 md:grid-cols-3">
        @for (p of items(); track p.slug) {
          <a class="ot-card block overflow-hidden" [routerLink]="['/khuyen-mai', p.slug]">
            @if (url(p); as src) { <img class="aspect-video w-full object-cover" loading="lazy" [src]="src" [alt]="p.title" /> }
            <div class="p-3"><h2 class="text-lg">{{ p.title }}</h2><p class="text-sm text-muted">{{ p.startDate | vnDate }} – {{ p.endDate | vnDate }}@if (p.upcoming) { · Sắp diễn ra }</p></div>
          </a>
        }
      </div>
    </div>
  `,
})
export class Promotions {
  protected readonly items = toSignal(inject(Api).getOr<Schemas['PublicPromotionDto'][]>('promotions', []), { initialValue: [] });

  protected url(p: Schemas['PublicPromotionDto']): string | null {
    return imageUrl(p.bannerImage);
  }
}

@Component({
  selector: 'ot-promotion',
  imports: [RouterLink, VnDatePipe, LucideAngularModule],
  template: `
    <article class="ot-page">
      <a class="inline-flex items-center gap-1 text-muted max-lg:hidden" routerLink="/khuyen-mai"><lucide-icon [img]="icons.back" [size]="16" />Khuyến mãi</a>
      @if (item(); as p) {
        @if (banner(); as src) { <img class="mt-3 aspect-video w-full rounded-lg object-cover" [src]="src" [alt]="p.title" /> }
        <h1 class="mt-3">{{ p.title }}</h1>
        <p class="text-sm text-muted">{{ p.startDate | vnDate }} – {{ p.endDate | vnDate }}</p>
        <div class="ot-prose" [innerHTML]="p.descriptionHtml"></div>
        @for (code of p.voucherCodes; track code) {
          <p class="mt-2 flex items-center gap-3"><b>{{ code }}</b><button type="button" class="ot-chip" (click)="copy(code)">{{ copied() === code ? 'Đã sao chép' : 'Sao chép mã' }}</button></p>
        }
      } @else if (missing()) {
        <p class="mt-3">Không tìm thấy khuyến mãi này.</p>
      }
    </article>
  `,
})
export class Promotion {
  protected readonly icons = { back: ChevronLeft };
  private readonly api = inject(Api);
  protected readonly item = signal<Schemas['PublicPromotionDto'] | null>(null);
  protected readonly missing = signal(false);
  protected readonly copied = signal<string | null>(null);
  protected readonly banner = computed(() => imageUrl(this.item()?.bannerImage));

  constructor() {
    const slug = inject(ActivatedRoute).snapshot.paramMap.get('slug') ?? '';
    const title = inject(Title);
    this.api.getOr<Schemas['PublicPromotionDto'] | null>(`promotions/${encodeURIComponent(slug)}`, null).subscribe((p) => {
      this.item.set(p);
      if (p) {
        title.setTitle(`${p.title} | Obento Tensho`);
      }

      this.missing.set(p === null);
    });
  }

  protected copy(code: string): void {
    void navigator.clipboard?.writeText(code).then(() => this.copied.set(code));
  }
}

@Component({
  selector: 'ot-news-list',
  imports: [RouterLink, VnDatePipe],
  template: `
    <div class="ot-page ot-page-wide">
      <h1>Tin tức</h1>
      <div class="ot-scroll-row mt-3">
        <a class="ot-chip" routerLink="/tin-tuc" [class.ot-chip-active]="!category()">Tất cả</a>
        @for (c of categories(); track c.id) {
          <a class="ot-chip" routerLink="/tin-tuc" [queryParams]="{ danhMuc: c.slug }" [class.ot-chip-active]="category() === c.slug">{{ c.name }}</a>
        }
      </div>
      @if (items().length === 0) { <div class="ot-panel mt-5 text-center"><p class="font-medium">Chưa có bài viết</p><p class="mt-1 text-sm text-muted">Quán sẽ đăng tin tức và sự kiện sớm.</p></div> }
      <div class="mt-4 grid grid-cols-1 gap-3 md:grid-cols-3">
        @for (n of items(); track n.slug) {
          <a class="ot-card block overflow-hidden" [routerLink]="['/tin-tuc', n.slug]">
            @if (url(n); as src) { <img class="aspect-video w-full object-cover" loading="lazy" [src]="src" [alt]="n.title" /> }
            <div class="p-3"><h2 class="text-lg">{{ n.title }}</h2><p class="text-sm text-muted">{{ n.publishedAtUtc | vnDate }}</p><p class="mt-1 text-sm">{{ n.summary }}</p></div>
          </a>
        }
      </div>
    </div>
  `,
})
export class NewsList {
  private readonly api = inject(Api);
  protected readonly categories = toSignal(this.api.getOr<Schemas['NewsCategoryDto'][]>('news/categories', []), { initialValue: [] });
  protected readonly category = signal<string | null>(null);
  protected readonly items = signal<Schemas['PublicNewsListItemDto'][]>([]);

  constructor() {
    inject(ActivatedRoute).queryParamMap
      .pipe(
        map((p) => p.get('danhMuc')),
        switchMap((c) => {
          this.category.set(c);
          return this.api.getPage<Schemas['PublicNewsListItemDto']>('news', { page: 1, pageSize: 30, ...(c ? { category: c } : {}) }).pipe(catchError(() => of({ data: [] as Schemas['PublicNewsListItemDto'][] })));
        }),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((r) => this.items.set(r.data));
  }

  protected url(n: Schemas['PublicNewsListItemDto']): string | null {
    return imageUrl(n.thumbnail);
  }
}

@Component({
  selector: 'ot-news-article',
  imports: [RouterLink, VnDatePipe, LucideAngularModule],
  template: `
    <article class="ot-page">
      <a class="inline-flex items-center gap-1 text-muted max-lg:hidden" routerLink="/tin-tuc"><lucide-icon [img]="icons.back" [size]="16" />Tin tức</a>
      @if (item(); as n) {
        <h1 class="mt-3">{{ n.title }}</h1>
        <p class="text-sm text-muted">{{ n.authorDisplayName }} · {{ n.publishedAtUtc | vnDate }}</p>
        <div class="ot-prose" [innerHTML]="n.contentHtml"></div>
      } @else if (missing()) {
        <p class="mt-3">Không tìm thấy bài viết này.</p>
      }
    </article>
  `,
})
export class NewsArticle {
  protected readonly icons = { back: ChevronLeft };
  protected readonly item = signal<Schemas['PublicNewsDto'] | null>(null);
  protected readonly missing = signal(false);

  constructor() {
    const slug = inject(ActivatedRoute).snapshot.paramMap.get('slug') ?? '';
    const title = inject(Title);
    const meta = inject(Meta);
    inject(Api).getOr<Schemas['PublicNewsDto'] | null>(`news/${encodeURIComponent(slug)}`, null).subscribe((n) => {
      this.item.set(n);
      if (n) {
        title.setTitle(`${n.metaTitle || n.title} | Obento Tensho`);
        meta.updateTag({ name: 'description', content: n.metaDescription || n.summary || n.title });
      }

      this.missing.set(n === null);
    });
  }
}

@Component({
  selector: 'ot-not-found',
  imports: [RouterLink],
  template: `<div class="ot-page"><h1>Không tìm thấy trang</h1><a class="ot-btn ot-btn-primary mt-4" routerLink="/">Về trang chủ</a></div>`,
})
export class NotFound {
  constructor() {
    const response = inject(RESPONSE_INIT, { optional: true });
    if (response) {
      response.status = 404;
    }
  }
}
