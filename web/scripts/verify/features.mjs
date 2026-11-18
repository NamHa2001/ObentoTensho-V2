// Bản đồ tính năng: mỗi mục ghi trang nào, cần đăng nhập gì, thế nào là đúng.
// Sửa một tính năng thì chạy mục tương ứng; thêm tính năng mới thì thêm mục ở đây.
// Các mục chỉ đọc, không tạo/sửa dữ liệu, nên chạy được cả trên host thử.

const CUSTOMER_PAGES = ['/', '/menu', '/gio-hang', '/khuyen-mai', '/tin-tuc', '/khong-gian', '/lien-he', '/gioi-thieu', '/tra-cuu', '/dang-nhap'];
const ADMIN_PAGES = ['/dashboard', '/orders', '/kitchen', '/reservations', '/reservations/new', '/calendar', '/floor', '/dine-in', '/invoices', '/customers', '/reports', '/settings', '/hours', '/translations', '/account', '/option-groups', '/r/items', '/r/vouchers', '/r/banners', '/r/staff'];

// Thẻ món có nút mở cửa sổ "Thêm vào giỏ"
const PLUS = `[...document.querySelectorAll('ot-menu-item-card button')].filter((b) => /thêm|\\+/i.test(b.getAttribute('aria-label') || b.textContent))`;
const SHEET_OPEN = `!!document.querySelector('ot-add-to-cart .ot-sheet-back')`;

/** Đi qua từng trang ở cả máy tính và điện thoại: không lỗi, không tràn ngang, có tiêu đề */
async function sweep(page, ctx, paths) {
  for (const size of ['desktop', 'mobile']) {
    await page.size(size);
    for (const path of paths) {
      page.problems = [];
      await page.go(path);
      await page.waitFor(`!!document.querySelector('h1') || !!document.querySelector('.ot-error')`, `tiêu đề trang ${path}`).catch(() => {});
      const name = `${size}${path.replace(/\//g, '-') || '-trang-chu'}`;
      await ctx.shot(name);
      const over = await page.overflow();
      const error = await page.eval(`document.querySelector('.ot-error')?.innerText.trim() ?? ''`);
      const fine = over <= 0 && !error && page.problems.length === 0;
      ctx.check(`${size} ${path}`, fine, [over > 0 && `tràn ngang ${over}px`, error && `báo lỗi "${error}"`, ...page.problems].filter(Boolean).join('; '));
    }
  }
}

