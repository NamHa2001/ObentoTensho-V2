import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CalendarDays, Eye, EyeOff, Lock, LucideAngularModule, Mail, User } from 'lucide-angular';
import { switchMap } from 'rxjs';

import { Api } from '../../core/api';
import { Auth, ContactPolicy } from '../../core/auth';
import { Turnstile } from '../../core/turnstile';
import { Submit } from '../../shared/form';
import { AuthCard } from './auth-card';

// Đăng ký chỉ cần email (chủ dự án 2026-11-13). Xong đăng nhập luôn để sang bước xác minh còn thiếu (email hoặc SĐT
// theo ContactPolicy). Email đã có tài khoản thì API vẫn trả 202 (không lộ),
// lần đăng nhập sau đó thất bại nên chỉ báo kiểm tra hộp thư
@Component({
  selector: 'ot-register',
  imports: [FormsModule, RouterLink, LucideAngularModule, AuthCard],
  template: `
    @if (sent()) {
      <ot-auth-card heading="Kiểm tra hộp thư" subtitle="Vui lòng mở email để tiếp tục. Nếu email đã có tài khoản, bạn hãy đăng nhập.">
        <a class="ot-btn ot-btn-primary w-full" routerLink="/dang-nhap">Đăng nhập</a>
      </ot-auth-card>
    } @else {
      <ot-auth-card heading="Đăng ký" subtitle="Tạo tài khoản để đặt món, giữ bàn và nhận ưu đãi thành viên.">
        <form class="ot-auth-form" (ngSubmit)="submit()">
          <div class="ot-auth-grid">
            <label>Họ tên<span class="ot-auth-field"><span class="ot-auth-icon"><lucide-icon [img]="icons.user" [size]="20" /></span><input class="ot-input" name="fullName" placeholder="Họ và tên" autocomplete="name" required [(ngModel)]="model.fullName" /></span></label>
            <label>Email<span class="ot-auth-field"><span class="ot-auth-icon"><lucide-icon [img]="icons.mail" [size]="20" /></span><input class="ot-input" name="email" type="email" inputmode="email" placeholder="Email của bạn" autocomplete="email" required [(ngModel)]="model.email" /></span></label>
            <label>Ngày sinh (không bắt buộc)<span class="ot-auth-field"><span class="ot-auth-icon"><lucide-icon [img]="icons.date" [size]="20" /></span><input class="ot-input" name="dob" type="date" [(ngModel)]="model.dateOfBirth" /></span></label>
            <label>Mật khẩu (từ 10 ký tự)<span class="ot-auth-field"><span class="ot-auth-icon"><lucide-icon [img]="icons.lock" [size]="20" /></span><input class="ot-input" name="password" [type]="show() ? 'text' : 'password'" placeholder="Tạo mật khẩu" autocomplete="new-password" required minlength="10" [(ngModel)]="model.password" /><button type="button" class="ot-auth-eye" [attr.aria-label]="show() ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'" [attr.aria-pressed]="show()" (click)="show.set(!show())"><lucide-icon [img]="show() ? icons.hide : icons.eye" [size]="20" /></button></span></label>
            <label>Nhập lại mật khẩu<span class="ot-auth-field"><span class="ot-auth-icon"><lucide-icon [img]="icons.lock" [size]="20" /></span><input class="ot-input" name="confirm" [type]="show() ? 'text' : 'password'" placeholder="Nhập lại mật khẩu" autocomplete="new-password" required [(ngModel)]="confirm" /></span></label>
          </div>
          @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
          <button class="ot-btn ot-btn-primary w-full" [disabled]="form.busy()">Đăng ký</button>
          <p class="text-sm text-muted">Khi đăng ký, bạn đồng ý với <a class="ot-auth-link" routerLink="/dieu-khoan">Điều khoản</a> và <a class="ot-auth-link" routerLink="/chinh-sach-bao-mat">Chính sách bảo mật</a>.</p>
          <p class="ot-auth-foot">Đã có tài khoản? <a class="ot-auth-link" routerLink="/dang-nhap">Đăng nhập</a></p>
        </form>
      </ot-auth-card>
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
  private readonly policy = inject(ContactPolicy);
  protected readonly icons = { user: User, mail: Mail, date: CalendarDays, lock: Lock, eye: Eye, hide: EyeOff };
  protected readonly show = signal(false);
  protected readonly sent = signal(false);
  protected confirm = '';
  protected model = { fullName: '', email: '', password: '', dateOfBirth: '' };

  protected submit(): void {
    if (this.model.password !== this.confirm) {
      this.form.error.set('Hai mật khẩu chưa khớp.');
      return;
    }

    const body = { ...this.model, dateOfBirth: this.model.dateOfBirth || null, preferredLang: 'vi' };
    this.form.run(this.turnstile.headers().pipe(switchMap((h) => this.api.post('auth/register', body, h))), () => {
      this.auth.login(this.model.email, this.model.password).subscribe({
        next: async (p) => void this.router.navigate([(await this.policy.pending(p)) ?? '/tai-khoan'], { queryParams: { next: this.next } }),
        error: () => this.sent.set(true),
      });
    });
  }
}
