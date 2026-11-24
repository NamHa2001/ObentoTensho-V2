import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { Observable } from 'rxjs';

const FIELD_ERRORS: Record<string, string> = {
  REQUIRED: 'chưa nhập',
  TOO_SHORT: 'quá ngắn',
  TOO_LONG: 'quá dài',
  INVALID_FORMAT: 'không đúng định dạng',
  OUT_OF_RANGE: 'ngoài khoảng cho phép',
  PASSWORD_TOO_COMMON: 'quá phổ biến, hãy chọn mật khẩu khác',
  PASSWORD_CONTAINS_EMAIL: 'không được chứa email',
  SAME_AS_CURRENT: 'phải khác hiện tại',
};

const FIELD_NAMES: Record<string, string> = {
  fullname: 'Họ tên',
  guestname: 'Họ tên',
  customername: 'Họ tên',
  email: 'Email',
  guestemail: 'Email',
  customeremail: 'Email',
  newemail: 'Email mới',
  phonenumber: 'Số điện thoại',
  guestphone: 'Số điện thoại',
  customerphone: 'Số điện thoại',
  password: 'Mật khẩu',
  newpassword: 'Mật khẩu mới',
  message: 'Nội dung',
  'invoice.buyername': 'Tên người mua / công ty',
  'invoice.nationalid': 'Số CCCD',
  'invoice.taxcode': 'Mã số thuế',
  'invoice.address': 'Địa chỉ',
  'invoice.email': 'Email nhận hóa đơn',
  'invoice.phone': 'SĐT người mua',
  invoicenumber: 'Số hóa đơn',
  reason: 'Lý do',
  memberphone: 'SĐT thành viên',
  guestcount: 'Số khách',
  name: 'Tên',
  title: 'Tiêu đề',
  slug: 'Đường dẫn',
  price: 'Giá',
  categoryid: 'Danh mục',
  startdate: 'Từ ngày',
  enddate: 'Đến ngày',
  contenthtml: 'Nội dung',
  date: 'Ngày',
  time: 'Giờ',
  code: 'Mã',
  note: 'Ghi chú',
};

// labels: nhãn của form đang mở (khóa viết thường) để gọi trường đúng tên người dùng thấy.
// Không bao giờ in tên trường kỹ thuật ("request", "$.categoryId"...) hay mã lỗi thô ra màn hình
export function errorText(e: unknown, labels: Record<string, string> = {}): string {
  const response = e as HttpErrorResponse;
  if (response.status === 0) {
    return 'Không kết nối được máy chủ, vui lòng thử lại.';
  }

  const body = response.error as { title?: string; errors?: Record<string, string[]> } | null;
  if (body?.errors) {
    const lines: string[] = [];
    let unknown = false;
    for (const [field, codes] of Object.entries(body.errors)) {
      const key = field.replace(/^\$\./, '').toLowerCase();
      const name = labels[key] ?? FIELD_NAMES[key];
      const reason = FIELD_ERRORS[codes[0]];
      if (name && reason) {
        lines.push(`${name}: ${reason}`);
      } else {
        unknown = true;
      }
    }

    if (unknown) {
      lines.push(lines.length > 0 ? 'Một số ô khác chưa đúng' : 'Vui lòng kiểm tra lại các ô đã nhập');
    }

    return lines.join('. ');
  }

  return body?.title ?? 'Có lỗi xảy ra, vui lòng thử lại.';
}

export function errorCode(e: unknown): string | null {
  return ((e as HttpErrorResponse).error as { errorCode?: string } | null)?.errorCode ?? null;
}

export class Submit {
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  // Nhãn các ô của form hiện tại, để lỗi từ server gọi đúng tên ô
  labels: Record<string, string> = {};

  run<T>(request: Observable<T>, done: (value: T) => void, failed?: (e: unknown) => boolean): void {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: (value) => {
        this.busy.set(false);
        done(value);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        if (!failed?.(e)) {
          this.error.set(errorText(e, this.labels));
        }
      },
    });
  }
}
