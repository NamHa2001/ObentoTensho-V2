import { Schemas } from '@ot/api';

import { addLine } from '../core/cart';
import { defaultSelection, isComplete, requiredCount, toggle, unitPrice } from './option-selection';

type Group = Schemas['PublicOptionGroupDto'];

function option(id: number, priceDelta = 0, extra: Partial<Schemas['PublicOptionDto']> = {}): Schemas['PublicOptionDto'] {
  return { id, name: `O${id}`, priceDelta, isDefault: false, status: 'available', allergens: [], ...extra };
}

const size: Group = { id: 1, name: 'Size', selectionType: 'one', isRequired: true, minSelect: 1, maxSelect: 1, options: [option(1, 0, { isDefault: true }), option(2, 15000)] };
const topping: Group = { id: 2, name: 'Topping', selectionType: 'many', isRequired: false, minSelect: 0, maxSelect: 2, options: [option(3, 5000), option(4, 7000), option(5, 9000)] };

describe('Chọn tùy chọn món', () => {
  it('chọn sẵn mục mặc định còn hàng, bỏ qua mục đã hết', () => {
    const soldOut: Group = { ...size, options: [option(1, 0, { isDefault: true, status: 'sold_out' }), option(2)] };
    expect(defaultSelection([size])).toEqual({ 1: [1] });
    expect(defaultSelection([soldOut])).toEqual({ 1: [] });
  });

  it('chọn một thì thay mục cũ', () => {
    expect(toggle({ 1: [1] }, size, 2)).toEqual({ 1: [2] });
  });

  it('chọn nhiều không vượt tối đa và bỏ chọn được', () => {
    let s = toggle({}, topping, 3);
    s = toggle(s, topping, 4);
    expect(toggle(s, topping, 5)).toEqual({ 2: [3, 4] });
    expect(toggle(s, topping, 3)).toEqual({ 2: [4] });
  });

  it('nhóm bắt buộc chưa chọn thì chưa đủ', () => {
    expect(requiredCount({ ...size, minSelect: 0 })).toBe(1);
    expect(isComplete({ 1: [] }, [size])).toBe(false);
    expect(isComplete({ 1: [1] }, [size, topping])).toBe(true);
  });

  it('giá một phần gồm các mục đã chọn', () => {
    expect(unitPrice(65000, { 1: [2], 2: [3, 4] }, [size, topping])).toBe(92000);
  });
});

describe('Giỏ hàng', () => {
  it('cùng món cùng tùy chọn thì cộng dồn, không vượt 99', () => {
    const lines = addLine([{ menuItemId: 1, optionIds: [2, 3], quantity: 98 }], 1, [3, 2], 5);
    expect(lines).toEqual([{ menuItemId: 1, optionIds: [2, 3], quantity: 99 }]);
  });

  it('khác tùy chọn thì tách dòng', () => {
    expect(addLine([{ menuItemId: 1, optionIds: [2], quantity: 1 }], 1, [3], 1)).toHaveLength(2);
  });
});
