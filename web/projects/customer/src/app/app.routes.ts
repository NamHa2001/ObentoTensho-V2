import { Routes } from '@angular/router';

import { authGuard, guestGuard, verifiedGuard } from './core/auth';

const auth = () => import('./pages/auth/password');
const order = () => import('./pages/order/order-detail');
const content = () => import('./pages/content/content');
const account = () => import('./pages/account/account');
const login = () => import('./pages/auth/login').then((m) => m.Login);
const reset = () => auth().then((m) => m.Reset);
const tokenAction = () => auth().then((m) => m.TokenAction);
const lookup = () => order().then((m) => m.Lookup);
const staticPage = () => content().then((m) => m.StaticPage);
const verifyEmail = { title: 'Xác thực email', endpoint: 'auth/verify-email' };

// data.back: trang cha của nút quay lại trên thanh tiêu đề (điện thoại)
// Các đường dẫn tiếng Anh (/auth, /orders, /reservations, /account) là liên kết trong email đã gửi
export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/home/home').then((m) => m.Home), title: 'Obento Tensho | Cơm hộp Nhật' },
  { path: 'menu', loadComponent: () => import('./pages/menu/menu').then((m) => m.Menu), title: 'Thực đơn | Obento Tensho' },
  { path: 'menu/:slug', data: { back: '/menu' }, loadComponent: () => import('./pages/menu-detail/menu-detail').then((m) => m.MenuDetail) },
  { path: 'dat-ban', canActivate: [verifiedGuard], loadComponent: () => import('./pages/reservation/reservation').then((m) => m.Reservation), title: 'Đặt bàn | Obento Tensho' },
  { path: 'gio-hang', loadComponent: () => import('./pages/order/cart').then((m) => m.CartPage), title: 'Giỏ hàng | Obento Tensho' },
  { path: 'thanh-toan', canActivate: [verifiedGuard], data: { back: '/gio-hang' }, loadComponent: () => import('./pages/order/checkout').then((m) => m.Checkout), title: 'Thanh toán | Obento Tensho' },
  { path: 'xac-nhan/:code', canActivate: [authGuard], data: { fresh: true }, loadComponent: () => order().then((m) => m.OrderDetail), title: 'Xác nhận đơn | Obento Tensho' },
  { path: 'tra-cuu', loadComponent: lookup, title: 'Tra cứu | Obento Tensho' },
  { path: 'orders/lookup', loadComponent: lookup, title: 'Tra cứu | Obento Tensho' },
  { path: 'reservations/lookup', data: { kind: 'reservation' }, loadComponent: lookup, title: 'Tra cứu | Obento Tensho' },
  { path: 'orders/track', loadComponent: () => order().then((m) => m.Track), title: 'Theo dõi đơn | Obento Tensho' },
  { path: 'reservations/manage', loadComponent: () => order().then((m) => m.ManageReservation), title: 'Đặt bàn | Obento Tensho' },
  { path: 'reservations', redirectTo: 'tai-khoan/dat-ban', pathMatch: 'full' },

  { path: 'gioi-thieu', data: { slug: 'about', heading: 'Giới thiệu' }, loadComponent: staticPage, title: 'Giới thiệu | Obento Tensho' },
  { path: 'chinh-sach-bao-mat', data: { slug: 'privacy', heading: 'Chính sách bảo mật' }, loadComponent: staticPage, title: 'Chính sách bảo mật | Obento Tensho' },
  { path: 'dieu-khoan', data: { slug: 'terms', heading: 'Điều khoản' }, loadComponent: staticPage, title: 'Điều khoản | Obento Tensho' },
  { path: 'chinh-sach-huy-hoan', data: { slug: 'policy', heading: 'Chính sách hủy và hoàn tiền' }, loadComponent: staticPage, title: 'Chính sách hủy và hoàn tiền | Obento Tensho' },
  { path: 'lien-he', loadComponent: () => content().then((m) => m.Contact), title: 'Liên hệ | Obento Tensho' },
  { path: 'khong-gian', loadComponent: () => content().then((m) => m.Gallery), title: 'Không gian | Obento Tensho' },
  { path: 'khuyen-mai', loadComponent: () => content().then((m) => m.Promotions), title: 'Khuyến mãi | Obento Tensho' },
  { path: 'khuyen-mai/:slug', data: { back: '/khuyen-mai' }, loadComponent: () => content().then((m) => m.Promotion), title: 'Khuyến mãi | Obento Tensho' },
  { path: 'tin-tuc', loadComponent: () => content().then((m) => m.NewsList), title: 'Tin tức | Obento Tensho' },
  { path: 'tin-tuc/:slug', data: { back: '/tin-tuc' }, loadComponent: () => content().then((m) => m.NewsArticle), title: 'Tin tức | Obento Tensho' },

  { path: 'dang-nhap', canActivate: [guestGuard], loadComponent: login, title: 'Đăng nhập | Obento Tensho' },
  { path: 'auth/login', canActivate: [guestGuard], loadComponent: login, title: 'Đăng nhập | Obento Tensho' },
  { path: 'dang-ky', canActivate: [guestGuard], loadComponent: () => import('./pages/auth/register').then((m) => m.Register), title: 'Đăng ký | Obento Tensho' },
  { path: 'quen-mat-khau', loadComponent: () => auth().then((m) => m.Forgot), title: 'Quên mật khẩu | Obento Tensho' },
  { path: 'dat-lai-mat-khau', loadComponent: reset, title: 'Đặt lại mật khẩu | Obento Tensho' },
  { path: 'auth/reset-password', loadComponent: reset, title: 'Đặt lại mật khẩu | Obento Tensho' },
  { path: 'xac-thuc-email', data: verifyEmail, loadComponent: tokenAction, title: 'Xác thực email | Obento Tensho' },
  { path: 'auth/verify-email', data: verifyEmail, loadComponent: tokenAction, title: 'Xác thực email | Obento Tensho' },
  { path: 'xac-minh-sdt', canActivate: [authGuard], loadComponent: () => import('./pages/auth/phone-verify').then((m) => m.PhoneVerify), title: 'Xác minh số điện thoại | Obento Tensho' },
  { path: 'xac-minh-email', canActivate: [authGuard], loadComponent: () => import('./pages/auth/phone-verify').then((m) => m.EmailVerify), title: 'Xác thực email | Obento Tensho' },
  { path: 'account/confirm-email', canActivate: [authGuard], data: { title: 'Xác nhận đổi email', endpoint: 'account/confirm-email' }, loadComponent: tokenAction, title: 'Xác nhận đổi email | Obento Tensho' },
  { path: 'newsletter/confirm', data: { title: 'Xác nhận đăng ký bản tin', endpoint: 'newsletter/confirm' }, loadComponent: tokenAction, title: 'Bản tin | Obento Tensho' },
  { path: 'newsletter/unsubscribe', data: { title: 'Hủy nhận bản tin', endpoint: 'newsletter/unsubscribe' }, loadComponent: tokenAction, title: 'Bản tin | Obento Tensho' },
  { path: 'account/points', redirectTo: 'tai-khoan/diem-thuong', pathMatch: 'full' },
  { path: 'account/vouchers', redirectTo: 'tai-khoan/diem-thuong', pathMatch: 'full' },

  { path: 'tai-khoan/don-hang/:code', canActivate: [authGuard], data: { back: '/tai-khoan/don-hang' }, loadComponent: () => order().then((m) => m.OrderDetail), title: 'Chi tiết đơn | Obento Tensho' },
  {
    path: 'tai-khoan',
    canActivate: [authGuard],
    loadComponent: () => account().then((m) => m.Account),
    title: 'Tài khoản | Obento Tensho',
    children: [
      { path: '', redirectTo: 'tong-quan', pathMatch: 'full' },
      { path: 'tong-quan', loadComponent: () => account().then((m) => m.AccountOverview) },
      { path: 'thong-tin', loadComponent: () => account().then((m) => m.AccountProfile) },
      { path: 'dat-ban', loadComponent: () => account().then((m) => m.AccountReservations) },
      { path: 'don-hang', loadComponent: () => account().then((m) => m.AccountOrders) },
      { path: 'diem-thuong', loadComponent: () => account().then((m) => m.AccountPoints) },
      { path: 'danh-gia', loadComponent: () => account().then((m) => m.AccountReviews) },
    ],
  },
  { path: '**', loadComponent: () => content().then((m) => m.NotFound), title: 'Không tìm thấy | Obento Tensho' },
];
