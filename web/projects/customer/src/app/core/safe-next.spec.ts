import { safeNext } from './safe-next';

describe('Đích quay lại sau đăng nhập', () => {
  it('giữ đường dẫn nội bộ', () => {
    expect(safeNext('/dat-ban')).toBe('/dat-ban');
    expect(safeNext('/menu?danhMuc=com-hop')).toBe('/menu?danhMuc=com-hop');
  });

  it('chặn trang ngoài và đường dẫn lạ', () => {
    for (const bad of ['https://trang-lua-dao.com', '//trang-lua-dao.com', '/\\trang-lua-dao.com', 'javascript:alert(1)', 'dat-ban', '/dat\nban', '']) {
      expect(safeNext(bad)).toBe('/tai-khoan');
    }
    expect(safeNext(null)).toBe('/tai-khoan');
  });
});
