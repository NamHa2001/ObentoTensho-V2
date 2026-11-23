import { Component, computed, DestroyRef, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Schemas } from '@ot/api';
import { Bell, ChevronDown, ChevronLeft, LogOut, LucideAngularModule, Menu, UserRound } from 'lucide-angular';
import { catchError, filter, map, of } from 'rxjs';

import { Api } from '../core/api';
import { Auth, StepUp } from '../core/auth';
import { Back } from '../core/back';
import { Live } from '../core/live';
import { Submit } from '../shared/ui';
import { NAV } from './nav';
import { Modal } from '../../../../customer/src/app/shared/modal';

const ROLES: Record<string, string> = { super_admin: 'SuperAdmin', manager: 'Quản lý', reception: 'Lễ tân', kitchen: 'Bếp', cashier: 'Thu ngân', content_editor: 'Biên tập' };

@Component({
  selector: 'ot-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, LucideAngularModule, Modal],
  template: `
    <div class="a-wrap" [class.a-open]="drawer()">
      @if (drawer()) { <div class="a-scrim" otModal (dismiss)="drawer.set(false)"></div> }
      <aside class="a-side" aria-label="Điều hướng">
        <div class="a-brand">@if (logo(); as url) { <img class="a-logo object-cover" [src]="url" alt="" /> } @else { <span class="a-logo" aria-hidden="true">天</span> }<span>Obento Tensho</span></div>
        <nav class="py-3">
          @for (group of groups(); track group.title) {
            @if (group.items.length === 1) {
              <a class="a-nav-link" [routerLink]="group.items[0].path" routerLinkActive="a-nav-on" (click)="drawer.set(false)"><lucide-icon [img]="group.items[0].icon" [size]="18" /><span>{{ group.items[0].label }}</span></a>
            } @else {
              <button type="button" class="a-nav-link a-nav-group" [class.a-nav-current]="contains(group)" [attr.aria-expanded]="open() === group.title" (click)="toggle(group.title)">
                <lucide-icon [img]="group.icon" [size]="18" /><span class="flex-1 text-left">{{ group.title }}</span>
                <lucide-icon class="a-nav-arrow" [class.a-nav-arrow-on]="open() === group.title" [img]="icons.down" [size]="16" />
              </button>
              @if (open() === group.title) {
                <div class="a-nav-sub">
                  @for (item of group.items; track item.path) {
                    <a class="a-nav-link" [routerLink]="item.path" routerLinkActive="a-nav-on" (click)="drawer.set(false)"><span>{{ item.label }}</span></a>
                  }
                </div>
              }
            }
          }
        </nav>
      </aside>

      <div class="a-body">
        <header class="a-top" style="padding-top: var(--ot-safe-top)">
          <button type="button" class="a-icon-btn a-menu-btn" aria-label="Mở menu" (click)="drawer.set(!drawer())"><lucide-icon [img]="icons.menu" [size]="18" /></button>
          @if (back.target()) { <button type="button" class="a-icon-btn a-back-btn" aria-label="Quay lại" (click)="back.go()"><lucide-icon [img]="icons.back" [size]="18" /> <span>Quay lại</span></button> }
          <div class="flex-1"></div>
          <div class="relative">
            <button type="button" class="a-icon-btn" [attr.aria-label]="'Thông báo, ' + live.unread() + ' chưa đọc'" (click)="toggleBell()">
              <lucide-icon [img]="icons.bell" [size]="18" />
              @if (live.unread() > 0) { <span class="a-count">{{ live.unread() }}</span> }
            </button>
            @if (bellOpen()) {
              <div class="a-drop">
                @for (n of live.notes(); track $index) { <a class="a-drop-item" [routerLink]="n.link" (click)="bellOpen.set(false)">{{ n.text }}</a> } @empty { <p class="p-3 text-sm text-muted">Chưa có thông báo mới.</p> }
              </div>
            }
          </div>
          <div class="relative">
            <button type="button" class="a-user" (click)="userOpen.set(!userOpen()); bellOpen.set(false)">
              <span class="a-user-text"><b class="block leading-tight">{{ auth.me()?.fullName }}</b><small class="text-muted">{{ role() }}</small></span>
              <span class="a-avatar" aria-hidden="true">{{ initial() }}</span>
            </button>
            @if (userOpen()) {
              <div class="a-drop">
                <p class="px-3 py-2 text-sm text-muted">{{ auth.me()?.email }}</p>
                <a class="a-drop-item" routerLink="/account" (click)="userOpen.set(false)"><lucide-icon [img]="icons.user" [size]="18" /> Tài khoản của tôi</a>
                <button type="button" class="a-drop-item text-danger" (click)="logout()"><lucide-icon [img]="icons.logout" [size]="18" /> Đăng xuất</button>
              </div>
            }
          </div>
        </header>
        <main class="a-main">
          @if (!live.connected()) { <p class="a-alert" role="alert">Mất kết nối cập nhật tức thời, đang nối lại…</p> }
          <router-outlet />
        </main>
      </div>
    </div>

    @if (live.toast(); as t) { <p class="a-card fixed top-20 right-4 z-50 p-3 font-medium" role="status">{{ t }}</p> }

    @if (stepUp.open()) {
      <div class="a-drawer items-center justify-center p-4" role="dialog" aria-modal="true">
        <form class="a-card grid w-full max-w-sm gap-3 p-5" (ngSubmit)="confirm()">
          <h2 class="text-xl">Xác thực lại</h2>
          <p class="text-sm text-muted">Thao tác này cần nhập lại mật khẩu.</p>
          <label>Mật khẩu<input class="ot-input" name="p" type="password" autocomplete="current-password" required [(ngModel)]="password" /></label>
          @if (auth.me()?.totpEnabled) { <label>Mã 6 số<input class="ot-input" name="c" inputmode="numeric" autocomplete="one-time-code" [(ngModel)]="code" /></label> }
          @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
          <div class="flex gap-2"><button class="ot-btn ot-btn-primary flex-1" [disabled]="form.busy()">Xác nhận</button><button type="button" class="ot-btn ot-btn-outline flex-1" (click)="cancelStepUp()">Hủy</button></div>
        </form>
      </div>
    }
  `,
})
export class Shell {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected readonly auth = inject(Auth);
  protected readonly stepUp = inject(StepUp);
  protected readonly live = inject(Live);
  protected readonly back = inject(Back);
  protected readonly icons = { menu: Menu, bell: Bell, user: UserRound, logout: LogOut, down: ChevronDown, back: ChevronLeft };
  protected readonly drawer = signal(false);
  protected readonly bellOpen = signal(false);
  protected readonly open = signal<string | null>(null);
  protected readonly userOpen = signal(false);
  protected readonly form = new Submit();
  protected password = '';
  protected code = '';
  private readonly url = toSignal(this.router.events.pipe(filter((e) => e instanceof NavigationEnd), map(() => this.router.url)), { initialValue: this.router.url });
  protected readonly logo = toSignal(this.api.publicSettings().pipe(map((s) => s['restaurant.logoUrl'] || null), catchError(() => of(null))), { initialValue: null });
  protected readonly role = computed(() => ROLES[this.auth.me()?.role ?? ''] ?? '');
  protected readonly initial = computed(() => (this.auth.me()?.fullName.trim().split(/\s+/).at(-1) ?? '?').charAt(0).toUpperCase());
  protected readonly groups = computed(() => {
    this.auth.me();
    return NAV.map((g) => ({ title: g.title, icon: g.icon, items: g.items.filter((i) => this.auth.has(i.cap)) })).filter((g) => g.items.length > 0);
  });

  constructor() {
    this.live.start();
    inject(DestroyRef).onDestroy(() => this.live.stop());
    // Mở sẵn nhóm chứa trang đang xem; chỉ một nhóm mở một lúc để menu luôn ngắn
    effect(() => {
      const group = this.groups().find((g) => this.contains(g));
      if (group) {
        this.open.set(group.title);
      }
    });
  }

  protected contains(group: { items: { path: string }[] }): boolean {
    const url = this.url().split(/[?#]/)[0];
    return group.items.some((i) => url === i.path || url.startsWith(`${i.path}/`));
  }

  protected toggle(title: string): void {
    this.open.update((current) => (current === title ? null : title));
  }

  protected toggleBell(): void {
    this.bellOpen.update((o) => !o);
    this.userOpen.set(false);
    this.live.markRead();
  }

  protected cancelStepUp(): void {
    this.password = '';
    this.code = '';
    this.stepUp.finish(null);
  }

  protected confirm(): void {
    this.form.run(this.api.post<Schemas['StepUpResponse']>('auth/step-up', { password: this.password, code: this.code || null }), (r) => {
      this.password = '';
      this.code = '';
      this.stepUp.finish(r.token);
    });
  }

  protected logout(): void {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/login'));
  }
}
