import { Pipe, PipeTransform } from '@angular/core';
import { Schemas } from '@ot/api';

export const ORDER_STATUS: Record<string, string> = { pending: 'Chờ xác nhận', preparing: 'Đang chuẩn bị', ready: 'Sẵn sàng', delivering: 'Đang giao', completed: 'Hoàn thành', cancelled: 'Đã hủy' };
export const RESERVATION_STATUS: Record<string, string> = { pending: 'Chờ xác nhận', confirmed: 'Đã xác nhận', checked_in: 'Đã đến', completed: 'Hoàn thành', cancelled: 'Đã hủy', no_show: 'Không đến' };
export const TIERS: Record<string, string> = { bronze: 'Đồng', silver: 'Bạc', gold: 'Vàng', diamond: 'Kim cương' };

// yyyy-MM-dd hoặc thời điểm UTC ⇒ ngày giờ Việt Nam
@Pipe({ name: 'vnDate' })
export class VnDatePipe implements PipeTransform {
  transform(value: string | null | undefined, withTime = false): string {
    if (!value) {
      return '';
    }

    if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
      const [y, m, d] = value.split('-');
      return `${d}/${m}/${y}`;
    }

    const date = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value));
    if (!withTime) {
      return date;
    }

    const time = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(value));
    return `${time} ${date}`;
  }
}

export function imageUrl(image: Schemas['ImageDto'] | null | undefined): string | null {
  return image?.variants.at(-1)?.url ?? null;
}

export function today(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
}

export function addDays(date: string, days: number): string {
  return new Date(Date.parse(`${date}T00:00:00Z`) + days * 86400000).toISOString().slice(0, 10);
}

// Số lưu dạng +84…, khách quen đọc và gõ dạng 0…
export function localPhone(phone: string | null | undefined): string {
  return phone ? phone.replace(/^\+84/, '0') : '';
}

// Người Việt được gọi bằng tên, là từ cuối của họ tên
export function givenName(fullName: string): string {
  return fullName.trim().split(/\s+/).at(-1) ?? fullName;
}
