import { computed, Directive, HostListener, inject, signal } from '@angular/core';
import { Schemas } from '@ot/api';
import { map } from 'rxjs';

import { Api } from '../core/api';
import { Submit } from './ui';

type MenuItem = Schemas['PublicMenuItemDto'];
type OptionGroup = Schemas['PublicOptionGroupDto'];

// Bảng chọn tùy chọn cho một món trước khi thêm vào danh sách đang chọn
export interface Picking {
  item: MenuItem;
  groups: OptionGroup[];
  selected: Set<number>;
  quantity: number;
}

export interface DraftLine {
  key: number;
  menuItemId: number;
  name: string;
  unitPrice: number;
  quantity: number;
  optionIds: number[];
  options: string[];
  note: string;
}

// Tìm món, danh mục dính khi cuộn, lưới món; dùng chung cho phiếu bàn và nhập đơn app
export const POS_MENU = `
            <input class="ot-input" name="q" placeholder="Tìm món" [ngModel]="query()" (ngModelChange)="query.set($event)" />
            <!-- Danh mục dính khi cuộn: hàng ngang trên điện thoại, cột trái từ máy tính bảng; bấm thì cuộn tới nhóm -->
            <div class="a-pos-menu">
              @if (!query().trim()) {
                <nav class="a-pos-cats" aria-label="Danh mục">
                  @for (g of groups(); track g.id) {
                    <button type="button" class="ot-chip" [id]="'pos-chip-' + g.id" [class.ot-chip-active]="activeCat() === g.id" (click)="jumpCat(g.id)">{{ g.name }}</button>
                  }
                </nav>
              }
              <div class="min-w-0">
                @if (query().trim()) {
                  <div class="grid grid-cols-2 gap-2 sm:grid-cols-3 2xl:grid-cols-4">
                    @for (m of shown(); track m.id) {
                      <button type="button" class="a-dine-item" [disabled]="m.status !== 'available'" (click)="choose(m)">
                        <span class="font-medium">{{ m.name }}</span>
                        <span class="text-sm text-muted">{{ m.status === 'available' ? (m.price | vnd) : 'Hết món' }}</span>
                      </button>
                    } @empty { <p class="text-muted">Không có món phù hợp.</p> }
                  </div>
                } @else {
                  @for (g of groups(); track g.id) {
                    <section class="a-pos-section" [id]="'pos-cat-' + g.id">
                      <h3 class="mb-2 font-medium">{{ g.name }}</h3>
                      <div class="grid grid-cols-2 gap-2 sm:grid-cols-3 2xl:grid-cols-4">
                        @for (m of g.items; track m.id) {
                          <button type="button" class="a-dine-item" [disabled]="m.status !== 'available'" (click)="choose(m)">
                            <span class="font-medium">{{ m.name }}</span>
                            <span class="text-sm text-muted">{{ m.status === 'available' ? (m.price | vnd) : 'Hết món' }}</span>
                          </button>
                        }
                      </div>
                    </section>
                  } @empty { <p class="text-muted">Chưa có món.</p> }
                }
              </div>
            </div>`;

// Bảng chọn tùy chọn và số lượng của một món
export const POS_PICKER = `
    @if (picking(); as p) {
      <div class="a-drawer" role="dialog" aria-modal="true">
        <form class="a-drawer-panel" (ngSubmit)="addPicked(p)">
          <div class="a-drawer-h">{{ p.item.name }}</div>
          <div class="a-drawer-b">
            @for (g of p.groups; track g.id) {
              <fieldset>
                <legend>{{ g.name }} <small class="font-normal text-muted">{{ g.isRequired ? 'bắt buộc' : 'không bắt buộc' }}{{ g.selectionType === 'many' ? ', tối đa ' + g.maxSelect : '' }}</small></legend>
                @for (o of g.options; track o.id) {
                  <label class="flex items-center gap-2 font-normal">
                    <input [type]="g.selectionType === 'one' ? 'radio' : 'checkbox'" [name]="'g' + g.id" [disabled]="o.status !== 'available'" [checked]="p.selected.has(o.id)" (change)="toggle(p, g, o.id)" />
                    {{ o.name }}@if (o.priceDelta) { <small class="text-muted">+{{ o.priceDelta | vnd }}</small> }@if (o.status !== 'available') { <small class="text-muted">(hết)</small> }
                  </label>
                }
              </fieldset>
            }
            <label>Số lượng<input class="ot-input" name="qty" type="number" min="1" max="99" [(ngModel)]="p.quantity" /></label>
            @if (pickError(); as e) { <p class="ot-error">{{ e }}</p> }
          </div>
          <div class="a-drawer-f">
            <button type="button" class="ot-btn ot-btn-outline" (click)="picking.set(null)">Đóng</button>
            <button class="ot-btn ot-btn-primary">Thêm</button>
          </div>
        </form>
      </div>
    }`;

