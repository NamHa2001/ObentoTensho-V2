import { isPlatformBrowser } from '@angular/common';
import { afterNextRender, Component, computed, DestroyRef, effect, HostListener, inject, Injector, PLATFORM_ID, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { ChevronDown, LucideAngularModule, Search, X } from 'lucide-angular';
import { catchError, debounceTime, forkJoin, map, merge, Observable, of, Subject, switchMap, tap } from 'rxjs';

import { Api, Page } from '../../core/api';
import { MenuItemCard } from '../../shared/menu-item-card';

type Item = Schemas['PublicMenuItemDto'];
type Category = Schemas['PublicCategoryDto'];

interface Filter {
  category: string | null;
  search: string;
  sort: string;
  without: string;
}

const PAGE_SIZE = 12;

// Xem cả thực đơn: tải hết món một lần (trần 100 món mỗi trang của API) để chia nhóm theo danh mục
const ALL_PAGE_SIZE = 100;

// docs/srs/30 §4: bộ lọc nằm trên địa chỉ trang nên chia sẻ link và nút quay lại giữ nguyên kết quả.
// Chủ quán 2026-11-14: khi không tìm/sắp xếp, mọi món hiện theo nhóm danh mục; thanh danh mục dính trên đầu (điện thoại)
// hoặc cột trái (máy tính), bấm thì cuộn tới nhóm, kéo trang thì danh mục đang xem tự sáng
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
  private readonly injector = inject(Injector);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly typed = new Subject<string>();
  private page = 1;
  private generation = 0;
  private readonly retry = new Subject<void>();
  private spyFrame = 0;

  // Cuộn tới nhóm do bấm danh mục: tạm ngừng theo dõi để nút không nhảy qua các nhóm ở giữa
  private jumpingUntil = 0;

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
  protected readonly categories = toSignal(this.api.getOr<Category[]>('menu/categories', []), { initialValue: [] });
  protected readonly filter = signal<Filter>({ category: null, search: '', sort: '', without: '' });
  protected readonly items = signal<Item[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly loadingMore = signal(false);
  protected readonly failed = signal(false);
  protected readonly active = signal<string | null>(null);

  // Chia nhóm khi xem cả thực đơn; đang tìm hoặc sắp xếp thì giữ một danh sách phẳng
  protected readonly grouped = computed(() => !this.filter().search && !this.filter().sort);
  protected readonly sections = computed(() => {
    const byCategory = new Map<number, Item[]>();
    for (const item of this.items()) {
      byCategory.set(item.categoryId, [...(byCategory.get(item.categoryId) ?? []), item]);
    }

    return this.categories()
      .filter((c) => byCategory.has(c.id))
      .map((c) => ({ slug: c.slug, name: c.name, items: byCategory.get(c.id) ?? [] }));
  });

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
        switchMap((filter) => (this.grouped() ? this.fetchAll(filter) : this.fetch(filter, 1))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.items.set(result?.data ?? []);
        this.total.set(result?.meta.total ?? 0);
        this.failed.set(result === null);
        this.loading.set(false);
        if (this.browser && this.grouped()) {
          // Chờ các nhóm món vẽ xong mới đo vị trí để cuộn tới danh mục
          afterNextRender(() => (this.filter().category ? this.jump(this.filter().category!) : this.spy()), { injector: this.injector });
        }
      });

    this.typed
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe((search) => this.navigate({ tim: search.trim() || null }));

    // Danh mục đang xem luôn nằm trong tầm mắt trên hàng cuộn ngang
    effect(() => {
      const slug = this.active();
      if (this.browser && slug) {
        untracked(() => document.getElementById(`dm-chip-${slug}`)?.scrollIntoView({ inline: 'center', block: 'nearest' }));
      }
    });
  }

  @HostListener('window:scroll')
  protected onScroll(): void {
    if (this.browser && this.grouped() && !this.spyFrame) {
      this.spyFrame = requestAnimationFrame(() => {
        this.spyFrame = 0;
        this.spy();
      });
    }
  }

  // Bấm danh mục: đang xem cả thực đơn thì cuộn tới nhóm; đang tìm/sắp xếp thì bỏ tìm và mở đúng nhóm đó
  protected pick(slug: string | null): void {
    if (!this.grouped()) {
      this.navigate({ tim: null, sapXep: null, danhMuc: slug });
      return;
    }

    if (slug) {
      this.jump(slug);
    } else {
      this.active.set(null);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
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

  private jump(slug: string): void {
    const section = document.getElementById(`dm-${slug}`);
    if (section) {
      this.active.set(slug);
      this.jumpingUntil = Date.now() + 800;
      section.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  // Nhóm đang xem = nhóm cuối cùng có tiêu đề đã chạm (hoặc qua) mép dưới thanh danh mục dính
  private spy(): void {
    if (Date.now() < this.jumpingUntil) {
      return;
    }

    // Điện thoại: mép dưới hàng danh mục dính; máy tính: mép dưới thanh tiêu đề
    const nav = document.querySelector('.ot-category-nav');
    const header = document.querySelector('ot-site-header');
    const edge = (window.innerWidth < 1024 && nav ? nav.getBoundingClientRect().bottom : (header?.getBoundingClientRect().bottom ?? 0) + 16) + 8;
    let current: string | null = null;
    for (const section of this.sections()) {
      const top = document.getElementById(`dm-${section.slug}`)?.getBoundingClientRect().top ?? Infinity;
      if (top <= edge) {
        current = section.slug;
      }
    }

    this.active.set(current ?? this.sections()[0]?.slug ?? null);
  }

  private parse(params: ParamMap): Filter {
    return { category: params.get('danhMuc'), search: params.get('tim') ?? '', sort: params.get('sapXep') ?? '', without: params.get('khongCo') ?? '' };
  }

  private navigate(queryParams: Record<string, string | null>): void {
    this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge', replaceUrl: true });
  }

  // Cả thực đơn (chia nhóm): đọc hết các trang 100 món, bỏ qua lọc danh mục (danh mục chỉ để cuộn tới)
  private fetchAll(filter: Filter): Observable<Page<Item> | null> {
    const first = this.fetch({ ...filter, category: null }, 1, ALL_PAGE_SIZE);
    return first.pipe(
      switchMap((page) => {
        if (!page || page.meta.total <= page.data.length) {
          return of(page);
        }

        const rest = Array.from({ length: Math.ceil(page.meta.total / ALL_PAGE_SIZE) - 1 }, (_, i) => this.fetch({ ...filter, category: null }, i + 2, ALL_PAGE_SIZE));
        return forkJoin(rest).pipe(map((pages) => ({ ...page, data: [...page.data, ...pages.flatMap((p) => p?.data ?? [])] })));
      }),
    );
  }

  private fetch(filter: Filter, page: number, pageSize = PAGE_SIZE) {
    const params: Record<string, string | number> = { page, pageSize };
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
