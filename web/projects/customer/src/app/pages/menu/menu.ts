import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { ChevronDown, LucideAngularModule, Search, X } from 'lucide-angular';
import { catchError, debounceTime, map, merge, of, Subject, switchMap, tap } from 'rxjs';

import { Api, Page } from '../../core/api';
import { MenuItemCard } from '../../shared/menu-item-card';

type Item = Schemas['PublicMenuItemDto'];

interface Filter {
  category: string | null;
  search: string;
  sort: string;
  without: string;
}

const PAGE_SIZE = 12;

// docs/srs/30 §4: bộ lọc nằm trên địa chỉ trang nên chia sẻ link và nút quay lại giữ nguyên kết quả
@Component({
  selector: 'ot-menu',
  imports: [RouterLink, LucideAngularModule, MenuItemCard],
  templateUrl: './menu.html',
})
export class Menu {
  private readonly api = inject(Api);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly typed = new Subject<string>();
  private page = 1;
  private generation = 0;
  private readonly retry = new Subject<void>();

  protected readonly icons = { search: Search, clear: X, down: ChevronDown };
  protected readonly skeletons = Array.from({ length: 8 }, (_, i) => i);
  protected readonly sorts = [
    { value: '', label: 'Mặc định' },
    { value: 'price', label: 'Giá tăng dần' },
    { value: '-price', label: 'Giá giảm dần' },
    { value: 'name', label: 'Tên A–Z' },
    { value: 'new', label: 'Món mới' },
  ];
  protected readonly allergens = [{ value: 'egg', label: 'Trứng' }, { value: 'milk', label: 'Sữa' }, { value: 'soy', label: 'Đậu nành' }, { value: 'wheat', label: 'Lúa mì' }, { value: 'shellfish', label: 'Hải sản có vỏ' }, { value: 'fish', label: 'Cá' }, { value: 'peanut', label: 'Đậu phộng' }, { value: 'tree_nut', label: 'Các loại hạt' }, { value: 'sesame', label: 'Mè' }];
  protected readonly categories = toSignal(this.api.getOr<Schemas['PublicCategoryDto'][]>('menu/categories', []), { initialValue: [] });
  protected readonly filter = signal<Filter>({ category: null, search: '', sort: '', without: '' });
  protected readonly items = signal<Item[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly loadingMore = signal(false);
  protected readonly failed = signal(false);

  constructor() {
    merge(this.route.queryParamMap.pipe(map((params) => this.parse(params))), this.retry.pipe(map(() => this.filter())))
      .pipe(
        tap((filter) => {
          this.filter.set(filter);
          this.page = 1;
          this.generation += 1;
          this.loadingMore.set(false);
          this.loading.set(true);
          this.failed.set(false);
        }),
        switchMap((filter) => this.fetch(filter, 1)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.items.set(result?.data ?? []);
        this.total.set(result?.meta.total ?? 0);
        this.failed.set(result === null);
        this.loading.set(false);
      });

    this.typed
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe((search) => this.navigate({ tim: search.trim() || null }));
  }

  protected onSearch(value: string): void {
    this.typed.next(value);
  }

  protected clearSearch(input: HTMLInputElement): void {
    input.value = '';
    input.focus();
    this.typed.next('');
  }

  protected onWithout(value: string): void {
    this.navigate({ khongCo: value || null });
  }

  protected onSort(value: string): void {
    this.navigate({ sapXep: value || null });
  }

  protected loadMore(): void {
    // Đổi bộ lọc trong lúc đang tải thì bỏ kết quả cũ, không nối vào danh sách mới
    const generation = this.generation;
    this.loadingMore.set(true);
    const next = this.page + 1;
    this.fetch(this.filter(), next)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (generation !== this.generation) {
          return;
        }

        if (result) {
          this.page = next;
          this.items.update((items) => [...items, ...result.data.filter((i) => !items.some((x) => x.id === i.id))]);
          this.total.set(result.meta.total);
        }

        this.loadingMore.set(false);
      });
  }

  protected reload(): void {
    this.retry.next();
  }

  private parse(params: ParamMap): Filter {
    return { category: params.get('danhMuc'), search: params.get('tim') ?? '', sort: params.get('sapXep') ?? '', without: params.get('khongCo') ?? '' };
  }

  private navigate(queryParams: Record<string, string | null>): void {
    this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge', replaceUrl: true });
  }

  private fetch(filter: Filter, page: number) {
    const params: Record<string, string | number> = { page, pageSize: PAGE_SIZE };
    if (filter.category) {
      params['category'] = filter.category;
    }

    if (filter.search) {
      params['q'] = filter.search;
    }

    if (filter.without) {
      params['excludeAllergens'] = filter.without;
    }

    if (filter.sort) {
      params['sort'] = filter.sort;
    }

    return this.api.getPage<Item>('menu/items', params).pipe(catchError(() => of(null as Page<Item> | null)));
  }
}
