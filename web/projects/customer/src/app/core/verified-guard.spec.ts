import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { of } from 'rxjs';

import { Auth, ContactPolicy, Profile, verifiedGuard } from './auth';

function setup(profile: Partial<Profile> | null, requirePhone: boolean): void {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: Auth, useValue: { init: () => Promise.resolve(), profile: signal(profile) } },
      { provide: ContactPolicy, useValue: Object.assign(Object.create(ContactPolicy.prototype) as ContactPolicy, { requirePhone$: of(requirePhone) }) },
    ],
  });
}

async function run(): Promise<string | true> {
  const result = (await TestBed.runInInjectionContext(() => verifiedGuard({} as ActivatedRouteSnapshot, { url: '/thanh-toan' } as RouterStateSnapshot))) as UrlTree | true;
  return result === true ? true : TestBed.inject(Router).serializeUrl(result);
}

describe('Chặn trang đặt món, đặt bàn khi tài khoản chưa xác minh', () => {
  it('tắt bắt buộc SĐT: email đã xác thực thì vào được', async () => {
    setup({ isEmailVerified: true, isPhoneVerified: false }, false);
    expect(await run()).toBe(true);
  });

  it('tắt bắt buộc SĐT nhưng email chưa xác thực', async () => {
    setup({ isEmailVerified: false, isPhoneVerified: false }, false);
    expect(await run()).toBe('/xac-minh-email?next=%2Fthanh-toan');
  });

  it('bật bắt buộc SĐT: chưa xác minh SĐT thì sang trang xác minh SĐT', async () => {
    setup({ isEmailVerified: true, isPhoneVerified: false }, true);
    expect(await run()).toBe('/xac-minh-sdt?next=%2Fthanh-toan');
  });

  it('chưa đăng nhập thì sang trang đăng nhập', async () => {
    setup(null, false);
    expect(await run()).toBe('/dang-nhap?next=%2Fthanh-toan');
  });
});
