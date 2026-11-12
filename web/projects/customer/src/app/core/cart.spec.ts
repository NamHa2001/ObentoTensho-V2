import { addLine, cleanLines, setLineNote } from './cart';

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

  it('ghi chú bị sửa tay thì làm sạch và cắt còn 200 ký tự', () => {
    const lines = cleanLines([
      { menuItemId: 1, optionIds: [], quantity: 1, note: '  ít\u0000  cay  ' },
      { menuItemId: 2, optionIds: [], quantity: 1, note: 'x'.repeat(500) },
      { menuItemId: 3, optionIds: [], quantity: 1, note: 123 },
    ]);
    expect(lines.map((l) => l.note)).toEqual(['ít cay', 'x'.repeat(200), undefined]);
  });

  it('cùng món khác ghi chú thì tách dòng, cùng ghi chú thì cộng dồn', () => {
    let lines = addLine([], 1, [2], 1, 'ít cay');
    lines = addLine(lines, 1, [2], 1, '');
    lines = addLine(lines, 1, [2], 2, ' ít cay ');
    expect(lines).toEqual([
      { menuItemId: 1, optionIds: [2], quantity: 3, note: 'ít cay' },
      { menuItemId: 1, optionIds: [2], quantity: 1 },
    ]);
  });

  it('sửa ghi chú ở giỏ giữ vị trí dòng, trùng dòng khác thì gộp', () => {
    const lines = [
      { menuItemId: 1, optionIds: [], quantity: 1, note: 'ít cay' },
      { menuItemId: 2, optionIds: [], quantity: 1 },
      { menuItemId: 1, optionIds: [], quantity: 2 },
    ];
    expect(setLineNote(lines, 1, 'không hành')[1]).toEqual({ menuItemId: 2, optionIds: [], quantity: 1, note: 'không hành' });
    expect(setLineNote(lines, 2, 'ít cay')).toEqual([{ menuItemId: 1, optionIds: [], quantity: 3, note: 'ít cay' }, { menuItemId: 2, optionIds: [], quantity: 1 }]);
  });

  it('không giữ quá 50 dòng', () => {
    const many = Array.from({ length: 200 }, (_, i) => ({ menuItemId: i + 1, optionIds: [], quantity: 1 }));
    expect(cleanLines(many)).toHaveLength(50);
  });
});
