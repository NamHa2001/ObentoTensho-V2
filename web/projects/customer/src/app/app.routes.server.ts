import { RenderMode, ServerRoute } from '@angular/ssr';

// Trang nội dung dựng ở server cho SEO; trang cần phiên đăng nhập, giỏ hàng, token trong # chỉ chạy ở trình duyệt
const server = ['', 'menu', 'menu/:slug', 'gioi-thieu', 'chinh-sach-bao-mat', 'dieu-khoan', 'chinh-sach-huy-hoan', 'lien-he', 'khong-gian', 'khuyen-mai', 'khuyen-mai/:slug', 'tin-tuc', 'tin-tuc/:slug'];

export const serverRoutes: ServerRoute[] = [
  ...server.map((path) => ({ path, renderMode: RenderMode.Server as const })),
  { path: '**', renderMode: RenderMode.Client },
];
