import { inject } from '@angular/core';
import { Route, Router, Routes } from '@angular/router';

import { Auth, authGuard, guestGuard } from './core/auth';
import { RESOURCES } from './shared/resources';

const login = () => import('./pages/login');
// Tiêu đề tab theo từng trang để nhân viên mở nhiều tab vẫn phân biệt được
const titled = (name: string) => `${name} | Quản trị Obento Tensho`;
const TITLES: Record<string, string> = {
  dashboard: 'Bảng tin', orders: 'Đơn hàng', 'orders/:id': 'Chi tiết đơn', kitchen: 'Màn hình bếp', reservations: 'Đặt bàn', 'reservations/new': 'Đặt bàn hộ khách',
  'reservations/:id': 'Chi tiết đặt bàn', calendar: 'Lịch đặt bàn', floor: 'Sơ đồ bàn', 'dine-in': 'Gọi món tại bàn', 'dine-in/:id': 'Phiếu bàn', invoices: 'Yêu cầu xuất hóa đơn',
  'option-groups': 'Nhóm tùy chọn', 'item-images/:id': 'Ảnh món', customers: 'Khách hàng', 'customers/:id': 'Chi tiết khách', reports: 'Báo cáo', settings: 'Cài đặt',
  hours: 'Giờ mở cửa', translations: 'Bản dịch', 'exports/download': 'Tải tệp xuất', account: 'Tài khoản của tôi',
};
const res = (key: string, cap: string) => ({ path: `r/${key}`, title: titled(RESOURCES[key].title), canActivate: [authGuard], data: { cap, resource: RESOURCES[key] }, loadComponent: () => import('./shared/resource').then((m) => m.ResourcePage) });
// back: trang cha của nút quay lại trên thanh trên cùng (điện thoại)
const page = (path: string, cap: string | undefined, load: NonNullable<Route['loadComponent']>, back?: string) => ({ path, title: titled(TITLES[path] ?? 'Quản trị'), canActivate: [authGuard], data: { cap, back }, loadComponent: load });

export const routes: Routes = [
  { path: 'login', title: titled('Đăng nhập'), canActivate: [guestGuard], loadComponent: () => login().then((m) => m.Login) },
  { path: 'forgot', title: titled('Quên mật khẩu'), loadComponent: () => login().then((m) => m.Forgot) },
  { path: 'auth/reset-password', title: titled('Đặt mật khẩu'), loadComponent: () => login().then((m) => m.SetPassword) },
  { path: 'auth/accept-invite', title: titled('Nhận lời mời'), data: { invite: true }, loadComponent: () => login().then((m) => m.SetPassword) },
  { path: 'auth/not-me', title: titled('Báo không phải tôi'), loadComponent: () => login().then((m) => m.NotMe) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', canActivate: [async () => { const auth = inject(Auth); await auth.init(); return inject(Router).createUrlTree([auth.home()]); }], children: [] },
      page('dashboard', 'dashboard', () => import('./pages/dashboard').then((m) => m.Dashboard)),
      page('orders', 'orders_cashier', () => import('./pages/orders').then((m) => m.Orders)),
      page('orders/:id', 'orders_cashier', () => import('./pages/orders').then((m) => m.Order), '/orders'),
      page('kitchen', 'orders_kitchen', () => import('./pages/kitchen').then((m) => m.Kitchen)),
      page('reservations', 'reservations', () => import('./pages/reservations').then((m) => m.Reservations)),
      page('reservations/new', 'reservations', () => import('./pages/reservations').then((m) => m.ReservationNew), '/reservations'),
      page('reservations/:id', 'reservations', () => import('./pages/reservations').then((m) => m.Reservation), '/reservations'),
      page('calendar', 'reservations', () => import('./pages/calendar').then((m) => m.Calendar)),
      page('floor', 'tables_view', () => import('./pages/floor').then((m) => m.Floor)),
      page('dine-in', 'dine_in', () => import('./pages/dine-in').then((m) => m.DineIn)),
      page('dine-in/:id', 'dine_in', () => import('./pages/dine-in').then((m) => m.DineInBill), '/dine-in'),
      page('invoices', 'invoices', () => import('./pages/invoices').then((m) => m.Invoices)),
      page('option-groups', 'menu', () => import('./pages/menu').then((m) => m.OptionGroups)),
      page('item-images/:id', 'menu', () => import('./pages/menu').then((m) => m.ItemImages), '/r/items'),
      page('customers', 'customers', () => import('./pages/customers').then((m) => m.Customers)),
      page('customers/:id', 'customers', () => import('./pages/customers').then((m) => m.Customer), '/customers'),
      page('reports', 'reports', () => import('./pages/reports').then((m) => m.Reports)),
      page('settings', 'reports', () => import('./pages/settings').then((m) => m.Settings)),
      page('hours', 'tables_view', () => import('./pages/hours').then((m) => m.Hours)),
      page('translations', 'content', () => import('./pages/translations').then((m) => m.Translations)),
      page('exports/download', 'customer_export', () => import('./pages/customers').then((m) => m.ExportDownload)),
      page('account', undefined, () => import('./pages/staff-account').then((m) => m.StaffAccount)),
      res('categories', 'menu'), res('items', 'menu'), res('banners', 'content'), res('gallery', 'content'), res('news', 'content'), res('news-categories', 'content'), res('pages', 'content'),
      res('promotions', 'customers'), res('vouchers', 'customers'), res('redeem-rewards', 'customers'), res('loyalty-adjustments', 'customers'), res('reviews', 'feedback'), res('contact-messages', 'feedback'), res('subscribers', 'feedback'), res('campaigns', 'feedback'),
      res('table-areas', 'tables_manage'), res('tables', 'tables_manage'), res('service-slots', 'shifts'), res('blocked-dates', 'shifts'), res('staff', 'staff_and_audit'), res('audit', 'staff_and_audit'), res('exports', 'customer_export'),
    ],
  },
  { path: '**', redirectTo: '' },
];
