import { afterNextRender, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { Auth } from './core/auth';
import { DialogHost } from './core/dialog';
import { BottomNav } from './layout/bottom-nav';
import { SiteFooter } from './layout/site-footer';
import { SiteHeader } from './layout/site-header';

@Component({
  selector: 'ot-root',
  imports: [RouterOutlet, SiteHeader, BottomNav, SiteFooter, DialogHost],
  templateUrl: './app.html',
})
export class App {
  private readonly auth = inject(Auth);

  // Khôi phục phiên đăng nhập sau khi trang đã hiện
  constructor() {
    afterNextRender(() => void this.auth.init());
  }
}
