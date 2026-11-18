// Điều khiển Chrome có sẵn trên máy qua DevTools Protocol, không cần cài thêm gói.
// Mọi bước chờ đều chờ theo điều kiện (waitFor), không chờ mò theo số giây.
import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const CHROME_PATHS = [
  process.env.CHROME_PATH,
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
  `${process.env.LOCALAPPDATA}/Google/Chrome/Application/chrome.exe`,
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
  '/usr/bin/google-chrome',
  '/usr/bin/chromium',
  '/usr/bin/chromium-browser',
];

export const SIZES = {
  desktop: { width: 1366, height: 900, mobile: false },
  mobile: { width: 390, height: 844, mobile: true },
};

const pause = (ms) => new Promise((r) => setTimeout(r, ms));

export async function launch() {
  const exe = CHROME_PATHS.find((p) => p && existsSync(p));
  if (!exe) throw new Error('Không tìm thấy Chrome. Đặt biến CHROME_PATH trỏ tới chrome.exe.');
  const profile = mkdtempSync(join(tmpdir(), 'ot-verify-'));
  // Cổng 0: Chrome tự chọn cổng trống và ghi vào DevToolsActivePort, không đụng tiến trình khác
  const proc = spawn(exe, ['--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'], { stdio: 'ignore' });
  const portFile = join(profile, 'DevToolsActivePort');
  let port;
  for (let i = 0; i < 100 && !port; i++) {
    await pause(100);
    if (existsSync(portFile)) port = readFileSync(portFile, 'utf8').split('\n')[0];
  }
  if (!port) throw new Error('Chrome không mở được cổng điều khiển');
  const target = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find((t) => t.type === 'page');
  const page = new Page(new WebSocket(target.webSocketDebuggerUrl));
  await page.ready;
  page.close = () => {
    page.ws.close();
    proc.kill();
    // Chrome nhả khoá hồ sơ chậm một chút sau khi tắt
    setTimeout(() => rmSync(profile, { recursive: true, force: true, maxRetries: 5 }), 500);
  };
  return page;
}

export class Page {
  /** Lỗi gặp trong lúc chạy: lỗi JS, console.error, API trả 4xx/5xx, hộp thoại gốc của trình duyệt */
  problems = [];
  /** Đường dẫn API được bỏ qua khi gom lỗi: chưa đăng nhập thì làm mới phiên nhận 401 là bình thường */
  allowed = ['/auth/refresh-token'];
  base = '';
  /** requestId của lần gọi API gần nhất theo đường dẫn (lần sau ghi đè lần trước), để đọc lại dữ liệu trang đã nhận */
  #responses = new Map();
  /** Yêu cầu XHR/fetch đang chờ trả lời, để biết trang đã tải xong dữ liệu */
  #inflight = new Set();
  #loaded = false;
  #id = 0;
  #wait = new Map();

  constructor(ws) {
    this.ws = ws;
    this.ready = new Promise((r) => (ws.onopen = r)).then(async () => {
      await this.send('Page.enable');
      await this.send('Runtime.enable');
      await this.send('Network.enable');
    });
    ws.onmessage = (m) => this.#onMessage(JSON.parse(m.data));
  }

