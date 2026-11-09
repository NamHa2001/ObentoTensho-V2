import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { LucideAngularModule } from 'lucide-angular';

import { Cart } from '../core/cart';
import { TAB_ITEMS } from './nav-items';

// Chỉ hiện trên điện thoại; chừa vùng thanh vuốt của iPhone/Android bằng safe-area
@Component({
  selector: 'ot-bottom-nav',
  imports: [RouterLink, RouterLinkActive, LucideAngularModule],
  host: { class: 'block md:hidden' },
  template: `
    <div style="height: calc(var(--ot-tabbar-h) + var(--ot-safe-bottom))"></div>
    <nav
      class="fixed inset-x-0 bottom-0 z-30 grid grid-cols-5 border-t border-line bg-surface"
      style="padding-bottom: var(--ot-safe-bottom); padding-left: var(--ot-safe-left); padding-right: var(--ot-safe-right)"
      aria-label="Điều hướng nhanh"
    >
      @for (item of items; track item.path) {
        <a
          [routerLink]="item.path"
          routerLinkActive="text-primary"
          [routerLinkActiveOptions]="{ exact: item.exact ?? false }"
          class="flex flex-col items-center justify-center gap-1 text-muted"
          style="height: var(--ot-tabbar-h)"
        >
          <span class="relative flex">
            <lucide-icon [img]="item.icon" [size]="22" />
            @if (item.path === '/gio-hang' && cart.count() > 0) {
              <span class="absolute -top-1.5 -right-2.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-primary px-1 text-[10px] leading-none font-bold text-on-primary">{{ cart.count() }}</span>
            }
          </span>
          <span class="text-[11px] leading-none font-medium whitespace-nowrap">{{ item.label }}</span>
        </a>
      }
    </nav>
  `,
})
export class BottomNav {
  protected readonly items = TAB_ITEMS;
  protected readonly cart = inject(Cart);
}
