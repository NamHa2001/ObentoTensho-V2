// Kiểm tra lỗ hổng gói npm: chặn mọi lỗ hổng mức High trở lên, trừ đúng các mã lỗi trong audit-exceptions.json còn hạn.
// Quá hạn thì chặn lại để buộc xem đã có bản vá chưa; lỗ hổng mới không có trong danh sách thì chặn như thường.
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';

const BLOCKING = new Set(['high', 'critical']);
const today = new Date().toISOString().slice(0, 10);
const exceptions = JSON.parse(readFileSync(new URL('../audit-exceptions.json', import.meta.url), 'utf8'));

const run = spawnSync('npm audit --json', { encoding: 'utf8', shell: true, maxBuffer: 64 * 1024 * 1024 });
const report = JSON.parse(run.stdout);
if (report.error) {
  console.error('Không chạy được npm audit:', report.error.summary ?? report.error);
  process.exit(1);
}

// Gói chỉ dính lỗ hổng qua gói khác thì "via" là tên gói; lỗ hổng gốc là các object có url GHSA
const advisories = new Map();
for (const vulnerability of Object.values(report.vulnerabilities ?? {})) {
  for (const via of vulnerability.via) {
    if (typeof via === 'object' && BLOCKING.has(via.severity)) {
      const id = via.url.split('/').pop();
      advisories.set(id, { id, name: via.name, severity: via.severity, title: via.title, range: via.range });
    }
  }
}

let failed = false;
for (const e of exceptions) {
  if (e.until < today) {
    console.error(`✗ Miễn trừ ${e.id} (${e.package}) đã hết hạn ${e.until}: kiểm tra bản vá rồi cập nhật gói, hoặc gia hạn kèm lý do.`);
    failed = true;
  }
}

for (const a of advisories.values()) {
  const exception = exceptions.find((e) => e.id === a.id && e.until >= today);
  if (exception) {
    console.log(`• Bỏ qua ${a.id} ${a.name} ${a.range} (${a.severity}) tới ${exception.until}: ${a.title}`);
  } else {
    console.error(`✗ ${a.id} ${a.name} ${a.range} (${a.severity}): ${a.title}`);
    failed = true;
  }
}

for (const e of exceptions.filter((x) => !advisories.has(x.id))) {
  console.log(`! Miễn trừ ${e.id} (${e.package}) không còn cần: xóa khỏi audit-exceptions.json.`);
}

if (failed) {
  process.exit(1);
}
console.log(`Không có lỗ hổng mức High trở lên ngoài danh sách miễn trừ (${advisories.size} lỗ hổng gốc đã xét).`);
