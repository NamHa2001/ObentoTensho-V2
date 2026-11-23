import { Component, computed, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { Check, LucideAngularModule, Plus, UtensilsCrossed } from 'lucide-angular';

import { AddToCart } from './add-to-cart';
import { VndPipe } from './vnd.pipe';

const ADDED_MS = 1800;

// Kiểu app đặt món: tên, mô tả ngắn, nhãn căn trái; giá và nút "+" chung một hàng sát đáy.
// Phần chữ co giãn theo nội dung, hàng giá luôn dính đáy thẻ nên giá và nút của các thẻ cùng hàng luôn ngang nhau
@Component({
  selector: 'ot-menu-item-card',
  imports: [RouterLink, LucideAngularModule, AddToCart, VndPipe],
  host: { class: 'block h-full' },
  template: `
    <div class="ot-dish ot-card flex h-full flex-col overflow-hidden p-2 md:p-3">
      <a [routerLink]="['/menu', item().slug]" class="flex flex-1 flex-col">
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
          } @else if (item().labels.isNew) {
            <span class="absolute top-2 left-2 rounded-sm bg-primary px-2 py-1 text-xs font-semibold text-on-primary">Món mới</span>
          }
          <!-- Nhãn Cay/Chay nằm trên ảnh để phần chữ của mọi thẻ cao bằng nhau, không hở trên hàng giá -->
          @if (tags().length > 0) {
            <span class="absolute top-2 right-2 flex gap-1">
              @for (t of tags(); track t) { <span class="rounded-sm bg-surface/90 px-2 py-0.5 text-xs font-semibold text-ink shadow-sm">{{ t }}</span> }
            </span>
          }
        </div>
        <div class="flex flex-1 flex-col px-1 pt-3">
          <h3 class="line-clamp-2 text-[15px] leading-snug font-semibold transition-colors md:text-base">{{ item().name }}</h3>
          <!-- Luôn chừa đủ 2 dòng mô tả: thẻ mô tả ngắn vẫn đặt giá cùng độ cao với thẻ bên cạnh -->
          <p class="mt-1 line-clamp-2 min-h-[2.75em] text-[13px] leading-[1.375] text-muted md:text-sm">{{ item().shortDesc }}</p>
        </div>
      </a>
      <div class="flex items-end justify-between gap-2 px-1 pt-3 pb-0.5">
        <div class="min-w-0">
          <p class="h-4 text-xs leading-4 text-muted line-through">
            @if (item().originalPrice; as original) {
              {{ original | vnd }}
            }
          </p>
          <p class="font-semibold text-primary tabular-nums">{{ item().price | vnd }}</p>
        </div>
        @if (unavailable(); as reason) {
          <span class="pb-1 text-xs font-semibold text-muted">{{ reason }}</span>
        } @else {
          <button type="button" class="ot-add-btn" [class.is-added]="added()" [attr.aria-label]="'Thêm ' + item().name + ' vào giỏ'" (click)="open.set(true)">
            <lucide-icon [img]="added() ? icons.check : icons.plus" [size]="18" />
          </button>
        }
      </div>
    </div>
    <!-- Đặt ngoài thẻ: thẻ nhấc lên khi rê chuột sẽ làm lệch lớp phủ nếu bảng nằm bên trong -->
    @if (open()) {
      <ot-add-to-cart [item]="item()" (added)="onAdded()" (closed)="open.set(false)" />
    }
  `,
})
export class MenuItemCard {
  readonly item = input.required<Schemas['PublicMenuItemDto']>();
  // Thẻ ở hàng đầu tải ảnh ngay để trang hiện nhanh
  readonly eager = input(false);
  protected readonly placeholder = UtensilsCrossed;
  protected readonly icons = { plus: Plus, check: Check };
  protected readonly open = signal(false);
  protected readonly added = signal(false);
  protected readonly imageUrl = computed(() => this.item().image?.variants.at(-1)?.url ?? null);
  protected readonly srcset = computed(() => this.item().image?.variants.map((v) => `${v.url} ${v.width}w`).join(', ') || null);
  protected readonly tags = computed(() => {
    const l = this.item().labels;
    return [l.isSpicy ? 'Cay' : '', l.isVegetarian ? 'Chay' : ''].filter(Boolean);
  });
  protected readonly unavailable = computed(() => {
    const item = this.item();
    return item.status !== 'available' ? 'Hết món' : item.allowTakeaway ? null : 'Chỉ tại quán';
  });
  private timer: ReturnType<typeof setTimeout> | undefined;

  // Thêm xong: đóng bảng, nút "+" thành dấu tích một lúc để khách biết đã vào giỏ
  protected onAdded(): void {
    this.open.set(false);
    this.added.set(true);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.added.set(false), ADDED_MS);
  }
}
