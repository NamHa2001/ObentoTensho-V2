import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Schemas } from '@ot/api';
import { catchError, finalize, firstValueFrom, from, map, Observable, of, shareReplay, switchMap, tap, throwError } from 'rxjs';

import { Api } from './api';

export type Me = Schemas['StaffMeDto'];
const CLIENT_HEADER = 'X-OT-Client';

@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly api = inject(Api);
  private refreshing: Observable<string | null> | null = null;
  private started: Promise<void> | null = null;
  readonly token = signal<string | null>(null);
  readonly me = signal<Me | null>(null);

  init(): Promise<void> {
    this.started ??= firstValueFrom(this.refresh().pipe(switchMap((t) => (t ? this.loadMe() : of(null))), map(() => undefined), catchError(() => of(undefined))));
    return this.started;
  }

  has(capability: string): boolean {
    return (this.me()?.capabilities as string[] | undefined)?.includes(capability) ?? false;
  }

  // Trang mặc định theo quyền, tránh đưa nhân viên tới trang bị chặn
  home(): string {
    if (this.has('dashboard')) return '/dashboard';
    if (this.has('orders_kitchen')) return '/kitchen';
    if (this.has('orders_cashier')) return '/orders';
    if (this.has('content')) return '/r/banners';
    return '/account';
  }

  startSession(session: Schemas['AccessTokenResponse']): Observable<Me> {
    this.token.set(session.accessToken);
    return this.loadMe();
  }

  loadMe(): Observable<Me> {
    return this.api.get<Me>('staff/me').pipe(tap((m) => this.me.set(m)));
  }

  refresh(): Observable<string | null> {
    this.refreshing ??= this.api.post<Schemas['AccessTokenResponse']>('auth/refresh-token', {}, null).pipe(
      map((r) => r.accessToken),
      catchError(() => of(null)),
      tap((t) => {
        this.token.set(t);
        if (!t) {
          this.me.set(null);
        }
      }),
      finalize(() => (this.refreshing = null)),
      shareReplay(1),
    );
    return this.refreshing;
  }

  logout(): Observable<unknown> {
    return this.api.post('auth/logout').pipe(
      catchError(() => of(null)),
      tap(() => {
        this.token.set(null);
        this.me.set(null);
      }),
    );
  }
}

// SEC-61: thao tác nhạy cảm cần nhập lại mật khẩu; hộp thoại nằm ở khung trang
@Injectable({ providedIn: 'root' })
export class StepUp {
  readonly open = signal(false);
  private resolve: ((token: string | null) => void) | null = null;
  private pending: Promise<string | null> | null = null;

  // Nhiều yêu cầu cùng cần xác thực lại thì chờ chung một hộp, không yêu cầu nào bị treo
  ask(): Promise<string | null> {
    if (!this.pending) {
      this.open.set(true);
      this.pending = new Promise((r) => (this.resolve = r));
    }

    return this.pending;
  }

  finish(token: string | null): void {
    this.open.set(false);
    this.resolve?.(token);
    this.resolve = null;
    this.pending = null;
  }
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(Auth);
  const stepUp = inject(StepUp);
  const router = inject(Router);
  const cookieCall = req.url.endsWith('/auth/refresh-token') || req.url.endsWith('/auth/logout');
  const base = cookieCall ? req.clone({ setHeaders: { [CLIENT_HEADER]: 'admin' } }) : req;
  const send = (token: string | null, step?: string) => {
    const headers: Record<string, string> = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;
    if (step) headers['X-Step-Up-Token'] = step;
    return next(base.clone({ setHeaders: headers }));
  };

  if (cookieCall || req.url.includes('/admin/auth/login') || req.url.includes('/admin/auth/2fa')) {
    return next(base);
  }

  return send(auth.token()).pipe(
    catchError((e: HttpErrorResponse) => {
      const code = (e.error as { errorCode?: string } | null)?.errorCode;
      if (code === 'STEP_UP_REQUIRED') {
        return from(stepUp.ask()).pipe(switchMap((t) => (t ? send(auth.token(), t) : throwError(() => e))));
      }

      if (e.status !== 401 || !auth.token()) {
        return throwError(() => e);
      }

      return auth.refresh().pipe(
        switchMap((t) => {
          if (!t) {
            void router.navigateByUrl('/login');
            return throwError(() => e);
          }

          return send(t);
        }),
      );
    }),
  );
};

export const guestGuard: CanActivateFn = async () => {
  const auth = inject(Auth);
  const router = inject(Router);
  await auth.init();
  return auth.me() ? router.createUrlTree([auth.home()]) : true;
};

export const authGuard: CanActivateFn = async (route) => {
  const auth = inject(Auth);
  const router = inject(Router);
  await auth.init();
  if (!auth.me()) {
    return router.createUrlTree(['/login']);
  }

  const cap = route.data['cap'] as string | undefined;
  return !cap || auth.has(cap) ? true : router.createUrlTree([auth.home()]);
};
