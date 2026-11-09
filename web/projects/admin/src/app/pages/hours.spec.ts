import { rangesOverlap } from './hours';

describe('Khoảng giờ trong ngày', () => {
  it('hai ca tách nhau thì hợp lệ', () => {
    expect(rangesOverlap([{ open: '10:00', close: '14:00' }, { open: '17:00', close: '21:00' }])).toBe(false);
  });

  it('ca sau bắt đầu đúng lúc ca trước kết thúc thì hợp lệ', () => {
    expect(rangesOverlap([{ open: '10:00', close: '14:00' }, { open: '14:00', close: '21:00' }])).toBe(false);
  });

  it('phát hiện chồng giờ, không phụ thuộc thứ tự nhập', () => {
    expect(rangesOverlap([{ open: '17:00', close: '21:00' }, { open: '10:00', close: '18:00' }])).toBe(true);
  });

  it('tính đúng khoảng qua nửa đêm', () => {
    expect(rangesOverlap([{ open: '22:00', close: '02:00' }, { open: '23:00', close: '23:30' }])).toBe(true);
  });
});
