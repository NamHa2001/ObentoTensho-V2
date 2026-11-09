import { afterNextRender, Component, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute as ActivatedRoutePublic, RouterLink } from '@angular/router';

import { Api } from '../../core/api';
import { Submit } from '../../shared/form';

// SEC-46: token trong #fragment của liên kết email, đọc xong xóa khỏi thanh địa chỉ
export function takeToken(doc: Document): string {
  const match = /[#&]token=([^&]+)/.exec(doc.location.hash);
  if (match) {
    history.replaceState(null, '', doc.location.pathname + doc.location.search);
  }

  return match ? decodeURIComponent(match[1]) : '';
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

@Component({
  selector: 'ot-forgot',
  imports: [FormsModule],
  template: `
    <form class="ot-form" (ngSubmit)="submit()">
      <h1>Quên mật khẩu</h1>
      @if (done()) {
        <p>Nếu email có tài khoản, liên kết đặt lại mật khẩu đã được gửi tới hộp thư của bạn.</p>
      } @else {
        <label>Email<input class="ot-input" name="email" type="email" required [(ngModel)]="email" /></label>
        @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
        <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Gửi liên kết</button>
      }
    </form>
  `,
})
export class Forgot {
  private readonly api = inject(Api);
  protected readonly form = new Submit();
  protected readonly done = signal(false);
  protected email = '';

  protected submit(): void {
    // Server luôn trả thành công để không lộ email, nên phải tự kiểm tra trước khi báo đã gửi
    if (!EMAIL.test(this.email.trim())) {
      this.form.error.set('Vui lòng nhập đúng địa chỉ email.');
      return;
    }

    this.form.run(this.api.post('auth/forgot-password', { email: this.email.trim() }), () => this.done.set(true));
  }
}

@Component({
  selector: 'ot-reset',
  imports: [FormsModule, RouterLink],
  template: `
    <form class="ot-form" (ngSubmit)="submit()">
      <h1>Đặt lại mật khẩu</h1>
      @if (done()) {
        <p>Đã đổi mật khẩu.</p><a class="ot-btn ot-btn-primary" routerLink="/dang-nhap">Đăng nhập</a>
      } @else if (missing()) {
        <p>Liên kết không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu gửi lại liên kết đặt lại mật khẩu.</p>
        <a class="ot-btn ot-btn-primary" routerLink="/quen-mat-khau">Gửi lại liên kết</a>
      } @else {
        <label>Mật khẩu mới (từ 10 ký tự)<input class="ot-input" name="password" type="password" autocomplete="new-password" required minlength="10" [(ngModel)]="password" /></label>
        <label>Nhập lại mật khẩu<input class="ot-input" name="confirm" type="password" autocomplete="new-password" required [(ngModel)]="confirm" /></label>
        @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
        <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Lưu</button>
      }
    </form>
  `,
})
export class Reset {
  private readonly api = inject(Api);
  private readonly token = takeToken(inject(DOCUMENT));
  protected readonly form = new Submit();
  protected readonly done = signal(false);
  // Mã nằm sau dấu # nên máy chủ không thấy; chỉ kết luận thiếu mã khi đã chạy ở trình duyệt
  protected readonly missing = signal(false);
  protected password = '';
  protected confirm = '';

  constructor() {
    afterNextRender(() => this.missing.set(!this.token));
  }

  protected submit(): void {
    if (this.password !== this.confirm) {
      this.form.error.set('Hai mật khẩu chưa khớp.');
      return;
    }

    this.form.run(this.api.post('auth/reset-password', { token: this.token, newPassword: this.password }), () => this.done.set(true));
  }
}

// Xác nhận bằng một lần POST token: dùng cho xác thực email, đổi email, xác nhận bản tin
@Component({
  selector: 'ot-token-action',
  imports: [RouterLink],
  template: `
    <div class="ot-form">
      <h1>{{ title }}</h1>
      @if (form.busy()) { <p>Đang xử lý…</p> }
      @if (done()) { <p>Thành công.</p><a class="ot-btn ot-btn-primary" routerLink="/">Về trang chủ</a> }
      @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
    </div>
  `,
})
export class TokenAction {
  private readonly api = inject(Api);
  protected readonly form = new Submit();
  protected readonly done = signal(false);
  protected title = '';

  constructor() {
    const route = inject(ActivatedRoutePublic).snapshot.data as { title: string; endpoint: string };
    this.title = route.title;
    this.form.run(this.api.post(route.endpoint, { token: takeToken(inject(DOCUMENT)) }), () => this.done.set(true));
  }
}
