import { Armchair, BarChart3, CalendarCheck, CalendarDays, CalendarX, ChefHat, ClipboardList, Clock, Clock3, Coins, Download, FileText, FolderTree, Images, Inbox, Languages, LayoutDashboard, ListChecks, LucideIconData, Map, Megaphone, Newspaper, Send, Settings, Star, Tags, Ticket, UserCog, Users, Utensils, ScrollText, Image, BadgePercent } from 'lucide-angular';

export interface NavItem {
  label: string;
  path: string;
  cap: string;
  icon: LucideIconData;
}

export const NAV: { title: string; icon: LucideIconData; items: NavItem[] }[] = [
  { title: 'Tổng quan', icon: LayoutDashboard, items: [{ label: 'Bảng tin', path: '/dashboard', cap: 'dashboard', icon: LayoutDashboard }] },
  {
    title: 'Vận hành',
    icon: ClipboardList,
    items: [
      { label: 'Đơn hàng', path: '/orders', cap: 'orders_cashier', icon: ClipboardList },
      { label: 'Màn hình bếp', path: '/kitchen', cap: 'orders_kitchen', icon: ChefHat },
      { label: 'Đặt bàn', path: '/reservations', cap: 'reservations', icon: CalendarCheck },
      { label: 'Lịch đặt bàn', path: '/calendar', cap: 'reservations', icon: CalendarDays },
      { label: 'Sơ đồ bàn', path: '/floor', cap: 'tables_view', icon: Map },
    ],
  },
  {
    title: 'Thực đơn',
    icon: Utensils,
    items: [
      { label: 'Danh mục', path: '/r/categories', cap: 'menu', icon: FolderTree },
      { label: 'Món ăn', path: '/r/items', cap: 'menu', icon: Utensils },
      { label: 'Nhóm tùy chọn', path: '/option-groups', cap: 'menu', icon: ListChecks },
    ],
  },
  {
    title: 'Khách hàng',
    icon: Users,
    items: [
      { label: 'Khách hàng', path: '/customers', cap: 'customers', icon: Users },
      { label: 'Duyệt điểm', path: '/r/loyalty-adjustments', cap: 'customers', icon: Coins },
      { label: 'Khuyến mãi', path: '/r/promotions', cap: 'customers', icon: BadgePercent },
      { label: 'Voucher', path: '/r/vouchers', cap: 'customers', icon: Ticket },
      { label: 'Đánh giá', path: '/r/reviews', cap: 'feedback', icon: Star },
      { label: 'Tin liên hệ', path: '/r/contact-messages', cap: 'feedback', icon: Inbox },
      { label: 'Người nhận bản tin', path: '/r/subscribers', cap: 'feedback', icon: Megaphone },
      { label: 'Chiến dịch bản tin', path: '/r/campaigns', cap: 'feedback', icon: Send },
    ],
  },
  {
    title: 'Nội dung',
    icon: Newspaper,
    items: [
      { label: 'Banner', path: '/r/banners', cap: 'content', icon: Image },
      { label: 'Không gian', path: '/r/gallery', cap: 'content', icon: Images },
      { label: 'Tin tức', path: '/r/news', cap: 'content', icon: Newspaper },
      { label: 'Danh mục tin', path: '/r/news-categories', cap: 'content', icon: Tags },
      { label: 'Trang tĩnh', path: '/r/pages', cap: 'content', icon: FileText },
      { label: 'Bản dịch', path: '/translations', cap: 'content', icon: Languages },
    ],
  },
  {
    title: 'Bàn và lịch',
    icon: Armchair,
    items: [
      { label: 'Tầng / khu vực', path: '/r/table-areas', cap: 'tables_manage', icon: Map },
      { label: 'Bàn', path: '/r/tables', cap: 'tables_manage', icon: Armchair },
      { label: 'Ca phục vụ', path: '/r/service-slots', cap: 'shifts', icon: Clock3 },
      { label: 'Ngày khóa', path: '/r/blocked-dates', cap: 'shifts', icon: CalendarX },
      { label: 'Giờ mở cửa', path: '/hours', cap: 'tables_view', icon: Clock },
    ],
  },
  {
    title: 'Hệ thống',
    icon: Settings,
    items: [
      { label: 'Báo cáo', path: '/reports', cap: 'reports', icon: BarChart3 },
      { label: 'Cài đặt', path: '/settings', cap: 'reports', icon: Settings },
      { label: 'Nhân viên', path: '/r/staff', cap: 'staff_and_audit', icon: UserCog },
      { label: 'Nhật ký thao tác', path: '/r/audit', cap: 'staff_and_audit', icon: ScrollText },
      { label: 'Tệp xuất', path: '/r/exports', cap: 'customer_export', icon: Download },
    ],
  },
];
