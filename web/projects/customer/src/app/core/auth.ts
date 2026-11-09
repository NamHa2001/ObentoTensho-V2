import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CanActivateFn, Router } from '@angular/router';
import { Schemas } from '@ot/api';
import { catchError, finalize, firstValueFrom, map, Observable, of, shareReplay, switchMap, tap, throwError } from 'rxjs';

import { Api } from './api';
import { Turnstile } from './turnstile';

export type Profile = Schemas['ProfileDto'];
const CLIENT = { 'X-OT-Client': 'web' };

// SEC-03: access token chỉ giữ trong bộ nhớ; phiên được khôi phục bằng cookie refresh HttpOnly
@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly api = inject(Api);
  private readonly turnstile = inject(Turnstile);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private refreshing: Observable<string | null> | null = null;
  private started: Promise<void> | null = null;

  readonly token = signal<string | null>(null);
  readonly profile = signal<Profile | null>(null);

  init(): Promise<void> {
    this.started ??= this.browser ? firstValueFrom(this.refresh().pipe(switchMap((t) => (t ? this.loadProfile() : of(null))), map(() => undefined), catchError(() => of(undefined)))) : Promise.resolve();
    return this.started;
  }

  login(email: string, password: string): Observable<Profile> {
    const send = (headers?: Record<string, string>) => this.api.post<Schemas['AccessTokenResponse']>('auth/login', { email, password }, headers);
    // SEC-51: sau vài lần sai server mới đòi Turnstile
    return send().pipe(
      catchError((e: HttpErrorResponse) => ((e.error as { errorCode?: string } | null)?.errorCode === 'CAPTCHA_FAILED' ? this.turnstile.headers().pipe(switchMap(send)) : throwError(() => e))),
      tap((r) => this.token.set(r.accessToken)),
      switchMap(() => this.loadProfile()),
    );
  }

  loadProfile(): Observable<Profile> {
    return this.api.get<Profile>('account/profile').pipe(tap((p) => this.profile.set(p)));
  }

  refresh(): Observable<string | null> {
    this.refreshing ??= this.api.post<Schemas['AccessTokenResponse']>('auth/refresh-token', {}, CLIENT).pipe(
      map((r) => r.accessToken),
      catchError(() => of(null)),
      tap((t) => {
        this.token.set(t);
        if (!t) {
          this.profile.set(null);
        }
      }),
      finalize(() => (this.refreshing = null)),
      shareReplay(1),
    );
    return this.refreshing;
  }

  logout(): Observable<void> {
    return this.api.post('auth/logout', {}, CLIENT).pipe(
      catchError(() => of(undefined)),
      tap(() => {
        this.token.set(null);
        this.profile.set(null);
      }),
    );
  }
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(Auth);
  const withToken = (token: string | null) => (token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);
  if (!req.url.includes('/api/v1/') || req.url.includes('/api/v1/auth/')) {
    return next(req);
  }

  return next(withToken(auth.token())).pipe(
    catchError((e: HttpErrorResponse) => {
      if (e.status !== 401 || !auth.token()) {
        return throwError(() => e);
      }

      return auth.refresh().pipe(switchMap((token) => (token ? next(withToken(token)) : throwError(() => e))));
    }),
  );
};

export const authGuard: CanActivateFn = async (_, state) => {
  const auth = inject(Auth);
  const router = inject(Router);
  await auth.init();
  return auth.profile() ? true : router.createUrlTree(['/dang-nhap'], { queryParams: { next: state.url } });
};

// Đặt bàn, đặt món: bắt buộc tài khoản đã xác minh SĐT
// Đã đăng nhập thì không vào lại trang đăng nhập/đăng ký
export const guestGuard: CanActivateFn = async () => {
  const auth = inject(Auth);
  const router = inject(Router);
  await auth.init();
  return auth.profile() ? router.createUrlTree(['/tai-khoan']) : true;
};

export const verifiedGuard: CanActivateFn = async (_, state) => {
  const auth = inject(Auth);
  const router = inject(Router);
  await auth.init();
  const profile = auth.profile();
  if (!profile) {
    return router.createUrlTree(['/dang-nhap'], { queryParams: { next: state.url } });
  }

  return profile.isPhoneVerified ? true : router.createUrlTree(['/xac-minh-sdt'], { queryParams: { next: state.url } });
};
