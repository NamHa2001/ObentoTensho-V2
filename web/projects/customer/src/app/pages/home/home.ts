import { afterNextRender, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Meta } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { ArrowRight, CalendarDays, ChevronRight, Clock, LucideAngularModule, UtensilsCrossed } from 'lucide-angular';

import { Api } from '../../core/api';
import { MenuItemCard } from '../../shared/menu-item-card';
import { Sakura } from '../../shared/sakura';
import { SectionTitle } from '../../shared/section-title';

type Hours = Schemas['PublicOpeningHoursDto'];
const BANNER_MS = 5000;
const SWIPE_PX = 40;
const DAYS = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];

@Component({
  selector: 'ot-home',
  imports: [RouterLink, LucideAngularModule, MenuItemCard, Sakura, SectionTitle],
  templateUrl: './home.html',
})
export class Home {
  private readonly api = inject(Api);

  protected readonly icons = { order: UtensilsCrossed, book: CalendarDays, next: ChevronRight, arrow: ArrowRight, clock: Clock };
  protected readonly categories = toSignal(this.api.getOr<Schemas['PublicCategoryDto'][]>('menu/categories', []), { initialValue: [] });
  protected readonly featured = toSignal(this.api.getOr<Schemas['PublicMenuItemDto'][]>('menu/items/featured', []), { initialValue: [] });
  protected readonly banners = toSignal(this.api.getOr<Schemas['PublicBannerDto'][]>('banners', []), { initialValue: [] });
  protected readonly active = signal(0);
  protected readonly banner = computed(() => {
    const list = this.banners();
    return list.length > 0 ? list[this.active() % list.length] : null;
  });
  protected readonly bannerCount = computed(() => this.banners().length);
  private paused = false;
  private touchX: number | null = null;
  private readonly hours = toSignal(this.api.getOr<Hours | null>('public/opening-hours', null), { initialValue: null });

  // Chỉ chạy ở trình duyệt để server dựng trang không bị treo chờ bộ đếm
  constructor() {
    inject(Meta).updateTag({ name: 'description', content: 'Cơm hộp Nhật nấu mới mỗi ngày. Đặt món mang về hoặc giữ bàn trước tại Obento Tensho.' });
    const destroy = inject(DestroyRef);
    afterNextRender(() => {
      const timer = setInterval(() => {
        if (!this.paused && !matchMedia('(prefers-reduced-motion: reduce)').matches) {
          this.move(1);
        }
      }, BANNER_MS);
      destroy.onDestroy(() => clearInterval(timer));
    });
  }

  protected pause(paused: boolean): void {
    this.paused = paused;
  }

  protected touchStart(event: TouchEvent): void {
    this.touchX = event.changedTouches[0].clientX;
  }

  protected touchEnd(event: TouchEvent): void {
    if (this.touchX !== null) {
      const distance = event.changedTouches[0].clientX - this.touchX;
      if (Math.abs(distance) > SWIPE_PX) {
        this.move(distance < 0 ? 1 : -1);
      }
    }

    this.touchX = null;
  }

  private move(delta: number): void {
    const count = this.bannerCount();
    if (count > 1) {
      this.active.update((i) => (i + delta + count) % count);
    }
  }

  // API trả null khi chưa đăng ảnh, dù kiểu sinh từ OpenAPI chưa ghi nhận
  protected imageUrl(image: Schemas['ImageDto'] | null): string | null {
    return image?.variants.at(-1)?.url ?? null;
  }

  protected srcset(image: Schemas['ImageDto']): string {
    return image.variants.map((v) => `${v.url} ${v.width}w`).join(', ');
  }

  // Thứ hai đứng đầu; dòng nào cũng đúng một cột tên ngày và một cột giờ
  protected readonly week = computed(() =>
    [...(this.hours()?.weekly ?? [])]
      .sort((a, b) => ((a.dayOfWeek + 6) % 7) - ((b.dayOfWeek + 6) % 7))
      .map((d) => ({
        day: DAYS[d.dayOfWeek],
        time: d.closed || d.ranges.length === 0 ? 'Nghỉ' : d.ranges.map((r) => `${r.open.slice(0, 5)} – ${r.close.slice(0, 5)}`).join(', '),
      })),
  );
}
