import { CalendarDays, House, LucideIconData, ShoppingBag, User, UtensilsCrossed } from 'lucide-angular';

export interface NavItem {
  label: string;
  path: string;
  icon: LucideIconData;
  exact?: boolean;
}

// Thanh dưới trên điện thoại: đúng 5 mục, chia đều bề ngang
export const TAB_ITEMS: NavItem[] = [
  { label: 'Trang chủ', path: '/', icon: House, exact: true },
  { label: 'Thực đơn', path: '/menu', icon: UtensilsCrossed },
  { label: 'Đặt bàn', path: '/dat-ban', icon: CalendarDays },
  { label: 'Giỏ hàng', path: '/gio-hang', icon: ShoppingBag },
  { label: 'Tài khoản', path: '/tai-khoan', icon: User },
];

export const HEADER_LINKS = [
  { label: 'Thực đơn', path: '/menu' },
  { label: 'Khuyến mãi', path: '/khuyen-mai' },
  { label: 'Không gian', path: '/khong-gian' },
  { label: 'Tin tức', path: '/tin-tuc' },
  { label: 'Tra cứu đơn', path: '/tra-cuu' },
  { label: 'Liên hệ', path: '/lien-he' },
];
