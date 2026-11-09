import { Component, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { catchError, Observable, switchMap, throwError } from 'rxjs';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { Turnstile } from '../../../../customer/src/app/core/turnstile';
import { errorCode, Submit } from '../shared/ui';

type LoginResult = Schemas['StaffLoginResponse'];

// Đăng nhập nhiều bước: mật khẩu → (đổi mật khẩu lần đầu) → (thiết lập 2FA) → nhập mã 2FA
@Component({
  selector: 'ot-admin-login',
  imports: [FormsModule, RouterLink],
  template: `
    <form class="ot-form" (ngSubmit)="submit()">
      <h1>Quản trị Obento Tensho</h1>
      @switch (step()) {
        @case ('login') {
          <label>Email<input class="ot-input" name="email" type="email" autocomplete="username" required [(ngModel)]="email" /></label>
          <label>Mật khẩu<input class="ot-input" name="password" type="password" autocomplete="current-password" required [(ngModel)]="password" /></label>
        }
        @case ('password_change') {
          <p>Bạn cần đổi mật khẩu trước khi tiếp tục.</p>
          <label>Mật khẩu mới (từ 10 ký tự)<input class="ot-input" name="newPassword" type="password" autocomplete="new-password" required [(ngModel)]="newPassword" /></label>
        }
        @case ('totp_setup') {
          <p>Mở ứng dụng xác thực (Google Authenticator…), thêm tài khoản bằng khóa sau rồi nhập mã 6 số:</p>
          <p class="break-all font-mono font-bold">{{ setup()?.secret }}</p>
          <label>Mã 6 số<input class="ot-input" name="code" inputmode="numeric" autocomplete="one-time-code" required [(ngModel)]="code" /></label>
        }
        @case ('recovery') {
          <p>Lưu các mã khôi phục sau, mỗi mã dùng một lần và chỉ hiện lúc này:</p>
          <ul class="font-mono">@for (c of recovery(); track c) { <li>{{ c }}</li> }</ul>
        }
        @case ('totp_verify') {
          <label>Mã 6 số từ ứng dụng xác thực<input class="ot-input" name="code" inputmode="numeric" autocomplete="one-time-code" [(ngModel)]="code" /></label>
          <label>Hoặc mã khôi phục<input class="ot-input" name="recoveryCode" [(ngModel)]="recoveryCode" /></label>
        }
      }
      @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
      <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">{{ step() === 'recovery' ? 'Đã lưu, tiếp tục' : 'Tiếp tục' }}</button>
      @if (step() === 'login') { <a class="text-primary" routerLink="/forgot">Quên mật khẩu?</a> }
    </form>
  `,
})
export class Login {
  private readonly api = inject(Api);
  private readonly turnstile = inject(Turnstile);
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);
  private challenge = '';
  private pending: Schemas['AccessTokenResponse'] | null = null;
  protected readonly form = new Submit();
  protected readonly step = signal<'login' | 'password_change' | 'totp_setup' | 'totp_verify' | 'recovery'>('login');
  protected readonly setup = signal<Schemas['TotpSetupResponse'] | null>(null);
  protected readonly recovery = signal<string[]>([]);
  protected email = '';
  protected password = '';
  protected newPassword = '';
  protected code = '';
  protected recoveryCode = '';

  protected submit(): void {
    switch (this.step()) {
      case 'login':
        this.run(this.sendLogin());
        break;
      case 'password_change':
        this.run(this.api.post<LoginResult>('auth/change-password', { challengeToken: this.challenge, currentPassword: this.password, newPassword: this.newPassword }));
        break;
      case 'totp_verify':
        this.run(this.api.post<LoginResult>('auth/2fa/verify', { challengeToken: this.challenge, code: this.code || null, recoveryCode: this.recoveryCode || null }));
        break;
      case 'totp_setup':
        this.form.run(this.api.post<Schemas['TotpSetupConfirmResponse']>('auth/2fa/setup/confirm', { challengeToken: this.challenge, code: this.code }), (r) => {
          this.pending = r.session;
          this.recovery.set(r.recoveryCodes);
          this.step.set('recovery');
        });
        break;
      default:
        this.finish(this.pending as Schemas['AccessTokenResponse']);
    }
  }

  // SEC-51: sau vài lần sai server mới đòi Turnstile
  private sendLogin(): Observable<LoginResult> {
    const send = (headers?: Record<string, string>) => this.api.postWith<LoginResult>('auth/login', { email: this.email, password: this.password }, headers);
    return send().pipe(catchError((e: unknown) => (errorCode(e) === 'CAPTCHA_FAILED' ? this.turnstile.headers().pipe(switchMap(send)) : throwError(() => e))));
  }

  private run(request: Observable<LoginResult>): void {
    this.form.run(request, (r) => {
      this.code = '';
      if (r.session) {
        this.finish(r.session);
        return;
      }

      this.challenge = r.challenge.challengeToken;
      if (r.challenge.kind === 'totp_setup') {
        this.form.run(this.api.post<Schemas['TotpSetupResponse']>('auth/2fa/setup', { challengeToken: this.challenge }), (s) => {
          this.setup.set(s);
          this.step.set('totp_setup');
        });
      } else {
        this.step.set(r.challenge.kind as 'password_change' | 'totp_verify');
      }
    });
  }

  private finish(session: Schemas['AccessTokenResponse']): void {
    this.form.run(this.auth.startSession(session), () => void this.router.navigateByUrl(this.auth.home()));
  }
}