// Danh sách món đang chọn: tăng giảm số lượng, ghi chú cho bếp
export const POS_DRAFT_LINES = `
                @for (d of draft(); track d.key) {
                  <div class="flex flex-wrap items-center gap-2 border-b border-line py-2 last:border-0">
                    <div class="min-w-0 flex-1"><b>{{ d.name }}</b>@for (o of d.options; track $index) { <small class="block text-muted">{{ o }}</small> }</div>
                    <div class="flex items-center gap-1">
                      <button type="button" class="ot-chip" aria-label="Bớt" (click)="bump(d, -1)"><lucide-icon [img]="icons.minus" [size]="18" /></button>
                      <span class="w-6 text-center">{{ d.quantity }}</span>
                      <button type="button" class="ot-chip" aria-label="Thêm" (click)="bump(d, 1)"><lucide-icon [img]="icons.plus" [size]="18" /></button>
                    </div>
                    <span class="w-24 text-right">{{ d.unitPrice * d.quantity | vnd }}</span>
                    <input class="ot-input basis-full" [name]="'note' + d.key" maxlength="200" placeholder="Ghi chú cho bếp, ví dụ: ít cay" [(ngModel)]="d.note" />
                  </div>
                }`;

@Directive()
export abstract class PosMenu {
  protected readonly api = inject(Api);
  protected abstract readonly form: Submit;
  private readonly details = new Map<string, OptionGroup[]>();
  private nextKey = 1;
  private spyFrame = 0;
  private jumpingUntil = 0;
  protected readonly menu = signal<MenuItem[]>([]);
  protected readonly categories = signal<Schemas['PublicCategoryDto'][]>([]);
  protected readonly activeCat = signal<number | null>(null);
  protected readonly query = signal('');
  protected readonly draft = signal<DraftLine[]>([]);
  protected readonly picking = signal<Picking | null>(null);
  protected readonly pickError = signal<string | null>(null);
  protected readonly shown = computed(() => {
    const q = this.query().trim().toLowerCase();
    return this.menu().filter((m) => !q || m.name.toLowerCase().includes(q));
  });
  // Món chia nhóm theo thứ tự danh mục của thực đơn
  protected readonly groups = computed(() =>
    this.categories()
      .map((c) => ({ id: c.id, name: c.name, items: this.menu().filter((m) => m.categoryId === c.id) }))
      .filter((g) => g.items.length > 0),
  );
  protected readonly draftCount = computed(() => this.draft().reduce((n, d) => n + d.quantity, 0));
  protected readonly draftTotal = computed(() => this.draft().reduce((n, d) => n + d.unitPrice * d.quantity, 0));

  protected loadMenu(): void {
    this.api.publicGet<Schemas['PublicCategoryDto'][]>('menu/categories').subscribe({ next: (c) => this.categories.set(c), error: () => undefined });
    this.api.publicGet<MenuItem[]>('menu/items', { pageSize: 100 }).subscribe({ next: (m) => this.menu.set(m), error: () => this.form.error.set('Không tải được thực đơn.') });
  }

  protected draftItems(): { menuItemId: number; quantity: number; optionIds: number[]; note: string | null }[] {
    return this.draft().map((d) => ({ menuItemId: d.menuItemId, quantity: d.quantity, optionIds: d.optionIds, note: d.note.trim() || null }));
  }

