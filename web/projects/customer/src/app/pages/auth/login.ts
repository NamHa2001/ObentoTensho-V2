import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Eye, EyeOff, Lock, LucideAngularModule, Mail } from 'lucide-angular';

import { Auth } from '../../core/auth';
import { safeNext } from '../../core/safe-next';
import { Submit } from '../../shared/form';
import { AuthCard } from './auth-card';

@Component({
  selector: 'ot-login',
  imports: [FormsModule, RouterLink, LucideAngularModule, AuthCard],
  template: `
    <ot-auth-card heading="Đăng nhập" subtitle="Đăng nhập để đặt món, giữ bàn và tích điểm dễ dàng hơn.">
      <form class="ot-auth-form" (ngSubmit)="submit()">
        <label>Email<span class="ot-auth-field"><span class="ot-auth-icon"><lucide-icon [img]="icons.mail" [size]="20" /></span><input class="ot-input" name="email" type="email" inputmode="email" placeholder="Email của bạn" autocomplete="username" required [(ngModel)]="email" /></span></label>
        <label>Mật khẩu<span class="ot-auth-field"><span class="ot-auth-icon"><lucide-icon [img]="icons.lock" [size]="20" /></span><input class="ot-input" name="password" [type]="show() ? 'text' : 'password'" placeholder="Nhập mật khẩu của bạn" autocomplete="current-password" required [(ngModel)]="password" /><button type="button" class="ot-auth-eye" [attr.aria-label]="show() ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'" [attr.aria-pressed]="show()" (click)="show.set(!show())"><lucide-icon [img]="show() ? icons.hide : icons.eye" [size]="20" /></button></span></label>
        <a class="ot-auth-link inline-flex min-h-11 items-center justify-self-end" routerLink="/quen-mat-khau">Quên mật khẩu?</a>
        @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
        <button class="ot-btn ot-btn-primary w-full" [disabled]="form.busy()">Đăng nhập</button>
        <p class="ot-auth-foot">Chưa có tài khoản? <a class="ot-auth-link" routerLink="/dang-ky" [queryParams]="{ next: next }">Đăng ký</a></p>
      </form>
    </ot-auth-card>
  `,
})
export class Login {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);
  protected readonly next = inject(ActivatedRoute).snapshot.queryParamMap.get('next');
  protected readonly form = new Submit();
  protected readonly icons = { mail: Mail, lock: Lock, eye: Eye, hide: EyeOff };
  protected readonly show = signal(false);
  protected email = '';
  protected password = '';

  protected submit(): void {
    this.form.run(this.auth.login(this.email, this.password), () => void this.router.navigateByUrl(safeNext(this.next)));
  }
}
