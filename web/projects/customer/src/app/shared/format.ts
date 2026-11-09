import { Pipe, PipeTransform } from '@angular/core';
import { Schemas } from '@ot/api';

export const ORDER_STATUS: Record<string, string> = { pending: 'Chờ xác nhận', preparing: 'Đang chuẩn bị', ready: 'Sẵn sàng lấy', completed: 'Hoàn thành', cancelled: 'Đã hủy' };
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

    return new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: withTime ? 'short' : undefined }).format(new Date(value));
  }
}

export function imageUrl(image: Schemas['ImageDto'] | null | undefined): string | null {
  return image?.variants.at(-1)?.url ?? null;
}

export function today(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
}