  protected jumpCat(id: number): void {
    this.activeCat.set(id);
    this.jumpingUntil = Date.now() + 800;
    document.getElementById(`pos-chip-${id}`)?.scrollIntoView({ inline: 'center', block: 'nearest' });
    document.getElementById(`pos-cat-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  protected showDraft(): void {
    document.getElementById('pos-draft')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // Nhóm đang xem = nhóm cuối có tiêu đề đã chạm mép dưới thanh danh mục dính
  @HostListener('window:scroll')
  protected onScroll(): void {
    if (this.spyFrame || Date.now() < this.jumpingUntil || this.query().trim()) {
      return;
    }

    this.spyFrame = requestAnimationFrame(() => {
      this.spyFrame = 0;
      const nav = document.querySelector('.a-pos-cats');
      const edge = (window.innerWidth < 768 && nav ? nav.getBoundingClientRect().bottom : 86) + 8;
      let current: number | null = null;
      for (const g of this.groups()) {
        if ((document.getElementById(`pos-cat-${g.id}`)?.getBoundingClientRect().top ?? Infinity) <= edge) {
          current = g.id;
        }
      }

      const next = current ?? this.groups()[0]?.id ?? null;
      if (next !== this.activeCat()) {
        this.activeCat.set(next);
        document.getElementById(`pos-chip-${next}`)?.scrollIntoView({ inline: 'center', block: 'nearest' });
      }
    });
  }

  // Món không có tùy chọn thì thêm luôn; có thì mở bảng chọn
  protected choose(m: MenuItem): void {
    const cached = this.details.get(m.slug);
    if (cached) {
      this.pick(m, cached);
      return;
    }

    this.form.run(this.api.publicGet<Schemas['PublicMenuItemDetailDto']>(`menu/items/${m.slug}`).pipe(map((d) => d.optionGroups)), (groups) => {
      this.details.set(m.slug, groups);
      this.pick(m, groups);
    });
  }

  private pick(m: MenuItem, groups: OptionGroup[]): void {
    if (groups.length === 0) {
      this.addDraft(m, [], [], 1);
      return;
    }

    const selected = new Set<number>(groups.flatMap((g) => g.options.filter((o) => o.isDefault && o.status === 'available').map((o) => o.id)));
    this.pickError.set(null);
    this.picking.set({ item: m, groups, selected, quantity: 1 });
  }

  protected toggle(p: Picking, g: OptionGroup, optionId: number): void {
    if (g.selectionType === 'one') {
      for (const o of g.options) {
        p.selected.delete(o.id);
      }

      p.selected.add(optionId);
    } else if (p.selected.has(optionId)) {
      p.selected.delete(optionId);
    } else {
      p.selected.add(optionId);
    }
  }

  protected addPicked(p: Picking): void {
    for (const g of p.groups) {
      const count = g.options.filter((o) => p.selected.has(o.id)).length;
      if (count < g.minSelect || count > g.maxSelect) {
        this.pickError.set(`Nhóm "${g.name}" cần chọn ${g.minSelect === g.maxSelect ? g.minSelect : `${g.minSelect}–${g.maxSelect}`} lựa chọn.`);
        return;
      }
    }

    const chosen = p.groups.flatMap((g) => g.options.filter((o) => p.selected.has(o.id)).map((o) => ({ g, o })));
    this.addDraft(
      p.item,
      chosen.map((x) => x.o.id),
      chosen.map((x) => `${x.g.name}: ${x.o.name}`),
      Math.min(99, Math.max(1, Number(p.quantity) || 1)),
      chosen.reduce((sum, x) => sum + x.o.priceDelta, 0),
    );
    this.picking.set(null);
  }

  private addDraft(m: MenuItem, optionIds: number[], options: string[], quantity: number, optionsPrice = 0): void {
    const same = this.draft().find((d) => d.menuItemId === m.id && d.optionIds.join() === optionIds.join() && !d.note);
    if (same) {
      this.bump(same, quantity);
      return;
    }

    this.draft.update((d) => [...d, { key: this.nextKey++, menuItemId: m.id, name: m.name, unitPrice: m.price + optionsPrice, quantity, optionIds, options, note: '' }]);
  }

  protected bump(d: DraftLine, delta: number): void {
    this.draft.update((all) => all.map((x) => (x.key === d.key ? { ...x, quantity: Math.min(99, x.quantity + delta) } : x)).filter((x) => x.quantity > 0));
  }
}
