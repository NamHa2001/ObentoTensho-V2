import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Cart } from '../../core/cart';
import { MenuDetail } from './menu-detail';

const option = (id: number, name: string, priceDelta: number, isDefault: boolean) => ({ id, name, priceDelta, isDefault, status: 'available', allergens: [] });

const detail = {
  item: {
    id: 7, slug: 'com', categoryId: 1, name: 'Cơm', shortDesc: null, price: 65000, originalPrice: null, status: 'available', allowTakeaway: true,
    labels: { isNew: false, isBestseller: false, isFeatured: false, isVegetarian: false, isSpicy: false }, allergens: [], image: null, contentLang: 'vi', isFallback: false,
  },
  fullDescHtml: null,
  images: [],
  optionGroups: [{ id: 1, name: 'Size', selectionType: 'one', isRequired: true, minSelect: 1, maxSelect: 1, options: [option(1, 'Vừa', 0, false), option(2, 'Lớn', 15000, false)] }],
  related: [],
};

describe('Trang chi tiết món', () => {
  async function open() {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(MenuDetail);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne((r) => r.url.endsWith('/menu/items/')).flush({ data: detail });
    http.expectOne((r) => r.url.includes('/reviews')).flush({ data: [] });
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('khóa nút tới khi chọn đủ, chọn rồi cộng giá và thêm đúng vào giỏ', async () => {
    const fixture = await open();
    const root: HTMLElement = fixture.nativeElement;
    const button = () => root.querySelector<HTMLButtonElement>('.ot-buy-bar .ot-btn')!;
    expect(button().disabled).toBe(true);
    expect(button().textContent).toContain('Chọn đủ');

    root.querySelectorAll<HTMLInputElement>('input[type=radio]')[1].dispatchEvent(new Event('change'));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(button().disabled).toBe(false);
    expect(button().textContent).toContain('80.000');

    button().click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(TestBed.inject(Cart).lines()).toEqual([{ menuItemId: 7, optionIds: [2], quantity: 1 }]);
    expect(button().textContent).toContain('Đã thêm');
  });
});
