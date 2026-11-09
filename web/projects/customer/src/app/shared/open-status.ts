import { Schemas } from '@ot/api';

type Hours = Schemas['PublicOpeningHoursDto'];
type Day = Pick<Hours['weekly'][number], 'closed' | 'ranges'>;

export interface OpenStatus {
  open: boolean;
  text: string;
}

const minutes = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));
const crosses = (r: Day['ranges'][number]) => r.close < r.open;

// Giờ đặc biệt của ngày đó được ưu tiên hơn giờ thường kỳ
function dayOf(hours: Hours, date: string, weekday: number): Day | null {
  return hours.special.find((s) => s.date === date) ?? hours.weekly.find((d) => d.dayOfWeek === weekday) ?? null;
}

function vnNow(now: Date): { date: string; weekday: number; minute: number } {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(now);
  const get = (type: string) => parts.find((p) => p.type === type)!.value;
  const date = `${get('year')}-${get('month')}-${get('day')}`;
  return { date, weekday: new Date(`${date}T00:00:00Z`).getUTCDay(), minute: Number(get('hour')) * 60 + Number(get('minute')) };
}

const shiftDate = (date: string, days: number) => new Date(Date.parse(`${date}T00:00:00Z`) + days * 86400000).toISOString().slice(0, 10);

// Khoảng qua nửa đêm của hôm trước vẫn tính là đang mở
export function openStatus(hours: Hours, at: Date): OpenStatus {
  const { date, weekday, minute } = vnNow(at);
  const ranges = (d: Day | null) => (d && !d.closed ? d.ranges : []);
  const today = ranges(dayOf(hours, date, weekday));
  const spill = ranges(dayOf(hours, shiftDate(date, -1), (weekday + 6) % 7)).find((r) => crosses(r) && minute < minutes(r.close));
  const current = spill ?? today.find((r) => minute >= minutes(r.open) && (crosses(r) || minute < minutes(r.close)));
  if (current) {
    return { open: true, text: `Đang mở cửa · đóng lúc ${current.close.slice(0, 5)}` };
  }

  const later = today.map((r) => r.open).filter((o) => minutes(o) > minute).sort()[0];
  if (later) {
    return { open: false, text: `Đã đóng cửa · mở lại lúc ${later.slice(0, 5)}` };
  }

  const next = ranges(dayOf(hours, shiftDate(date, 1), (weekday + 1) % 7)).map((r) => r.open).sort()[0];
  return { open: false, text: next ? `Đã đóng cửa · mở lại lúc ${next.slice(0, 5)} ngày mai` : 'Đã đóng cửa' };
}
