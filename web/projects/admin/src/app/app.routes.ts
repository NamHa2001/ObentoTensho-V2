import { inject } from '@angular/core';
import { Router, Routes } from '@angular/router';

import { Auth, authGuard, guestGuard } from './core/auth';
import { RESOURCES } from './shared/resources';

const login = () => import('./pages/login');
const res = (key: string, cap: string) => ({ path: `r/${key}`, canActivate: [authGuard], data: { cap, resource: RESOURCES[key] }, loadComponent: () => import('./shared/resource').then((m) => m.ResourcePage) });
const page = (path: string, cap: string | undefined, load: () => Promise<any>) => ({ path, canActivate: [authGuard], data: { cap }, loadComponent: load });

export const routes: Routes = [
  { path: 'login', canActivate: [guestGuard], loadComponent: () => login().then((m) => m.Login) },
  { path: 'forgot', loadComponent: () => login().then((m) => m.Forgot) },
  { path: 'auth/reset-password', loadComponent: () => login().then((m) => m.SetPassword) },
  { path: 'auth/accept-invite', data: { invite: true }, loadComponent: () => login().then((m) => m.SetPassword) },
  { path: 'auth/not-me', loadComponent: () => login().then((m) => m.NotMe) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', canActivate: [async () => { const auth = inject(Auth); await auth.init(); return inject(Router).createUrlTree([auth.home()]); }], children: [] },
      page('dashboard', 'dashboard', () => import('./pages/dashboard').then((m) => m.Dashboard)),
      page('orders', 'orders_cashier', () => import('./pages/orders').then((m) => m.Orders)),
      page('orders/:id', 'orders_cashier', () => import('./pages/orders').then((m) => m.Order)),
      page('kitchen', 'orders_kitchen', () => import('./pages/kitchen').then((m) => m.Kitchen)),
      page('reservations', 'reservations', () => import('./pages/reservations').then((m) => m.Reservations)),
      page('reservations/new', 'reservations', () => import('./pages/reservations').then((m) => m.ReservationNew)),
      page('reservations/:id', 'reservations', () => import('./pages/reservations').then((m) => m.Reservation)),
      page('calendar', 'reservations', () => import('./pages/calendar').then((m) => m.Calendar)),
      page('floor', 'tables_view', () => import('./pages/floor').then((m) => m.Floor)),
      page('option-groups', 'menu', () => import('./pages/menu').then((m) => m.OptionGroups)),
      page('item-images/:id', 'menu', () => import('./pages/menu').then((m) => m.ItemImages)),
      page('customers', 'customers', () => import('./pages/customers').then((m) => m.Customers)),
      page('customers/:id', 'customers', () => import('./pages/customers').then((m) => m.Customer)),
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
