// Đích quay lại sau đăng nhập lấy từ đường dẫn nên có thể bị sửa: chỉ nhận đường dẫn nội bộ của trang khách
export function safeNext(next: string | null | undefined, fallback = '/tai-khoan'): string {
  if (!next || !next.startsWith('/') || next.startsWith('//') || next.includes('\\') || /[\u0000-\u001f]/.test(next)) {
    return fallback;
  }

  return next;
}
