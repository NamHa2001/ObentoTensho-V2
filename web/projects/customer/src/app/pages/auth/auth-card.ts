import { Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Schemas } from '@ot/api';

import { Api } from '../../core/api';
import { imageUrl } from '../../shared/format';
import { Sakura } from '../../shared/sakura';

// Thẻ hai nửa của đăng nhập và đăng ký: form bên trái, ảnh bên phải. Ảnh lấy từ banner đăng ở admin, chưa có thì hiện họa tiết
@Component({
  selector: 'ot-auth-card',
  imports: [Sakura],
  template: `
    <div class="ot-container py-4 md:py-8">
      <div class="ot-auth-card">
        <div class="ot-auth-panel">
          <h1 class="ot-auth-title">{{ heading() }}</h1>
          <p class="ot-auth-sub">{{ subtitle() }}</p>
          @if (phoneImage() && image(); as url) { <img class="ot-auth-image" [src]="url" alt="" /> }
          <ng-content />
        </div>
        <div class="ot-auth-aside" aria-hidden="true">
          @if (image(); as url) {
            <img class="absolute inset-0 size-full object-cover" [src]="url" alt="" />
          } @else {
            <ot-sakura class="absolute top-8 right-8 size-16 opacity-30" />
            <ot-sakura class="absolute bottom-12 left-10 size-24 opacity-20" />
            <p class="font-heading relative text-4xl font-extrabold text-on-primary">Obento Tensho</p>
            <p class="relative mt-3 text-lg text-on-primary">Cơm hộp Nhật, nấu mới mỗi ngày</p>
          }
        </div>
      </div>
    </div>
  `,
})
export class AuthCard {
  readonly heading = input.required<string>();
  readonly subtitle = input.required<string>();
  readonly phoneImage = input(false);
  private readonly banners = toSignal(inject(Api).getOr<Schemas['PublicBannerDto'][]>('banners', []), { initialValue: [] });
  protected readonly image = computed(() => imageUrl(this.banners()[0]?.image));
}