  #onMessage(d) {
    if (d.id && this.#wait.has(d.id)) {
      this.#wait.get(d.id)(d);
      this.#wait.delete(d.id);
      return;
    }
    const p = d.params;
    if (d.method === 'Runtime.exceptionThrown') this.problems.push('Lỗi JS: ' + (p.exceptionDetails.exception?.description ?? p.exceptionDetails.text).split('\n')[0]);
    if (d.method === 'Runtime.consoleAPICalled' && p.type === 'error') this.problems.push('console.error: ' + p.args.map((a) => a.value ?? a.description ?? '').join(' ').slice(0, 200));
    if (d.method === 'Page.javascriptDialogOpening') {
      this.problems.push(`Hộp thoại gốc của trình duyệt (${p.type}): ${p.message}`);
      this.send('Page.handleJavaScriptDialog', { accept: false });
    }
    if (d.method === 'Network.requestWillBeSent' && (p.type === 'XHR' || p.type === 'Fetch')) this.#inflight.add(p.requestId);
    if (d.method === 'Network.loadingFinished' || d.method === 'Network.loadingFailed') this.#inflight.delete(p.requestId);
    if (d.method === 'Network.responseReceived' && p.response.url.includes('/api/')) this.#responses.set(new URL(p.response.url).pathname, p.requestId);
    if (d.method === 'Network.responseReceived' && p.response.status >= 400 && p.response.url.includes('/api/')) {
      const path = new URL(p.response.url).pathname;
      if (!this.allowed.some((a) => path.includes(a))) this.problems.push(`API ${p.response.status}: ${path}`);
    }
  }

  send(method, params = {}) {
    const id = ++this.#id;
    this.ws.send(JSON.stringify({ id, method, params }));
    return new Promise((resolve, reject) => this.#wait.set(id, (d) => (d.error ? reject(new Error(`${method}: ${d.error.message}`)) : resolve(d.result))));
  }

  /** Chạy biểu thức JS trong trang, trả giá trị (chờ cả Promise) */
  async eval(expression) {
    const r = await this.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (r.exceptionDetails) throw new Error('Lỗi khi chạy trong trang: ' + (r.exceptionDetails.exception?.description ?? r.exceptionDetails.text).split('\n')[0]);
    return r.result.value;
  }

  /** Chờ tới khi biểu thức JS trả giá trị truthy; quá hạn thì báo lỗi kèm mô tả điều đang chờ */
  async waitFor(expression, what, timeout = 15000) {
    const end = Date.now() + timeout;
    for (;;) {
      const v = await this.eval(expression).catch(() => undefined);
      if (v) return v;
      if (Date.now() > end) throw new Error(`Quá ${timeout / 1000}s vẫn chưa thấy: ${what}`);
      await pause(150);
    }
  }

  async size(name) {
    const s = SIZES[name];
    await this.send('Emulation.setDeviceMetricsOverride', { width: s.width, height: s.height, deviceScaleFactor: 1, mobile: s.mobile });
  }

  /**
   * Mở đường dẫn, chờ trang vẽ xong và hết yêu cầu dữ liệu đang chạy.
   * Lần đầu tải cả trang; các lần sau chuyển trang bên trong app như người dùng bấm link
   * (nhanh hơn, không gọi lại API khởi động nên không chạm giới hạn tần suất). reload: true để tải lại cả trang.
   */
  async go(path, { reload = false } = {}) {
    if (!this.#loaded || reload) {
      await this.send('Page.navigate', { url: this.base + path });
      await this.waitFor(`document.readyState !== 'loading' && !!document.querySelector('router-outlet')`, `trang ${path} tải xong`);
      this.#loaded = true;
    } else {
      await this.eval(`history.pushState(null, '', ${JSON.stringify(path)}); dispatchEvent(new PopStateEvent('popstate'))`);
    }
    await this.idle();
  }

  /** Chờ tới khi không còn yêu cầu XHR/fetch nào đang chạy trong một khoảng ngắn liên tục */
  async idle(timeout = 15000) {
    const end = Date.now() + timeout;
    let quiet = 0;
    while (quiet < 3) {
      if (Date.now() > end) throw new Error(`Quá ${timeout / 1000}s trang vẫn còn ${this.#inflight.size} yêu cầu dữ liệu chưa xong`);
      await pause(150);
      quiet = this.#inflight.size === 0 ? quiet + 1 : 0;
    }
  }

  /** Dữ liệu JSON trang đã nhận từ API có đường dẫn kết thúc bằng suffix (vd '/dashboard') */
  async apiData(suffix) {
    const path = [...this.#responses.keys()].find((k) => k.endsWith(suffix));
    if (!path) return undefined;
    const r = await this.send('Network.getResponseBody', { requestId: this.#responses.get(path) });
    return JSON.parse(r.base64Encoded ? Buffer.from(r.body, 'base64').toString('utf8') : r.body);
  }

  path() {
    return this.eval('location.pathname + location.search');
  }

  async type(selector, value) {
    await this.waitFor(`!!document.querySelector(${JSON.stringify(selector)})`, `ô nhập ${selector}`);
    await this.eval(`(() => { const e = document.querySelector(${JSON.stringify(selector)}); e.focus(); e.value = ${JSON.stringify(value)}; e.dispatchEvent(new Event('input', { bubbles: true })); })()`);
  }

  /** Bấm phần tử theo CSS selector, hoặc nút/link có chữ đúng bằng text (ghi dạng 'text=Gửi bếp') */
  async click(target, scope = 'document') {
    const find = target.startsWith('text=')
      ? `[...${scope}.querySelectorAll('button, a, [role=button], [role=tab]')].find((e) => e.innerText.trim() === ${JSON.stringify(target.slice(5))} && !e.disabled)`
      : `${scope}.querySelector(${JSON.stringify(target)})`;
    await this.waitFor(`!!(${find})`, `nút/phần tử ${target}`);
    await this.eval(`(() => { const e = ${find}; e.scrollIntoView({ block: 'center' }); e.focus(); e.click(); })()`);
  }

  async key(key) {
    const codes = { Escape: 27, Tab: 9, Enter: 13 };
    await this.send('Input.dispatchKeyEvent', { type: 'keyDown', key, code: key, windowsVirtualKeyCode: codes[key] });
    await this.send('Input.dispatchKeyEvent', { type: 'keyUp', key, code: key, windowsVirtualKeyCode: codes[key] });
  }

  /** Mô tả ngắn phần tử đang được focus, để kiểm hỗ trợ bàn phím */
  focused() {
    return this.eval(`(() => { const a = document.activeElement; return a && a !== document.body ? (a.getAttribute('aria-label') || a.innerText || a.name || a.tagName).replace(/\\s+/g, ' ').trim().slice(0, 40) : '(không có)'; })()`);
  }

  /** Số px trang tràn ngang (0 là không tràn) */
  overflow() {
    return this.eval('document.documentElement.scrollWidth - document.documentElement.clientWidth');
  }

  async shot(file) {
    const r = await this.send('Page.captureScreenshot', { format: 'png' });
    writeFileSync(file, Buffer.from(r.data, 'base64'));
  }
}