function takeToken(doc: Document): string {
  const m = /[#&]token=([^&]+)/.exec(doc.location.hash);
  if (m) history.replaceState(null, '', doc.location.pathname);
  return m ? decodeURIComponent(m[1]) : '';
}

@Component({
  selector: 'ot-admin-forgot',
  imports: [FormsModule],
  template: `
    <form class="ot-form" (ngSubmit)="submit()">
      <h1>Quên mật khẩu</h1>
      @if (done()) { <p>Nếu email có tài khoản, liên kết đã được gửi.</p> } @else {
        <label>Email<input class="ot-input" name="email" type="email" required [(ngModel)]="email" /></label>
        @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
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
    this.form.run(this.api.post('auth/forgot-password', { email: this.email }), () => this.done.set(true));
  }
}

// Đặt lại mật khẩu và nhận lời mời: nhập mật khẩu mới với token trong email
@Component({
  selector: 'ot-admin-set-password',
  imports: [FormsModule, RouterLink],
  template: `
    <form class="ot-form" (ngSubmit)="submit()">
      <h1>Đặt mật khẩu</h1>
      @if (done()) { <p>Đã lưu.</p><a class="ot-btn ot-btn-primary" routerLink="/login">Đăng nhập</a> } @else {
        <label>Mật khẩu mới (từ 10 ký tự)<input class="ot-input" name="password" type="password" autocomplete="new-password" required [(ngModel)]="password" /></label>
        @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
        <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Lưu</button>
      }
    </form>
  `,
})
export class SetPassword {
  private readonly api = inject(Api);
  private readonly token = takeToken(inject(DOCUMENT));
  private readonly invite = inject(ActivatedRoute).snapshot.data['invite'] === true;
  protected readonly form = new Submit();
  protected readonly done = signal(false);
  protected password = '';

  protected submit(): void {
    const request = this.invite
      ? this.api.post('auth/accept-invite', { token: this.token, password: this.password })
      : this.api.post('auth/reset-password', { token: this.token, newPassword: this.password });
    this.form.run(request, () => this.done.set(true));
  }
}

@Component({
  selector: 'ot-admin-not-me',
  template: `<div class="ot-form"><h1>Không phải tôi</h1>@if (done()) { <p>Đã khóa phiên đăng nhập lạ. Hãy đặt lại mật khẩu.</p> } @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }</div>`,
})
export class NotMe {
  protected readonly form = new Submit();
  protected readonly done = signal(false);

  constructor() {
    this.form.run(inject(Api).post('auth/not-me', { token: takeToken(inject(DOCUMENT)) }), () => this.done.set(true));
  }
}

export { switchMap };
