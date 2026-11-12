import { Component, computed, DestroyRef, inject, RESPONSE_INIT, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { Check, ChevronLeft, LucideAngularModule, ShoppingBag, Star, UtensilsCrossed } from 'lucide-angular';
import { catchError, map, of, switchMap, tap } from 'rxjs';

import { Api } from '../../core/api';
import { AddToCart } from '../../shared/add-to-cart';
import { Lightbox } from '../../shared/lightbox';
import { MenuItemCard } from '../../shared/menu-item-card';
import { SectionTitle } from '../../shared/section-title';
import { VndPipe } from '../../shared/vnd.pipe';

type Detail = Schemas['PublicMenuItemDetailDto'];
type Review = Schemas['PublicReviewDto'];

const ALLERGENS: Record<string, string> = {
  egg: 'Trứng',
  milk: 'Sữa',
  soy: 'Đậu nành',
  wheat: 'Lúa mì',
  shellfish: 'Hải sản có vỏ',
  fish: 'Cá',
  peanut: 'Đậu phộng',
  tree_nut: 'Các loại hạt',
  sesame: 'Mè',
};

@Component({
  selector: 'ot-menu-detail',
  imports: [RouterLink, LucideAngularModule, AddToCart, Lightbox, MenuItemCard, SectionTitle, VndPipe],
  templateUrl: './menu-detail.html',
})
export class MenuDetail {
  private readonly api = inject(Api);
  private readonly title = inject(Title);

  protected readonly icons = { back: ChevronLeft, bag: ShoppingBag, check: Check, star: Star, photo: UtensilsCrossed };
  protected readonly stars = [1, 2, 3, 4, 5];
  protected readonly detail = signal<Detail | null>(null);
  protected readonly state = signal<'loading' | 'ready' | 'missing' | 'failed'>('loading');
  protected readonly reviews = signal<Review[]>([]);
  protected readonly activeImage = signal(0);
  protected readonly added = signal(false);
  protected readonly zoom = signal(false);
  protected readonly picker = signal(false);
  protected readonly zoomImages = computed(() => this.gallery().map((i) => i.variants.at(-1)?.url ?? ''));

  protected readonly gallery = computed(() => {
    const d = this.detail();
    if (!d) {
      return [];
    }

    return d.images.length > 0 ? d.images : d.item.image ? [d.item.image] : [];
  });
  protected readonly current = computed(() => this.gallery()[this.activeImage()] ?? null);
  protected readonly unavailable = computed(() => {
    const item = this.detail()?.item;
    if (!item) {
      return null;
    }

    return item.status !== 'available' ? 'Món đang tạm hết' : item.allowTakeaway ? null : 'Món chỉ phục vụ tại quán';
  });

  constructor() {
    const route = inject(ActivatedRoute);
    const response = inject(RESPONSE_INIT, { optional: true });
    this.title.setTitle('Món ăn — Obento Tensho');
    route.paramMap
      .pipe(
        map((params) => params.get('slug') ?? ''),
        tap(() => {
          this.state.set('loading');
          this.reviews.set([]);
        }),
        switchMap((slug) =>
          this.api.get<Detail>(`menu/items/${encodeURIComponent(slug)}`).pipe(
            map((detail) => ({ detail, slug, error: 0 })),
            catchError((e: { status?: number }) => of({ detail: null, slug, error: e.status ?? 500 })),
          ),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe(({ detail, slug, error }) => {
        this.detail.set(detail);
        if (!detail) {
          this.state.set(error === 404 ? 'missing' : 'failed');
          if (response && error === 404) {
            response.status = 404;
          }

          return;
        }

        this.activeImage.set(0);
        this.added.set(false);
        this.state.set('ready');
        this.title.setTitle(`${detail.item.name} — Obento Tensho`);
        this.api.getOr<Review[]>(`menu/items/${encodeURIComponent(slug)}/reviews`, [], { page: 1, pageSize: 5 }).subscribe((r) => this.reviews.set(r));
      });
  }

  protected allergen(code: string): string {
    return ALLERGENS[code] ?? code;
  }

  protected imageUrl(image: Schemas['ImageDto']): string | null {
    return image.variants.at(-1)?.url ?? null;
  }

  protected srcset(image: Schemas['ImageDto']): string {
    return image.variants.map((v) => `${v.url} ${v.width}w`).join(', ');
  }

  // Trang này để xem; bấm thêm mới mở bảng chọn tùy chọn, ghi chú và số lượng
  protected openPicker(): void {
    if (this.detail() && !this.unavailable()) {
      this.picker.set(true);
    }
  }

  protected onAdded(): void {
    this.picker.set(false);
    this.added.set(true);
  }
}
