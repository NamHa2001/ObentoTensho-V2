import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { LucideAngularModule, Menu, ShoppingBag, User, X } from 'lucide-angular';

import { map } from 'rxjs';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { Cart } from '../core/cart';
import { givenName } from '../shared/format';
import { HEADER_LINKS } from './nav-items';

@Component({
  selector: 'ot-site-header',
  imports: [RouterLink, RouterLinkActive, LucideAngularModule],
  host: { class: 'sticky top-0 z-30 block rounded-b-lg bg-surface shadow-[var(--ot-shadow-1)]', style: 'padding-top: var(--ot-safe-top)' },
  template: `
    <!-- Máy tính: ba cột, hai bên bằng nhau nên menu luôn nằm đúng giữa trang -->
    <div class="ot-container flex items-center justify-between gap-4 lg:grid lg:grid-cols-[1fr_auto_1fr] lg:gap-6" style="height: var(--ot-header-h)">
      <a routerLink="/" class="flex min-w-0 items-center gap-2 justify-self-start md:gap-3">
        @if (logo(); as url) { <img class="size-10 rounded-full object-cover md:size-14 lg:size-10 xl:size-14" [src]="url" alt="" /> } @else { <span class="flex size-10 shrink-0 items-center justify-center rounded-full bg-primary font-serif text-2xl leading-none font-bold text-on-primary md:size-14 md:text-[34px]" aria-hidden="true">天</span> }
        <span class="font-heading truncate text-xl font-extrabold text-primary max-[319px]:text-base lg:max-xl:text-lg xl:text-[26px]">Obento Tensho</span>
      </a>

      <nav class="hidden items-center gap-1 lg:flex xl:gap-2" aria-label="Điều hướng chính">
        @for (link of links; track link.path) {
          <a [routerLink]="link.path" routerLinkActive="bg-primary text-on-primary" class="flex h-10 items-center rounded-full px-3.5 text-[15px] font-medium whitespace-nowrap transition-colors hover:text-primary xl:px-4 xl:text-base [&.bg-primary]:hover:text-on-primary">{{ link.label }}</a>
        }
      </nav>

      <div class="hidden items-center gap-3 justify-self-end md:ml-auto md:flex lg:ml-0">
        <a routerLink="/gio-hang" class="relative flex size-11 items-center justify-center" aria-label="Giỏ hàng">
          <lucide-icon [img]="cart" [size]="24" />
          @if (cartState.count() > 0) {
            <span class="absolute top-0.5 right-0 flex h-[18px] min-w-[18px] items-center justify-center rounded-full bg-primary px-1 text-[11px] leading-none font-bold text-on-primary">{{ cartState.count() }}</span>
          }
        </a>
        @if (auth.profile(); as me) {
          <a routerLink="/tai-khoan" class="flex h-11 items-center gap-2 font-semibold" [title]="me.fullName"><lucide-icon class="shrink-0 text-primary" [img]="user" [size]="22" /><span class="max-w-[160px] truncate xl:max-w-[200px]">Xin chào, {{ givenName(me.fullName) }}</span></a>
        } @else {
          <a routerLink="/dang-nhap" class="ot-btn ot-btn-primary whitespace-nowrap" [class.invisible]="!auth.ready()"><lucide-icon class="lg:max-xl:hidden" [img]="user" [size]="20" /> Đăng nhập</a>
        }
      </div>
      <button type="button" class="flex size-11 shrink-0 items-center justify-center lg:hidden" aria-label="Mở menu" [attr.aria-expanded]="open()" (click)="open.set(!open())"><lucide-icon [img]="open() ? close : menu" [size]="24" /></button>
    </div>
    @if (open()) {
      <nav class="ot-container border-t border-line pb-2 lg:hidden" aria-label="Menu">
        @for (link of links; track link.path) {
          <a [routerLink]="link.path" routerLinkActive="text-primary" class="flex min-h-12 items-center font-medium" (click)="open.set(false)">{{ link.label }}</a>
        }
      </nav>
    }
  `,
})
export class SiteHeader {
  protected readonly links = HEADER_LINKS;
  protected readonly open = signal(false);
  protected readonly logo = toSignal(inject(Api).getOr<Record<string, string>>('public/settings', {}).pipe(map((v) => v['restaurant.logoUrl'] ?? null)), { initialValue: null });
  protected readonly menu = Menu;
  protected readonly close = X;
  protected readonly cartState = inject(Cart);
  protected readonly cart = ShoppingBag;
  protected readonly user = User;
  protected readonly auth = inject(Auth);
  protected readonly givenName = givenName;
}
