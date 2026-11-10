import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';

import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { safeNext } from '../../core/safe-next';
import { Turnstile } from '../../core/turnstile';
import { Submit } from '../../shared/form';
import { localPhone } from '../../shared/format';
import { AuthCard } from './auth-card';

const RESEND_SECONDS = 60;

@Component({
  selector: 'ot-phone-verify',
  imports: [FormsModule, RouterLink, AuthCard],
  template: `
    <ot-auth-card heading="Xác minh số điện thoại" subtitle="Bước cuối để đặt bàn, đặt món và nhận voucher sinh nhật.">
      <form class="ot-auth-form" (ngSubmit)="verify()">
      @if (auth.profile()?.isPhoneVerified) {
        <p>Số điện thoại đã được xác minh.</p>
        <a class="ot-btn ot-btn-primary" routerLink="/tai-khoan">Về tài khoản</a>
      } @else {
        <p>Mã 6 số sẽ gửi qua SMS tới <b>{{ phone() }}</b>. Cần xác minh để đặt bàn, đặt món và nhận voucher sinh nhật.</p>
        <button type="button" class="ot-btn ot-btn-outline" [disabled]="sender.busy() || wait() > 0" (click)="send()">{{ wait() > 0 ? 'Gửi lại sau ' + wait() + ' giây' : sent() ? 'Gửi lại mã' : 'Gửi mã' }}</button>
        @if (sender.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
        @if (sent()) {
          <label>Mã xác minh<input class="ot-input" name="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6" required [(ngModel)]="code" /></label>
          @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
          <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Xác minh</button>
        }
        <p class="text-sm"><a class="text-primary" routerLink="/tai-khoan/thong-tin">Sai số điện thoại? Sửa trong tài khoản</a></p>
      }
      </form>
    </ot-auth-card>
  `,
})
export class PhoneVerify {
  private readonly api = inject(Api);
  private readonly turnstile = inject(Turnstile);
  private readonly router = inject(Router);
  private readonly next = inject(ActivatedRoute).snapshot.queryParamMap.get('next');
  protected readonly auth = inject(Auth);
  protected readonly phone = computed(() => localPhone(this.auth.profile()?.phoneNumber));
  protected readonly form = new Submit();
  protected readonly sender = new Submit();
  protected readonly sent = signal(false);
  protected readonly wait = signal(0);
  private readonly destroy = inject(DestroyRef);
  protected code = '';

  protected send(): void {
    this.sender.run(this.turnstile.headers().pipe(switchMap((h) => this.api.post('account/phone/send-code', {}, h))), () => {
      this.sent.set(true);
      this.startWait();
    });
  }

  // Server cho gửi lại sau 60 giây
  private startWait(): void {
    this.wait.set(RESEND_SECONDS);
    const timer = setInterval(() => {
      this.wait.update((s) => s - 1);
      if (this.wait() <= 0) {
        clearInterval(timer);
      }
    }, 1000);
    this.destroy.onDestroy(() => clearInterval(timer));
  }

  protected verify(): void {
    this.form.run(this.api.post('account/phone/verify', { code: this.code }).pipe(switchMap(() => this.auth.loadProfile())), () => void this.router.navigateByUrl(safeNext(this.next)));
  }
}
