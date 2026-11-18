// Kiểm chứng tính năng trên trình duyệt thật, cùng một cách mỗi lần:
//   npm run verify                         liệt kê các tính năng có thể kiểm
//   npm run verify -- them-vao-gio bang-tin kiểm các tính năng đã nêu
//   npm run verify -- --all                kiểm tất cả
//   thêm --host để kiểm trên host thử thay vì máy dev (localhost:4200 / 4201)
// Ảnh chụp từng bước nằm ở web/.verify/<tính năng>/. Có bước sai thì thoát mã 1.
// Tài khoản đăng nhập đọc từ tệp JSON do biến môi trường OBENTO_VERIFY_LOGIN chỉ tới, đặt ngoài repo cùng các bí mật khác:
//   { "admin": { "email": "...", "password": "..." }, "customer": { "email": "...", "password": "..." } }
import { existsSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { launch } from './browser.mjs';
import { FEATURES } from './features.mjs';

const WEB = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const LOGIN_FILE = process.env.OBENTO_VERIFY_LOGIN;

const args = process.argv.slice(2);
const host = args.includes('--host');
const BASES = host
  ? { customer: 'https://obento-tensho.pages.dev', admin: 'https://obento-tensho-admin.pages.dev' }
  : { customer: 'http://localhost:4200', admin: 'http://localhost:4201' };
const names = args.includes('--all') ? Object.keys(FEATURES) : args.filter((a) => !a.startsWith('--'));

if (names.length === 0) {
  console.log('Các tính năng kiểm được (npm run verify -- <tên> ...):\n');
  for (const [name, f] of Object.entries(FEATURES)) console.log(`  ${name.padEnd(18)} [${f.app}${f.login ? ', đăng nhập ' + f.login : ''}] ${f.what}`);
  process.exit(0);
}
const unknown = names.filter((n) => !FEATURES[n]);
if (unknown.length) {
  console.error(`Không có tính năng: ${unknown.join(', ')}. Chạy "npm run verify" để xem danh sách.`);
  process.exit(2);
}

for (const app of new Set(names.map((n) => FEATURES[n].app))) {
  const ok = await fetch(BASES[app]).then((r) => r.ok, () => false);
  if (!ok) {
    console.error(`Không mở được ${BASES[app]}.` + (host ? '' : ` Bật máy dev: API (dotnet run --project src/ObentoTensho.Api) và "npx ng serve ${app}" trong web/.`));
    process.exit(2);
  }
}

function account(kind) {
  if (!LOGIN_FILE || !existsSync(LOGIN_FILE)) throw new Error(`Tính năng này cần đăng nhập: đặt OBENTO_VERIFY_LOGIN trỏ tới tệp tài khoản (xem đầu tệp scripts/verify/verify.mjs)${LOGIN_FILE ? ', hiện không thấy ' + LOGIN_FILE : ''}`);
  const a = JSON.parse(readFileSync(LOGIN_FILE, 'utf8'))[kind];
  if (!a?.email || !a?.password) throw new Error(`Tệp ${LOGIN_FILE} chưa có mục "${kind}" với email và password`);
  return a;
}

async function signIn(page, kind) {
  const { email, password } = account(kind);
  await page.go(kind === 'admin' ? '/login' : '/dang-nhap');
  await page.type('input[name=email]', email);
  await page.type('input[name=password]', password);
  await page.eval(`document.querySelector('input[name=password]').form.requestSubmit()`);
  const where = await page.waitFor(`(() => { if (document.querySelector('input[name=code]')) return 'code'; if (document.querySelector('.ot-error')) return 'error:' + document.querySelector('.ot-error').innerText.trim(); return /\\/(login|dang-nhap)$/.test(location.pathname) ? '' : 'ok'; })()`, `đăng nhập ${kind} xong`);
  if (where === 'code') throw new Error(`Tài khoản ${kind} cần mã 2FA; máy dev đặt Security:SkipStaffTwoFactor=true, host thử thì dùng tài khoản không bật 2FA`);
  if (where.startsWith('error:')) throw new Error(`Đăng nhập ${kind} không được: ${where.slice(6)}`);
  page.problems = [];
}

let failed = 0;
for (const name of names) {
  const feature = FEATURES[name];
  const out = join(WEB, '.verify', name);
  rmSync(out, { recursive: true, force: true });
  mkdirSync(out, { recursive: true });
  console.log(`\n▶ ${name} — ${feature.what}\n  ${BASES[feature.app]}`);

  const page = await launch();
  page.base = BASES[feature.app];
  let step = 0;
  const ctx = {
    check(label, ok, detail = '') {
      if (!ok) failed++;
      console.log(`  ${ok ? '✓' : '✗'} ${label}${detail ? ` — ${detail}` : ''}`);
    },
    skip(reason) {
      console.log(`  – bỏ qua: ${reason}`);
    },
    shot: (file) => page.shot(join(out, `${String(++step).padStart(2, '0')}-${file}.png`)),
  };
  try {
    if (feature.login) await signIn(page, feature.login);
    await feature.run(page, ctx);
  } catch (e) {
    failed++;
    console.log(`  ✗ dừng giữa chừng: ${e.message}`);
    await ctx.shot('loi').catch(() => {});
  } finally {
    page.close();
  }
  console.log(`  ảnh: ${out}`);
}

console.log(failed ? `\n${failed} bước sai.` : '\nTất cả đều đúng.');
process.exit(failed ? 1 : 0);
