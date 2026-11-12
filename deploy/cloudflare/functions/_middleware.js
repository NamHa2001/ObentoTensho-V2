// Cloudflare Pages: phục vụ giao diện tĩnh, chuyển /api, /uploads, /hubs sang máy chủ API.
// Trình duyệt chỉ thấy một tên miền nên cookie phiên (SameSite=Strict) và kiểm tra Origin của API giữ nguyên.
// Biến môi trường API_ORIGIN (đặt trong cài đặt dự án Pages), ví dụ https://obento-api.runasp.net
const PROXIED = /^\/(api|uploads|hubs)(\/|$)|^\/\.well-known\/security\.txt$/;

// Header trình duyệt không được tự đặt để giả IP; ghi lại từ IP Cloudflare nhìn thấy
const STRIPPED = ['x-forwarded-for', 'x-forwarded-host', 'x-forwarded-proto', 'x-real-ip', 'forwarded'];

export async function onRequest({ request, env, next }) {
  const url = new URL(request.url);
  if (!PROXIED.test(url.pathname)) {
    return next();
  }

  if (!env.API_ORIGIN) {
    return new Response('Chưa cấu hình API_ORIGIN cho dự án Pages.', { status: 500 });
  }

  const target = new URL(url.pathname + url.search, env.API_ORIGIN);
  const headers = new Headers(request.headers);
  for (const name of STRIPPED) {
    headers.delete(name);
  }

  headers.set('X-Forwarded-For', request.headers.get('CF-Connecting-IP') ?? '');
  headers.set('X-Forwarded-Proto', 'https');
  headers.set('X-Forwarded-Host', url.host);

  const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
  // WebSocket (SignalR) đi qua nguyên vẹn: fetch giữ header Upgrade và trả về kết nối của máy chủ
  return fetch(target, { method: request.method, headers, body: hasBody ? request.body : undefined, redirect: 'manual' });
}
