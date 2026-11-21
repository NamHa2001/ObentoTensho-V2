import { Component, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { Check, Eye, EyeOff, KeyRound, Lock, LucideAngularModule, Mail, ShieldCheck } from 'lucide-angular';
import { catchError, map, Observable, of, switchMap, throwError } from 'rxjs';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { Turnstile } from '../../../../customer/src/app/core/turnstile';
import { errorCode, Submit } from '../shared/ui';

type LoginResult = Schemas['StaffLoginResponse'];

// Khung chung của các trang chưa đăng nhập, cùng kiểu thẻ đăng nhập trang khách: form bên trái, thương hiệu (logo từ Cài đặt) bên phải
@Component({
  selector: 'ot-admin-auth',
  imports: [LucideAngularModule],
  template: `
    <main class="a-auth">
      <div class="a-auth-card">
        <aside class="a-auth-brand">
          <span class="a-auth-logo" aria-hidden="true">@if (logo(); as url) { <img [src]="url" alt="" /> } @else { 天 }</span>
          <p class="a-auth-name">Obento Tensho</p>
          <p class="a-auth-tag">Trang quản trị dành cho nhân viên quán</p>
          <ul class="a-auth-points">
            <li><lucide-icon [img]="check" [size]="18" />Đơn hàng và màn hình bếp theo thời gian thực</li>
            <li><lucide-icon [img]="check" [size]="18" />Đặt bàn, sơ đồ bàn và ca phục vụ</li>
            <li><lucide-icon [img]="check" [size]="18" />Thực đơn, khuyến mãi và báo cáo doanh thu</li>
          </ul>
        </aside>
        <div class="a-auth-panel"><ng-content /></div>
      </div>
      <p class="a-auth-copy">© {{ year }} Obento Tensho</p>
    </main>
  `,
})
export class AuthFrame {
  protected readonly check = Check;
  protected readonly year = new Date().getFullYear();
  protected readonly logo = toSignal(inject(Api).publicSettings().pipe(map((s) => s['restaurant.logoUrl'] || null), catchError(() => of(null))), { initialValue: null });
}

// Đăng nhập nhiều bước: mật khẩu → (đổi mật khẩu lần đầu) → (thiết lập 2FA) → nhập mã 2FA
@Component({
  selector: 'ot-admin-login',
  imports: [FormsModule, RouterLink, LucideAngularModule, AuthFrame],
  template: `
    <ot-admin-auth>
      <form class="ot-form" (ngSubmit)="submit()">
        @switch (step()) {
          @case ('login') {
            <h1>Đăng nhập</h1>
            <p class="a-auth-sub">Dùng tài khoản nhân viên do quản lý cấp.</p>
            <label>Email<span class="a-auth-field"><span class="a-auth-icon"><lucide-icon [img]="icons.mail" [size]="18" /></span><input class="ot-input" name="email" type="email" autocomplete="username" required [(ngModel)]="email" /></span></label>
            <label>Mật khẩu<span class="a-auth-field"><span class="a-auth-icon"><lucide-icon [img]="icons.lock" [size]="18" /></span><input class="ot-input" name="password" [type]="show() ? 'text' : 'password'" autocomplete="current-password" required [(ngModel)]="password" /><button type="button" class="a-auth-eye" [attr.aria-label]="show() ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'" (click)="show.set(!show())"><lucide-icon [img]="show() ? icons.eyeOff : icons.eye" [size]="18" /></button></span></label>
          }
          @case ('password_change') {
            <h1>Đổi mật khẩu</h1>
            <p class="a-auth-sub">Lần đầu đăng nhập, bạn cần đặt mật khẩu riêng trước khi tiếp tục.</p>
            <label>Mật khẩu mới (từ 10 ký tự)<span class="a-auth-field"><span class="a-auth-icon"><lucide-icon [img]="icons.lock" [size]="18" /></span><input class="ot-input" name="newPassword" [type]="show() ? 'text' : 'password'" autocomplete="new-password" required [(ngModel)]="newPassword" /><button type="button" class="a-auth-eye" [attr.aria-label]="show() ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'" (click)="show.set(!show())"><lucide-icon [img]="show() ? icons.eyeOff : icons.eye" [size]="18" /></button></span></label>
          }
          @case ('totp_setup') {
            <h1>Bật xác thực 2 bước</h1>
            <p class="a-auth-sub">Mở ứng dụng xác thực (Google Authenticator…), thêm tài khoản bằng khóa sau rồi nhập mã 6 số.</p>
            <p class="a-auth-key">{{ setup()?.secret }}</p>
            <label>Mã 6 số<span class="a-auth-field"><span class="a-auth-icon"><lucide-icon [img]="icons.shield" [size]="18" /></span><input class="ot-input" name="code" inputmode="numeric" autocomplete="one-time-code" required [(ngModel)]="code" /></span></label>
          }
          @case ('recovery') {
            <h1>Mã khôi phục</h1>
            <p class="a-auth-sub">Lưu các mã sau ở nơi an toàn. Mỗi mã dùng một lần và chỉ hiện lúc này.</p>
            <ul class="a-auth-codes">@for (c of recovery(); track c) { <li>{{ c }}</li> }</ul>
          }
          @case ('totp_verify') {
            <h1>Xác thực 2 bước</h1>
            <p class="a-auth-sub">Nhập mã 6 số đang hiện trong ứng dụng xác thực.</p>
            <label>Mã 6 số<span class="a-auth-field"><span class="a-auth-icon"><lucide-icon [img]="icons.shield" [size]="18" /></span><input class="ot-input" name="code" inputmode="numeric" autocomplete="one-time-code" [(ngModel)]="code" /></span></label>
            <label>Hoặc mã khôi phục<span class="a-auth-field"><span class="a-auth-icon"><lucide-icon [img]="icons.key" [size]="18" /></span><input class="ot-input" name="recoveryCode" [(ngModel)]="recoveryCode" /></span></label>
          }
        }
        @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
        <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">{{ step() === 'recovery' ? 'Đã lưu, tiếp tục' : step() === 'login' ? 'Đăng nhập' : 'Tiếp tục' }}</button>
        @if (step() === 'login') { <a class="a-auth-link" routerLink="/forgot">Quên mật khẩu?</a> }
      </form>
    </ot-admin-auth>
  `,
})
export class Login {
  protected readonly icons = { mail: Mail, lock: Lock, eye: Eye, eyeOff: EyeOff, shield: ShieldCheck, key: KeyRound };
  protected readonly show = signal(false);
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
  imports: [FormsModule, RouterLink, LucideAngularModule, AuthFrame],
  template: `
    <ot-admin-auth>
      <form class="ot-form" (ngSubmit)="submit()">
        <h1>Quên mật khẩu</h1>
        @if (done()) { <p class="a-auth-sub">Nếu email có tài khoản, liên kết đặt lại mật khẩu đã được gửi.</p> } @else {
          <p class="a-auth-sub">Nhập email tài khoản, hệ thống sẽ gửi liên kết đặt lại mật khẩu.</p>
          <label>Email<span class="a-auth-field"><span class="a-auth-icon"><lucide-icon [img]="mail" [size]="18" /></span><input class="ot-input" name="email" type="email" autocomplete="username" required [(ngModel)]="email" /></span></label>
          @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
          <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Gửi liên kết</button>
        }
        <a class="a-auth-link" routerLink="/login">‹ Quay lại đăng nhập</a>
      </form>
    </ot-admin-auth>
  `,
})
export class Forgot {
  private readonly api = inject(Api);
  protected readonly mail = Mail;
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
  imports: [FormsModule, RouterLink, LucideAngularModule, AuthFrame],
  template: `
    <ot-admin-auth>
      <form class="ot-form" (ngSubmit)="submit()">
        <h1>Đặt mật khẩu</h1>
        @if (done()) { <p class="a-auth-sub">Đã lưu mật khẩu mới.</p><a class="ot-btn ot-btn-primary" routerLink="/login">Đăng nhập</a> } @else {
          <p class="a-auth-sub">Chọn mật khẩu từ 10 ký tự, không trùng với email.</p>
          <label>Mật khẩu mới<span class="a-auth-field"><span class="a-auth-icon"><lucide-icon [img]="icons.lock" [size]="18" /></span><input class="ot-input" name="password" [type]="show() ? 'text' : 'password'" autocomplete="new-password" required [(ngModel)]="password" /><button type="button" class="a-auth-eye" [attr.aria-label]="show() ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'" (click)="show.set(!show())"><lucide-icon [img]="show() ? icons.eyeOff : icons.eye" [size]="18" /></button></span></label>
          @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
          <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Lưu</button>
        }
      </form>
    </ot-admin-auth>
  `,
})
export class SetPassword {
  private readonly api = inject(Api);
  private readonly token = takeToken(inject(DOCUMENT));
  private readonly invite = inject(ActivatedRoute).snapshot.data['invite'] === true;
  protected readonly icons = { lock: Lock, eye: Eye, eyeOff: EyeOff };
  protected readonly show = signal(false);
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
  imports: [RouterLink, AuthFrame],
  template: `
    <ot-admin-auth>
      <div class="ot-form">
        <h1>Không phải tôi</h1>
        @if (done()) { <p class="a-auth-sub">Đã khóa phiên đăng nhập lạ. Hãy đặt lại mật khẩu.</p><a class="ot-btn ot-btn-primary" routerLink="/forgot">Đặt lại mật khẩu</a> }
        @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
      </div>
    </ot-admin-auth>
  `,
})
export class NotMe {
  protected readonly form = new Submit();
  protected readonly done = signal(false);

  constructor() {
    this.form.run(inject(Api).post('auth/not-me', { token: takeToken(inject(DOCUMENT)) }), () => this.done.set(true));
  }
}

export { switchMap };