export const FEATURES = {
  'trang-khach': {
    app: 'customer',
    what: 'Các trang công khai của trang khách mở được ở máy tính và điện thoại: không lỗi JS, không lỗi API, không tràn ngang',
    run: (page, ctx) => sweep(page, ctx, CUSTOMER_PAGES),
  },

  'nhay-danh-muc': {
    app: 'customer',
    what: 'Thực đơn: bấm một danh mục thì cuộn tới đúng nhóm món đó, nút danh mục được tô sáng',
    async run(page, ctx) {
      await page.size('mobile');
      await page.go('/menu');
      await page.waitFor(`document.querySelectorAll('.ot-menu-section').length > 1`, 'thực đơn có từ 2 nhóm món');
      // Lấy nhóm cuối cùng: xa đầu trang nhất nên dễ lộ lỗi cuộn sai nhất
      const slug = await page.eval(`[...document.querySelectorAll('.ot-menu-section')].at(-1).id.replace('dm-', '')`);
      await page.click(`#dm-chip-${slug}`);
      const top = await page.waitFor(`(() => { const t = Math.round(document.getElementById('dm-${slug}').getBoundingClientRect().top); return t >= 0 && t < 260 ? String(t) : ''; })()`, `nhóm ${slug} cuộn lên gần đầu màn hình`).catch(() => null);
      await ctx.shot('sau-khi-bam');
      ctx.check(`cuộn tới nhóm "${slug}"`, top !== null, top === null ? 'nhóm món không nằm ở đầu màn hình sau khi bấm' : `cách đỉnh ${top}px`);
      ctx.check('nút danh mục được tô sáng', await page.eval(`document.getElementById('dm-chip-${slug}').classList.contains('ot-chip-active')`));
      ctx.check('không lỗi trong lúc chạy', page.problems.length === 0, page.problems.join('; '));
    },
  },

  'them-vao-gio': {
    app: 'customer',
    what: 'Cửa sổ "Thêm vào giỏ": mở ra thì focus vào trong, Tab không ra ngoài, Esc đóng và trả focus về nút vừa bấm, bấm nền thì đóng',
    async run(page, ctx) {
      await page.size('mobile');
      await page.go('/menu');
      await page.waitFor(`${PLUS}.length > 0`, 'nút thêm món trên thẻ món');
      const opener = await page.eval(`(() => { const b = ${PLUS}[0]; b.focus(); b.click(); return b.getAttribute('aria-label') || b.textContent.trim(); })()`);
      await page.waitFor(SHEET_OPEN, 'cửa sổ thêm vào giỏ mở ra');
      await page.waitFor(`!!document.activeElement?.closest('ot-add-to-cart')`, 'focus vào trong cửa sổ', 3000).catch(() => {});
      await ctx.shot('cua-so-mo');
      ctx.check('mở ra thì focus nằm trong cửa sổ', await page.eval(`!!document.activeElement?.closest('ot-add-to-cart')`), `focus: ${await page.focused()}`);
      for (let i = 0; i < 8; i++) await page.key('Tab');
      ctx.check('bấm Tab nhiều lần focus vẫn trong cửa sổ', await page.eval(`!!document.activeElement?.closest('ot-add-to-cart')`), `focus: ${await page.focused()}`);
      await page.key('Escape');
      const closed = await page.waitFor(`!(${SHEET_OPEN})`, 'cửa sổ đóng sau Esc', 3000).then(() => true, () => false);
      ctx.check('Esc đóng cửa sổ', closed);
      ctx.check('đóng xong focus về nút vừa bấm', (await page.focused()) === opener.replace(/\s+/g, ' ').trim().slice(0, 40), `focus: ${await page.focused()}`);
      await page.eval(`${PLUS}[0].click()`);
      await page.waitFor(SHEET_OPEN, 'cửa sổ mở lại');
      await page.eval(`document.querySelector('ot-add-to-cart .ot-sheet').click()`);
      ctx.check('bấm bên trong cửa sổ không đóng', await page.eval(SHEET_OPEN));
      await page.eval(`document.querySelector('ot-add-to-cart .ot-sheet-back').click()`);
      ctx.check('bấm ra nền thì đóng', await page.waitFor(`!(${SHEET_OPEN})`, 'cửa sổ đóng', 3000).then(() => true, () => false));
      ctx.check('không lỗi trong lúc chạy', page.problems.length === 0, page.problems.join('; '));
    },
  },

  'ban-do-giao-hang': {
    app: 'customer',
    login: 'customer',
    what: 'Thanh toán: chọn "Giao tận nơi" thì hiện ô địa chỉ và bản đồ có ô bản đồ tải được (cần tài khoản khách đã xác minh SĐT, quán bật giao hàng)',
    async run(page, ctx) {
      await page.size('mobile');
      await page.go('/menu');
      await page.waitFor(`${PLUS}.length > 0`, 'nút thêm món trên thẻ món');
      // Món có nhóm tùy chọn bắt buộc thì nút thêm bị khoá; thử lần lượt tới món thêm được
      const added = await page.eval(`(async () => {
        const plus = ${PLUS};
        for (const b of plus.slice(0, 8)) {
          b.click();
          for (let i = 0; i < 30 && !document.querySelector('ot-add-to-cart .ot-btn-primary'); i++) await new Promise((r) => requestAnimationFrame(r));
          const add = document.querySelector('ot-add-to-cart .ot-btn-primary');
          if (add && !add.disabled) { add.click(); return true; }
          document.querySelector('ot-add-to-cart .ot-sheet-back')?.click();
        }
        return false;
      })()`);
      if (!added) return ctx.skip('8 món đầu đều cần chọn tùy chọn bắt buộc, chưa thêm được món vào giỏ');
      await page.go('/thanh-toan');
      if ((await page.path()).startsWith('/xac-minh')) return ctx.skip('tài khoản khách chưa xác minh số điện thoại');
      const hasDelivery = await page.waitFor(`[...document.querySelectorAll('.ot-chip')].some((c) => c.innerText.trim() === 'Giao tận nơi')`, 'lựa chọn Giao tận nơi', 8000).then(() => true, () => false);
      if (!hasDelivery) return ctx.skip('quán chưa bật giao hàng: Quản trị → Cài đặt → tick "Nhận đơn giao tận nơi" và nhập Vị trí quán');
      await page.click('text=Giao tận nơi');
      await page.waitFor(`!!document.querySelector('ot-address-picker input')`, 'ô nhập địa chỉ');
      const tiles = await page.waitFor(`(() => { const n = [...document.querySelectorAll('ot-address-picker .leaflet-tile-loaded')].length; return n > 0 ? n : 0; })()`, 'ô bản đồ tải xong', 15000).catch(() => 0);
      const height = await page.eval(`Math.round(document.querySelector('ot-address-picker .leaflet-container')?.getBoundingClientRect().height ?? 0)`);
      await page.eval(`document.querySelector('ot-address-picker').scrollIntoView({ block: 'center' })`);
      await ctx.shot('ban-do');
      ctx.check('bản đồ hiện, có chiều cao', height > 100, `cao ${height}px`);
      ctx.check('ô bản đồ tải được', tiles > 0, `${tiles} ô`);
      ctx.check('không lỗi trong lúc chạy', page.problems.length === 0, page.problems.join('; '));
    },
  },

  'trang-quan-tri': {
    app: 'admin',
    login: 'admin',
    what: 'Các trang quản trị mở được ở máy tính và điện thoại: không lỗi JS, không lỗi API, không tràn ngang',
    run: (page, ctx) => sweep(page, ctx, ADMIN_PAGES),
  },

  'bang-tin': {
    app: 'admin',
    login: 'admin',
    what: 'Bảng tin: biểu đồ hiện đúng số liệu API trả về (API có dữ liệu thì biểu đồ không được trống)',
    async run(page, ctx) {
      await page.size('desktop');
      await page.go('/dashboard');
      await page.waitFor(`document.querySelectorAll('ot-chart').length > 0`, 'biểu đồ trên Bảng tin');
      const data = await page.apiData('/dashboard');
      const charts = { 'Món bán chạy': data?.topItems, 'Doanh thu theo danh mục': data?.revenueByCategory, 'Doanh thu theo ngày': data?.revenueByDay };
      await ctx.shot('bang-tin');
      for (const [title, rows] of Object.entries(charts)) {
        if (rows === undefined) continue;
        const empty = await page.eval(`(() => { const c = [...document.querySelectorAll('ot-chart')].find((x) => x.querySelector('.a-card-h span')?.innerText.trim() === ${JSON.stringify(title)}); return c ? !!c.querySelector('.a-chart-empty') : null; })()`);
        if (empty === null) ctx.check(title, false, 'không thấy biểu đồ này');
        else ctx.check(title, empty === (rows.length === 0), `API ${rows.length} dòng, biểu đồ ${empty ? 'trống' : 'có hình'}`);
      }
      ctx.check('không lỗi trong lúc chạy', page.problems.length === 0, page.problems.join('; '));
    },
  },

  'in-phieu': {
    app: 'admin',
    login: 'admin',
    what: 'In phiếu: bấm In ở bếp và In tạm tính ở bàn đang có khách thì gọi lệnh in đúng một lần, phiếu có đủ nội dung',
    async run(page, ctx) {
      await page.size('desktop');
      // Thay lệnh in thật bằng việc ghi lại nội dung phiếu lúc gọi in
      const capture = `window.__printed = []; window.print = () => window.__printed.push(document.querySelector('.ot-ticket')?.innerText ?? '')`;

      await page.go('/kitchen');
      await page.eval(capture);
      const hasOrder = await page.waitFor(`[...document.querySelectorAll('button')].some((b) => b.innerText.trim() === 'In')`, 'đơn trên màn hình bếp', 6000).then(() => true, () => false);
      if (!hasOrder) ctx.skip('bếp: không có đơn nào đang chờ để in');
      else {
        const code = await page.eval(`[...document.querySelectorAll('button')].find((b) => b.innerText.trim() === 'In').closest('article')?.querySelector('b')?.innerText.trim() ?? ''`);
        await page.click('text=In');
        const printed = await page.waitFor(`window.__printed.length > 0 && window.__printed`, 'lệnh in ở bếp', 5000).catch(() => []);
        await page.send('Emulation.setEmulatedMedia', { media: 'print' });
        await ctx.shot('bep-ban-in');
        await page.send('Emulation.setEmulatedMedia', { media: '' });
        ctx.check('bếp: gọi in đúng một lần', printed.length === 1, `${printed.length} lần`);
        ctx.check('bếp: phiếu có mã đơn và món', !!printed[0] && printed[0].includes(code) && printed[0].includes('×'), (printed[0] ?? '').replace(/\s+/g, ' ').slice(0, 120));
      }

      await page.go('/dine-in');
      const bill = await page.waitFor(`document.querySelector('.a-dine-table[data-status="occupied"]') ? 'co' : (document.querySelectorAll('.a-dine-table').length ? 'khong' : '')`, 'sơ đồ bàn', 8000).catch(() => '');
      if (bill !== 'co') return ctx.skip('tại bàn: không có bàn nào đang có khách để in tạm tính');
      const table = await page.eval(`document.querySelector('.a-dine-table[data-status="occupied"] b').innerText.trim()`);
      await page.click('.a-dine-table[data-status="occupied"]');
      await page.waitFor(`location.pathname.startsWith('/dine-in/')`, 'mở phiếu bàn');
      await page.eval(capture);
      const canPrint = await page.waitFor(`[...document.querySelectorAll('button')].find((b) => b.innerText.trim() === 'In tạm tính') ? 'co' : ''`, 'nút In tạm tính', 8000).catch(() => '');
      if (!canPrint || (await page.eval(`[...document.querySelectorAll('button')].find((b) => b.innerText.trim() === 'In tạm tính').disabled`))) return ctx.skip(`tại bàn: ${table} chưa có món nào để in`);
      await page.click('text=In tạm tính');
      const printed = await page.waitFor(`window.__printed.length > 0 && window.__printed`, 'lệnh in tạm tính', 5000).catch(() => []);
      await page.send('Emulation.setEmulatedMedia', { media: 'print' });
      await ctx.shot('tam-tinh-ban-in');
      await page.send('Emulation.setEmulatedMedia', { media: '' });
      ctx.check('tại bàn: gọi in đúng một lần', printed.length === 1, `${printed.length} lần`);
      ctx.check('tại bàn: phiếu có tên bàn, món và tổng cộng', !!printed[0] && printed[0].includes(table) && printed[0].includes('×') && printed[0].includes('Tổng cộng'), (printed[0] ?? '').replace(/\s+/g, ' ').slice(0, 160));
      ctx.check('không lỗi trong lúc chạy', page.problems.length === 0, page.problems.join('; '));
    },
  },
};
