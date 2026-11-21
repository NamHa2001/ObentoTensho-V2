import { afterNextRender, Component, computed, DestroyRef, inject, isDevMode, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NgTemplateOutlet } from '@angular/common';
import { Meta } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { CalendarDays, ChevronRight, Clock, Gift, LucideAngularModule, MapPin, MessageCircle, Navigation, Phone, ShoppingBag, Star, UtensilsCrossed } from 'lucide-angular';
import { catchError, map, of } from 'rxjs';

import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { VnDatePipe } from '../../shared/format';
import { MenuItemCard } from '../../shared/menu-item-card';
import { openStatus } from '../../shared/open-status';
import { Sakura } from '../../shared/sakura';
import { SectionTitle } from '../../shared/section-title';

type Hours = Schemas['PublicOpeningHoursDto'];
const BANNER_MS = 5000;
const SWIPE_PX = 40;
const STORY_CHARS = 180;
const MINUTE_MS = 60000;
const DAYS = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];

// Máy dev chừa khung trống để thấy bố cục; web thật chỉ hiện những gì đã đăng
function slots<T>(list: readonly T[], count: number): (T | null)[] {
  const taken: (T | null)[] = list.slice(0, count);
  return isDevMode() ? [...taken, ...new Array<null>(count - taken.length).fill(null)] : taken;
}

@Component({
  selector: 'ot-home',
  imports: [NgTemplateOutlet, RouterLink, LucideAngularModule, MenuItemCard, Sakura, SectionTitle, VnDatePipe],
  templateUrl: './home.html',
})
export class Home {
  private readonly api = inject(Api);

