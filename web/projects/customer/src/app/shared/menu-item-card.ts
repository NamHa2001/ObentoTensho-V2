import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { LucideAngularModule, UtensilsCrossed } from 'lucide-angular';

import { VndPipe } from './vnd.pipe';

// Ảnh tỷ lệ cố định, tên đúng 2 dòng, luôn chừa dòng giá gốc: giá của các thẻ cùng hàng luôn ngang nhau
@Component({
  selector: 'ot-menu-item-card',
  imports: [RouterLink, LucideAngularModule, VndPipe],
  host: { class: 'block h-full' },
  template: `
    <a [routerLink]="['/menu', item().slug]" class="ot-card flex h-full flex-col overflow-hidden p-2 md:p-[13px]">
      <div class="relative aspect-[4/3] w-full overflow-hidden rounded-lg bg-surface-2">
        @if (imageUrl(); as url) {
          <img [src]="url" [attr.srcset]="srcset()" sizes="(max-width: 767px) 50vw, 300px" [alt]="item().name" [attr.loading]="eager() ? 'eager' : 'lazy'" [attr.fetchpriority]="eager() ? 'high' : null" decoding="async" class="absolute inset-0 size-full object-cover" />
        } @else {
          <div class="absolute inset-0 flex items-center justify-center text-muted">
            <lucide-icon [img]="placeholder" [size]="32" />
          </div>
        }
        @if (item().status === 'sold_out') {
          <span class="absolute top-2 left-2 rounded-sm bg-surface px-2 py-1 text-xs font-semibold">Hết món</span>
        } @else if (item().labels.isBestseller) {
          <span class="absolute top-2 left-2 rounded-sm bg-primary px-2 py-1 text-xs font-semibold text-on-primary">Bán chạy</span>
        }
      </div>
      <div class="flex flex-1 flex-col px-1 pt-3 pb-1 text-center">
        <h3 class="ot-title-2 text-[15px] font-semibold md:text-base">{{ item().name }}</h3>
        <div class="mt-auto pt-2">
          <p class="h-4 text-xs leading-4 text-muted line-through">
            @if (item().originalPrice; as original) {
              {{ original | vnd }}
            }
          </p>
          <p class="font-semibold text-primary">{{ item().price | vnd }}</p>
        </div>
      </div>
    </a>
  `,
})
export class MenuItemCard {
  readonly item = input.required<Schemas['PublicMenuItemDto']>();
  // Thẻ ở hàng đầu tải ảnh ngay để trang hiện nhanh
  readonly eager = input(false);
  protected readonly placeholder = UtensilsCrossed;
  protected readonly imageUrl = computed(() => this.item().image?.variants.at(-1)?.url ?? null);
  protected readonly srcset = computed(() => this.item().image?.variants.map((v) => `${v.url} ${v.width}w`).join(', ') || null);
}
