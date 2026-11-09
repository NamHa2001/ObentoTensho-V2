import { cleanLines } from './cart';

describe('Đọc giỏ hàng từ bộ nhớ trình duyệt', () => {
  it('dữ liệu hỏng hoặc không phải danh sách thì coi như giỏ trống', () => {
    expect(cleanLines('rác')).toEqual([]);
    expect(cleanLines({ a: 1 })).toEqual([]);
    expect(cleanLines(null)).toEqual([]);
  });

  it('ép số lượng về 1–99 và bỏ dòng sai kiểu', () => {
    const lines = cleanLines([
      { menuItemId: 5, optionIds: [3, 1], quantity: 999999 },
      { menuItemId: 6, optionIds: [], quantity: -5 },
      { menuItemId: 'x', optionIds: [], quantity: 1 },
      { menuItemId: 7, optionIds: [], quantity: 1.5 },
    ]);
    expect(lines).toEqual([{ menuItemId: 5, optionIds: [1, 3], quantity: 99 }]);
  });

  it('không giữ quá 50 dòng', () => {
    const many = Array.from({ length: 200 }, (_, i) => ({ menuItemId: i + 1, optionIds: [], quantity: 1 }));
    expect(cleanLines(many)).toHaveLength(50);
  });
});
