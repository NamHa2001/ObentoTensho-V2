import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Auth } from '../../core/auth';
import { Submit } from '../../shared/form';

@Component({
  selector: 'ot-login',
  imports: [FormsModule, RouterLink],
  template: `
    <form class="ot-form" (ngSubmit)="submit()">
      <h1>Đăng nhập</h1>
      <label>Email<input class="ot-input" name="email" type="email" autocomplete="username" required [(ngModel)]="email" /></label>
      <label>Mật khẩu<input class="ot-input" name="password" type="password" autocomplete="current-password" required [(ngModel)]="password" /></label>
      @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
      <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Đăng nhập</button>
      <p><a class="text-primary" routerLink="/quen-mat-khau">Quên mật khẩu?</a></p>
      <p>Chưa có tài khoản? <a class="text-primary" routerLink="/dang-ky" [queryParams]="{ next: next }">Đăng ký</a></p>
    </form>
  `,
})
export class Login {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);
  protected readonly next = inject(ActivatedRoute).snapshot.queryParamMap.get('next');
  protected readonly form = new Submit();
  protected email = '';
  protected password = '';

  protected submit(): void {
    this.form.run(this.auth.login(this.email, this.password), () => void this.router.navigateByUrl(this.next ?? '/tai-khoan'));
  }
}