  protected readonly icons = { clock: Clock, order: UtensilsCrossed, book: CalendarDays, next: ChevronRight, gift: Gift, pin: MapPin, phone: Phone, map: Navigation, star: Star };
  protected readonly stars = [1, 2, 3, 4, 5];
  protected readonly services = [
    { path: '/menu', icon: ShoppingBag, title: 'Đặt món', note: 'Hẹn giờ đến lấy' },
    { path: '/dat-ban', icon: CalendarDays, title: 'Đặt bàn', note: 'Chọn ngày và ca' },
    { path: '/tai-khoan/diem-thuong', icon: Gift, title: 'Tích điểm', note: 'Đổi lấy ưu đãi' },
    { path: '/lien-he', icon: MessageCircle, title: 'Liên hệ', note: 'Hỏi quán, góp ý' },
  ];
  protected readonly categories = toSignal(this.api.getOr<Schemas['PublicCategoryDto'][]>('menu/categories', []), { initialValue: [] });
  protected readonly featured = toSignal(this.api.getOr<Schemas['PublicMenuItemDto'][]>('menu/items/featured', []), { initialValue: [] });
  protected readonly banners = toSignal(this.api.getOr<Schemas['PublicBannerDto'][]>('banners', []), { initialValue: [] });
  // Danh mục luôn lấp đủ chiều ngang vùng an toàn, kể cả khi quán chỉ có vài danh mục
  protected readonly catCols = computed(() => Math.min(this.categories().length, 4));
  protected readonly catColsMd = computed(() => (this.categories().length === 3 ? 3 : Math.min(this.categories().length, 2)));
  protected readonly active = signal(0);
  protected readonly banner = computed(() => {
    const list = this.banners();
    return list.length > 0 ? list[this.active() % list.length] : null;
  });
  protected readonly bannerCount = computed(() => this.banners().length);
  protected readonly member = inject(Auth).profile;
  protected readonly settings = toSignal(this.api.getOr<Record<string, string>>('public/settings', {}), { initialValue: {} as Record<string, string> });
  private readonly promotions = toSignal(this.api.getOr<Schemas['PublicPromotionDto'][]>('promotions', []), { initialValue: [] });
  private readonly gallery = toSignal(this.api.getOr<Schemas['PublicGalleryItemDto'][]>('gallery', []), { initialValue: [] });
  private readonly about = toSignal(this.api.getOr<Schemas['PublicStaticPageDto'] | null>('public/pages/about', null), { initialValue: null });
  private readonly reviews = toSignal(this.api.getPage<Schemas['PublicReviewDto']>('reviews', { page: 1, pageSize: 12 }).pipe(map((p) => p.data), catchError(() => of([]))), { initialValue: [] });
  private readonly news = toSignal(this.api.getPage<Schemas['PublicNewsListItemDto']>('news', { page: 1, pageSize: 3 }).pipe(map((p) => p.data), catchError(() => of([]))), { initialValue: [] });
  protected readonly featuredSlots = computed(() => slots(this.featured(), 4));
  protected readonly promoSlots = computed(() => slots(this.promotions(), 3));
  protected readonly gallerySlots = computed(() => slots(this.gallery().filter((g) => g.image !== null), 4));
  protected readonly reviewSlots = computed(() => slots(this.reviews().filter((r) => r.comment), 3));
  protected readonly newsSlots = computed(() => slots(this.news(), 3));
  protected readonly story = computed(() => {
    const text = (this.about()?.contentHtml ?? '').replace(/<[^>]+>/g, ' ').replace(/&nbsp;/g, ' ').replace(/\s+/g, ' ').trim();
    return (text.length > STORY_CHARS ? `${text.slice(0, STORY_CHARS).trimEnd()}…` : text) || 'Cơm hộp Nhật nấu mới mỗi ngày, mang về hoặc thưởng thức ngay tại quán.';
  });
  protected readonly mapUrl = computed(() => {
    const address = this.settings()['restaurant.address'];
    return address ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address)}` : null;
  });
  protected readonly socials = computed(() => {
    const all = this.settings();
    return [['Facebook', 'social.facebook'], ['Zalo', 'social.zalo'], ['Instagram', 'social.instagram'], ['TikTok', 'social.tiktok'], ['YouTube', 'social.youtube']]
      .filter(([, key]) => all[key])
      .map(([label, key]) => ({ label, url: all[key] }));
  });
  protected readonly status = computed(() => {
    const hours = this.hours();
    return hours && hours.weekly.length > 0 ? openStatus(hours, new Date(this.now())) : null;
  });
  // Tính lại trạng thái mở cửa mỗi phút để trang để lâu không báo sai giờ
  private readonly now = signal(Date.now());
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
      const clock = setInterval(() => this.now.set(Date.now()), MINUTE_MS);
      destroy.onDestroy(() => {
        clearInterval(timer);
        clearInterval(clock);
      });
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

  // Liên kết trong web đi bằng router (không tải lại trang); liên kết ngoài mở tab mới
  protected internalLink(link: string | null | undefined): string | null {
    return link && link.startsWith('/') && !link.startsWith('//') ? link : null;
  }

    protected srcset(image: Schemas['ImageDto']): string {
    return image.variants.map((v) => `${v.url} ${v.width}w`).join(', ');
  }

  // Thứ hai đứng đầu; các ngày liền nhau cùng giờ gộp một dòng, cả tuần như nhau thì ghi "Mỗi ngày"
  protected readonly week = computed(() => {
    const days = [...(this.hours()?.weekly ?? [])]
      .sort((a, b) => ((a.dayOfWeek + 6) % 7) - ((b.dayOfWeek + 6) % 7))
      .map((d) => ({
        day: DAYS[d.dayOfWeek],
        time: d.closed || d.ranges.length === 0 ? 'Nghỉ' : d.ranges.map((r) => `${r.open.slice(0, 5)} – ${r.close.slice(0, 5)}`).join(', '),
      }));
    const groups: { from: string; to: string; time: string }[] = [];
    for (const d of days) {
      const last = groups.at(-1);
      if (last?.time === d.time) last.to = d.day;
      else groups.push({ from: d.day, to: d.day, time: d.time });
    }

    return groups.map((g) => ({
      day: groups.length === 1 && days.length === DAYS.length ? 'Mỗi ngày' : g.from === g.to ? g.from : `${g.from} – ${g.to}`,
      time: g.time,
    }));
  });
}
