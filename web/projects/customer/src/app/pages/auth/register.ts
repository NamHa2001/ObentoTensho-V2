import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';

import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { Turnstile } from '../../core/turnstile';
import { Submit } from '../../shared/form';

// Đăng ký xong đăng nhập luôn để sang bước xác minh SĐT. Email đã có tài khoản thì API vẫn trả 202 (không lộ),
// lần đăng nhập sau đó thất bại nên chỉ báo kiểm tra hộp thư
@Component({
  selector: 'ot-register',
  imports: [FormsModule, RouterLink],
  template: `
    @if (sent()) {
      <div class="ot-form"><h1>Kiểm tra hộp thư</h1><p>Vui lòng mở email để tiếp tục. Nếu email đã có tài khoản, bạn hãy đăng nhập.</p><a class="ot-btn ot-btn-primary" routerLink="/dang-nhap">Đăng nhập</a></div>
    } @else {
      <form class="ot-form" (ngSubmit)="submit()">
        <h1>Đăng ký</h1>
        <label>Họ tên<input class="ot-input" name="fullName" autocomplete="name" required [(ngModel)]="model.fullName" /></label>
        <label>Email<input class="ot-input" name="email" type="email" autocomplete="email" required [(ngModel)]="model.email" /></label>
        <label>Số điện thoại Việt Nam<input class="ot-input" name="phone" type="tel" autocomplete="tel" required [(ngModel)]="model.phoneNumber" /></label>
        <label>Ngày sinh (không bắt buộc)<input class="ot-input" name="dob" type="date" [(ngModel)]="model.dateOfBirth" /></label>
        <label>Mật khẩu (từ 10 ký tự)<input class="ot-input" name="password" type="password" autocomplete="new-password" required minlength="10" [(ngModel)]="model.password" /></label>
        <label>Nhập lại mật khẩu<input class="ot-input" name="confirm" type="password" autocomplete="new-password" required [(ngModel)]="confirm" /></label>
        @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
        <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Đăng ký</button>
        <p class="text-sm">Khi đăng ký, bạn đồng ý với <a class="text-primary" routerLink="/dieu-khoan">Điều khoản</a> và <a class="text-primary" routerLink="/chinh-sach-bao-mat">Chính sách bảo mật</a>.</p>
        <p>Đã có tài khoản? <a class="text-primary" routerLink="/dang-nhap">Đăng nhập</a></p>
      </form>
    }
  `,
})
export class Register {
  private readonly api = inject(Api);
  private readonly auth = inject(Auth);
  private readonly turnstile = inject(Turnstile);
  private readonly router = inject(Router);
  private readonly next = inject(ActivatedRoute).snapshot.queryParamMap.get('next');
  protected readonly form = new Submit();
  protected readonly sent = signal(false);
  protected confirm = '';
  protected model = { fullName: '', email: '', phoneNumber: '', password: '', dateOfBirth: '' };

  protected submit(): void {
    if (this.model.password !== this.confirm) {
      this.form.error.set('Hai mật khẩu chưa khớp.');
      return;
    }

    const body = { ...this.model, dateOfBirth: this.model.dateOfBirth || null, preferredLang: 'vi' };
    this.form.run(this.turnstile.headers().pipe(switchMap((h) => this.api.post('auth/register', body, h))), () => {
      this.auth.login(this.model.email, this.model.password).pipe(switchMap((p) => [p])).subscribe({
        next: () => void this.router.navigate(['/xac-minh-sdt'], { queryParams: { next: this.next } }),
        error: () => this.sent.set(true),
      });
    });
  }
}
