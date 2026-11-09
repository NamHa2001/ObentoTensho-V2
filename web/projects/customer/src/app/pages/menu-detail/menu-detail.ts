import { Component, computed, DestroyRef, inject, RESPONSE_INIT, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { Check, ChevronLeft, LucideAngularModule, Minus, Plus, ShoppingBag, Star, UtensilsCrossed } from 'lucide-angular';
import { catchError, map, of, switchMap, tap } from 'rxjs';

import { Api } from '../../core/api';
import { Cart, MAX_QUANTITY } from '../../core/cart';
import { Lightbox } from '../../shared/lightbox';
import { MenuItemCard } from '../../shared/menu-item-card';
import { SectionTitle } from '../../shared/section-title';
import { VndPipe } from '../../shared/vnd.pipe';
import { defaultSelection, isComplete, isOrderable, requiredCount, Selection, toggle, unitPrice } from './option-selection';

type Detail = Schemas['PublicMenuItemDetailDto'];
type Group = Schemas['PublicOptionGroupDto'];
type Option = Schemas['PublicOptionDto'];
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
  imports: [RouterLink, LucideAngularModule, Lightbox, MenuItemCard, SectionTitle, VndPipe],
  templateUrl: './menu-detail.html',
})
export class MenuDetail {
  private readonly api = inject(Api);
  private readonly cart = inject(Cart);
  private readonly title = inject(Title);

  protected readonly icons = { back: ChevronLeft, minus: Minus, plus: Plus, bag: ShoppingBag, check: Check, star: Star, photo: UtensilsCrossed };
  protected readonly maxQuantity = MAX_QUANTITY;
  protected readonly stars = [1, 2, 3, 4, 5];
  protected readonly detail = signal<Detail | null>(null);
  protected readonly state = signal<'loading' | 'ready' | 'missing' | 'failed'>('loading');
  protected readonly reviews = signal<Review[]>([]);
  protected readonly selection = signal<Selection>({});
  protected readonly quantity = signal(1);
  protected readonly activeImage = signal(0);
  protected readonly added = signal(false);
  protected readonly zoom = signal(false);
  protected readonly zoomImages = computed(() => this.gallery().map((i) => i.variants.at(-1)?.url ?? ''));

  protected readonly gallery = computed(() => {
    const d = this.detail();
    if (!d) {
      return [];
    }

    return d.images.length > 0 ? d.images : d.item.image ? [d.item.image] : [];
  });
  protected readonly current = computed(() => this.gallery()[this.activeImage()] ?? null);
  protected readonly unit = computed(() => {
    const d = this.detail();
    return d ? unitPrice(d.item.price, this.selection(), d.optionGroups) : 0;
  });
  protected readonly total = computed(() => this.unit() * this.quantity());
  protected readonly complete = computed(() => isComplete(this.selection(), this.detail()?.optionGroups ?? []));
  protected readonly unavailable = computed(() => {
    const item = this.detail()?.item;
    if (!item) {
      return null;
    }

    return item.status !== 'available' ? 'Món đang tạm hết' : item.allowTakeaway ? null : 'Món chỉ phục vụ tại quán';
  });
  protected readonly canAdd = computed(() => this.complete() && !this.unavailable());

  constructor() {
    const route = inject(ActivatedRoute);
    const response = inject(RESPONSE_INIT, { optional: true });
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

        this.selection.set(defaultSelection(detail.optionGroups));
        this.quantity.set(1);
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

  protected selected(group: Group, option: Option): boolean {
    return this.selection()[group.id]?.includes(option.id) ?? false;
  }

  protected disabled(group: Group, option: Option): boolean {
    if (!isOrderable(option)) {
      return true;
    }

    return group.selectionType === 'many' && !this.selected(group, option) && (this.selection()[group.id]?.length ?? 0) >= group.maxSelect;
  }

  protected rule(group: Group): string {
    if (group.selectionType === 'one') {
      return group.isRequired ? 'Bắt buộc · chọn 1' : 'Không bắt buộc · chọn 1';
    }

    const min = requiredCount(group);
    return min > 0 ? `Bắt buộc · chọn ${min}–${group.maxSelect}` : `Không bắt buộc · tối đa ${group.maxSelect}`;
  }

  protected choose(group: Group, option: Option): void {
    this.selection.update((s) => toggle(s, group, option.id));
    this.added.set(false);
  }

  protected changeQuantity(delta: number): void {
    this.quantity.update((q) => Math.min(MAX_QUANTITY, Math.max(1, q + delta)));
    this.added.set(false);
  }

  protected add(): void {
    const d = this.detail();
    if (!d || !this.canAdd()) {
      return;
    }

    this.cart.add(d.item.id, Object.values(this.selection()).flat(), this.quantity());
    this.added.set(true);
  }
}
