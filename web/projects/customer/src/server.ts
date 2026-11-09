import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();

// Không cho nhúng trang vào web khác (chống lừa bấm), không đoán loại tệp, không gửi đường dẫn đầy đủ sang trang ngoài.
// Chưa siết script-src vì Angular SSR có script nội tuyến; Nginx khi triển khai thêm CSP đầy đủ (VIEC_CON_NO)
const SECURITY_HEADERS: Record<string, string> = {
  'Content-Security-Policy': "base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'",
  'X-Frame-Options': 'DENY',
  'X-Content-Type-Options': 'nosniff',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
  'Permissions-Policy': 'camera=(), microphone=(), geolocation=(), payment=(), usb=()',
  'Cross-Origin-Opener-Policy': 'same-origin',
};

app.disable('x-powered-by');
app.use((_req, res, next) => {
  res.set(SECURITY_HEADERS);
  next();
});

/**
 * Example Express Rest API endpoints can be defined here.
 * Uncomment and define endpoints as necessary.
 *
 * Example:
 * ```ts
 * app.get('/api/{*splat}', (req, res) => {
 *   // Handle API request
 * });
 * ```
 */

/**
 * Serve static files from /browser
 */
// Tệp do Angular sinh có mã băm trong tên (main-AB12CD34.js) nên cache vĩnh viễn an toàn.
// Tệp giữ nguyên tên (ảnh trang trí, biểu tượng, manifest, tệp của service worker) phải hỏi lại máy chủ để thấy bản mới
const HASHED = /-[A-Z0-9]{8,}\.(?:js|css|woff2?)$/;
const ALWAYS_FRESH = /(?:^|\/)(?:ngsw\.json|ngsw-worker\.js|safety-worker\.js|worker-basic\.min\.js|manifest\.webmanifest)$/;
app.use(
  express.static(browserDistFolder, {
    index: false,
    redirect: false,
    setHeaders: (res, path) => {
      const file = path.replaceAll('\\', '/');
      if (HASHED.test(file)) {
        res.setHeader('Cache-Control', 'public, max-age=31536000, immutable');
      } else if (ALWAYS_FRESH.test(file)) {
        res.setHeader('Cache-Control', 'no-cache');
      } else {
        res.setHeader('Cache-Control', 'public, max-age=86400, must-revalidate');
      }
    },
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) =>
      response ? writeResponseToNodeResponse(response, res) : next(),
    )
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
