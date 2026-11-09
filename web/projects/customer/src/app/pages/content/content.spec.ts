import { isEmbeddable } from './content';

describe('Nhúng video', () => {
  it('chỉ nhận https từ nguồn đã cho phép', () => {
    expect(isEmbeddable('https://www.youtube.com/embed/abc')).toBe(true);
    expect(isEmbeddable('http://www.youtube.com/embed/abc')).toBe(false);
    expect(isEmbeddable('https://evil.test/embed')).toBe(false);
    expect(isEmbeddable('javascript:alert(1)')).toBe(false);
    expect(isEmbeddable('khong phai url')).toBe(false);
  });
});
