import { Schemas } from '@ot/api';

import { openStatus } from './open-status';

const day = (dayOfWeek: number, ranges: { open: string; close: string }[], closed = false) => ({ dayOfWeek, closed, ranges }) as never;
const hours = (weekly: never[], special: never[] = []) => ({ weekly, special }) as Schemas['PublicOpeningHoursDto'];
// 09/11/2026 là thứ Hai (1); giờ Việt Nam là UTC+7
const at = (time: string, date = '2026-11-09') => new Date(`${date}T${time}:00+07:00`);

describe('Trạng thái mở cửa', () => {
  const twoShifts = hours([day(1, [{ open: '10:00:00', close: '14:00:00' }, { open: '17:00:00', close: '21:00:00' }]), day(2, [{ open: '09:30:00', close: '21:00:00' }])]);

  it('đang trong ca thì báo giờ đóng của ca đó', () => {
    expect(openStatus(twoShifts, at('11:00'))).toEqual({ open: true, text: 'Đang mở cửa · đóng lúc 14:00' });
  });

  it('giữa hai ca thì báo giờ mở lại', () => {
    expect(openStatus(twoShifts, at('15:00'))).toEqual({ open: false, text: 'Đã đóng cửa · mở lại lúc 17:00' });
  });

  it('hết ca cuối thì báo giờ mở ngày mai', () => {
    expect(openStatus(twoShifts, at('21:00')).text).toBe('Đã đóng cửa · mở lại lúc 09:30 ngày mai');
  });

  it('khoảng qua nửa đêm của hôm trước vẫn đang mở', () => {
    const late = hours([day(0, [{ open: '18:00:00', close: '02:00:00' }]), day(1, [])]);
    expect(openStatus(late, at('01:00')).text).toBe('Đang mở cửa · đóng lúc 02:00');
  });

  it('giờ đặc biệt được ưu tiên', () => {
    const closedToday = hours([day(1, [{ open: '10:00:00', close: '21:00:00' }])], [{ date: '2026-11-09', closed: true, ranges: [] } as never]);
    expect(openStatus(closedToday, at('11:00')).open).toBe(false);
  });
});
