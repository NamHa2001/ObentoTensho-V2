import { Component, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

import { Api } from '../../core/api';
import { Sakura } from '../../shared/sakura';

// Thẻ hai nửa của đăng nhập và đăng ký: form bên trái, khung thương hiệu bên phải (logo từ Cài đặt).
// Không dùng ảnh banner: banner có chữ khuyến mãi, cắt dọc sẽ mất chữ
@Component({
  selector: 'ot-auth-card',
  imports: [Sakura],
  template: `
    <div class="ot-container py-4 md:py-8">
      <div class="ot-auth-card">
        <div class="ot-auth-panel">
          <h1 class="ot-auth-title">{{ heading() }}</h1>
          <p class="ot-auth-sub">{{ subtitle() }}</p>
          <ng-content />
        </div>
        <div class="ot-auth-aside" aria-hidden="true">
          <ot-sakura class="absolute top-8 right-8 size-16 opacity-30" />
          <ot-sakura class="absolute right-16 bottom-12 size-24 opacity-20" />
          <span class="ot-auth-logo">@if (logo(); as url) { <img [src]="url" alt="" /> } @else { 天 }</span>
          <p class="font-heading relative mt-6 text-4xl font-semibold text-on-primary">Obento Tensho</p>
          <p class="relative mt-3 text-lg text-on-primary">Cơm hộp Nhật, nấu mới mỗi ngày</p>
          <ul class="ot-auth-points">
            <li>Đặt món mang về, theo dõi đơn ngay trên web</li>
            <li>Giữ bàn trước, không phải chờ</li>
            <li>Tích điểm mỗi đơn, nhận quà sinh nhật</li>
          </ul>
        </div>
      </div>
    </div>
  `,
})
export class AuthCard {
  readonly heading = input.required<string>();
  readonly subtitle = input.required<string>();
  protected readonly logo = toSignal(inject(Api).getOr<Record<string, string>>('public/settings', {}).pipe(map((v) => v['restaurant.logoUrl'] || null)), { initialValue: null });
}
