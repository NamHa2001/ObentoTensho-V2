import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { Api } from '../core/api';
import { resource, ResourcePage } from './resource';

interface Dish {
  id: number;
  nameVi: string;
  price: number;
  status: string;
  isActive: boolean;
  allergens: string[];
  version: string;
}

const dish: Dish = { id: 7, nameVi: 'Cơm gà', price: 45000, status: 'sold_out', isActive: true, allergens: ['egg'], version: 'v1' };

const config = resource<Dish>({
  title: 'Món ăn',
  path: 'menu/items',
  canEdit: true,
  columns: [{ key: 'nameVi', label: 'Tên' }, { key: 'price', label: 'Giá', map: (r) => `${r.price / 1000}k` }, { key: 'status', label: 'Trạng thái' }, { key: 'isActive', label: 'Hiện' }],
  fields: [
    { key: 'nameVi', label: 'Tên', required: true },
    { key: 'allergens', label: 'Dị ứng', type: 'multi', options: [{ value: 'egg', label: 'Trứng' }, { value: 'milk', label: 'Sữa' }] },
  ],
});

async function render() {
  const api = { raw: vi.fn(() => of({ data: [dish], meta: { page: 1, pageSize: 20, total: 1 } })), put: vi.fn(() => of(null)) };
  TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: Api, useValue: api }] });
  const fixture = TestBed.createComponent(ResourcePage);
  fixture.componentRef.setInput('resource', config);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return { fixture, api, el };
}

describe('Bảng quản trị dùng chung', () => {
  it('hiện ô theo hàm map, trạng thái tiếng Việt kèm nhãn màu, có/không cho cột đúng sai', async () => {
    const { el } = await render();
    const cells = [...el.querySelectorAll('tbody td')].map((td) => td.textContent?.trim());
    expect(cells.slice(0, 4)).toEqual(['Cơm gà', '45k', 'sold_out', 'Có']);
    expect(el.querySelector('tbody .a-badge-warn')?.textContent?.trim()).toBe('sold_out');
    expect(el.querySelector('tbody .a-badge-good')?.textContent?.trim()).toBe('Có');
  });

  it('mở form sửa thì tích sẵn lựa chọn đang có, lưu gửi kèm phiên bản của dòng', async () => {
    const { fixture, api, el } = await render();
    (el.querySelector('tbody .ot-chip') as HTMLButtonElement).click();
    fixture.detectChanges();
    (el.querySelector('.a-menu .a-drop-item') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const boxes = [...el.querySelectorAll<HTMLInputElement>('fieldset input[type=checkbox]')];
    expect(boxes.map((b) => b.checked)).toEqual([true, false]);

    (el.querySelector('.a-drawer-f .ot-btn-primary') as HTMLButtonElement).click();
    expect(api.put).toHaveBeenCalledWith('menu/items/7', { nameVi: 'Cơm gà', allergens: ['egg'] }, 'v1');
  });
});
