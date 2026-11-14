import { Location } from '@angular/common';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';

import { Back } from './back';

@Component({ template: '' })
class Blank {}

async function setup(...urls: string[]): Promise<{ back: Back; router: Router; location: Location }> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'menu', component: Blank },
        { path: 'menu/:slug', data: { back: '/menu' }, component: Blank },
        { path: 'gio-hang', component: Blank },
      ]),
    ],
  });
  const back = TestBed.inject(Back);
  const router = TestBed.inject(Router);
  for (const url of urls) {
    await router.navigateByUrl(url);
  }

  return { back, router, location: TestBed.inject(Location) };
}

describe('Nút quay lại trên thanh tiêu đề', () => {
  it('trang không khai báo trang cha thì không hiện nút', async () => {
    const { back } = await setup('/menu');
    expect(back.target()).toBeNull();
  });

  it('vừa đi từ thực đơn sang chi tiết món thì lùi lịch sử để giữ bộ lọc', async () => {
    const { back, location } = await setup('/menu?danhMuc=com', '/menu/com-ga');
    const spy = vi.spyOn(location, 'back');
    back.go();
    expect(spy).toHaveBeenCalled();
  });

  it('vào thẳng chi tiết món từ chỗ khác thì mở trang thực đơn', async () => {
    const { back, router } = await setup('/gio-hang', '/menu/com-ga');
    const spy = vi.spyOn(router, 'navigateByUrl');
    back.go();
    expect(spy).toHaveBeenCalledWith('/menu');
  });
});
