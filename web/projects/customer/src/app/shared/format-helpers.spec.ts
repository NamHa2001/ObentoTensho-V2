import { addDays, localPhone, VnDatePipe } from './format';

describe('Ngày và số điện thoại', () => {
  it('cộng ngày qua cuối tháng và cuối năm', () => {
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
  });

  it('đổi +84 sang dạng 0 quen thuộc, giữ nguyên số đã là dạng 0', () => {
    expect(localPhone('+84935766574')).toBe('0935766574');
    expect(localPhone('0935766574')).toBe('0935766574');
    expect(localPhone(null)).toBe('');
    expect(localPhone('84935766574')).toBe('0935766574');
  });
});

describe('Hiển thị ngày giờ Việt Nam', () => {
  const pipe = new VnDatePipe();

  it('năm luôn đủ 4 chữ số, đổi sang giờ Việt Nam', () => {
    expect(pipe.transform('2026-11-16T03:00:00Z')).toBe('16/11/2026');
    expect(pipe.transform('2026-11-16T03:00:00Z', true)).toBe('10:00 16/11/2026');
  });

  it('ngày dạng yyyy-MM-dd giữ nguyên ngày', () => {
    expect(pipe.transform('2026-11-09')).toBe('09/11/2026');
  });
});
