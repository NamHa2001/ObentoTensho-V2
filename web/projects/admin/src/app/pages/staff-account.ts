import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { Submit } from '../shared/ui';
import { askConfirm } from '../../../../customer/src/app/core/dialog';

@Component({
  selector: 'ot-staff-account',
  imports: [FormsModule],
  template: `
    <h1>Tài khoản của tôi</h1>
    <p class="mb-4">{{ auth.me()?.fullName }} · {{ auth.me()?.email }}</p>
    <form class="ot-stack max-w-md" (ngSubmit)="change()">
      <h2 class="text-xl">Đổi mật khẩu</h2>
      <label>Mật khẩu hiện tại<input class="ot-input" name="c" type="password" required [(ngModel)]="p.currentPassword" /></label>
      <label>Mật khẩu mới<input class="ot-input" name="n" type="password" required [(ngModel)]="p.newPassword" /></label>
      @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
      @if (done()) { <p>Đã đổi mật khẩu.</p> }
      <button class="ot-btn ot-btn-outline" [disabled]="form.busy()">Đổi mật khẩu</button>
    </form>

    <section class="ot-stack max-w-md">
      <h2 class="text-xl">Xác thực hai bước</h2>
      <p>Trạng thái: <b>{{ auth.me()?.totpEnabled ? 'Đang bật' : 'Đang tắt' }}</b></p>
      @if (twoFactor.error(); as e) { <p class="ot-error">{{ e }}</p> }
      @if (setup(); as s) {
        <p>Thêm khóa sau vào ứng dụng xác thực rồi nhập mã 6 số:</p>
        <p class="font-mono font-medium break-all">{{ s.secret }}</p>
        <label>Mã 6 số<input class="ot-input" name="code" inputmode="numeric" autocomplete="one-time-code" [(ngModel)]="code" /></label>
        <button type="button" class="ot-btn ot-btn-primary" [disabled]="twoFactor.busy()" (click)="confirm()">Xác nhận</button>
      } @else if (auth.me()?.totpEnabled) {
        <button type="button" class="ot-btn ot-btn-outline" [disabled]="twoFactor.busy()" (click)="regenerate()">Tạo lại mã khôi phục</button>
        <button type="button" class="ot-btn ot-btn-outline" [disabled]="twoFactor.busy()" (click)="disable()">Tắt xác thực hai bước</button>
      } @else {
        <button type="button" class="ot-btn ot-btn-primary" [disabled]="twoFactor.busy()" (click)="begin()">Bật xác thực hai bước</button>
      }
      @if (codes().length > 0) {
        <p>Lưu các mã khôi phục sau, chỉ hiện một lần:</p>
        <ul class="font-mono">@for (c of codes(); track c) { <li>{{ c }}</li> }</ul>
      }
    </section>
  `,
})
export class StaffAccount {
  private readonly api = inject(Api);
  protected readonly auth = inject(Auth);
  protected readonly form = new Submit();
  protected readonly twoFactor = new Submit();
  protected readonly done = signal(false);
  protected readonly setup = signal<{ secret: string } | null>(null);
  protected readonly codes = signal<string[]>([]);
  protected p = { currentPassword: '', newPassword: '' };
  protected code = '';

  protected change(): void {
    this.form.run(this.api.post('auth/me/change-password', this.p), () => this.done.set(true));
  }

  protected begin(): void {
    this.codes.set([]);
    this.twoFactor.run(this.api.post<{ secret: string }>('auth/me/2fa/setup'), (s) => this.setup.set(s));
  }

  protected confirm(): void {
    this.twoFactor.run(this.api.post<{ recoveryCodes: string[] }>('auth/me/2fa/setup/confirm', { code: this.code }), (r) => {
      this.setup.set(null);
      this.code = '';
      this.codes.set(r.recoveryCodes);
      this.auth.loadMe().subscribe();
    });
  }

  protected async regenerate(): Promise<void> {
    if (await askConfirm('Các mã khôi phục cũ sẽ mất hiệu lực. Tiếp tục?')) {
      this.twoFactor.run(this.api.post<{ recoveryCodes: string[] }>('auth/me/2fa/recovery-codes'), (r) => this.codes.set(r.recoveryCodes));
    }
  }

  protected async disable(): Promise<void> {
    if (await askConfirm('Tắt xác thực hai bước?')) {
      this.twoFactor.run(this.api.post('auth/me/2fa/disable'), () => this.auth.loadMe().subscribe());
    }
  }
}
