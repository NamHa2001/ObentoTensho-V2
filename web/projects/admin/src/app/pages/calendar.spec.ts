import { monthCells, shiftMonth } from './calendar';

describe('Lịch tháng', () => {
  it('tháng 11/2026 bắt đầu Chủ nhật nên có 6 ô trống đầu', () => {
    const cells = monthCells('2026-11');
    expect(cells.slice(0, 6).every((c) => c === null)).toBe(true);
    expect(cells[6]).toEqual({ date: '2026-11-01', day: 1 });
    expect(cells.at(-1)).toEqual({ date: '2026-11-30', day: 30 });
  });

  it('tháng 2 năm nhuận có 29 ngày', () => {
    expect(monthCells('2028-02').at(-1)?.day).toBe(29);
  });

  it('chuyển tháng qua năm', () => {
    expect(shiftMonth('2026-12', 1)).toBe('2027-01');
    expect(shiftMonth('2026-01', -1)).toBe('2025-12');
  });
});
