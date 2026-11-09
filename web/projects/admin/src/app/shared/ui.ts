import { signal } from '@angular/core';
import { Observable, Subscription } from 'rxjs';

import { errorText } from '../../../../customer/src/app/shared/form';

export { errorCode, errorText, Submit } from '../../../../customer/src/app/shared/form';
export { ORDER_STATUS, RESERVATION_STATUS, TIERS, VnDatePipe, imageUrl, today } from '../../../../customer/src/app/shared/format';
export { VndPipe } from '../../../../customer/src/app/shared/vnd.pipe';

// API bắt buộc mô tả ảnh; mặc định lấy tên tệp
export function altOf(file: File): string {
  return file.name.replace(/\.[^.]+$/, '').slice(0, 200) || 'Ảnh';
}

// Tải danh sách: chỉ lấy kết quả của lần gọi mới nhất. Dùng thay Submit cho việc đọc, vì realtime có thể gọi tải lại liên tiếp
export class Latest {
  private current: Subscription | null = null;
  readonly error = signal<string | null>(null);

  run<T>(request: Observable<T>, done: (value: T) => void): void {
    this.current?.unsubscribe();
    this.current = request.subscribe({
      next: (value) => {
        this.error.set(null);
        done(value);
      },
      error: (e: unknown) => this.error.set(errorText(e)),
    });
  }
}

// Ô nhập datetime-local là giờ Việt Nam; API nhận và trả UTC
export function vnInputFromUtc(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }

  const parts = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
  return parts.replace(' ', 'T');
}

export function utcFromVnInput(value: string | null | undefined): string | null {
  return value ? new Date(`${value}:00+07:00`).toISOString() : null;
}
