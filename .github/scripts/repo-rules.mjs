// Quy tắc chung của repo, chạy trong CI cho mọi lần push và PR (ai đẩy cũng bị kiểm như nhau):
//   node .github/scripts/repo-rules.mjs commits <base> <head>   lời commit đúng dạng, không có tệp bị chặn, không có từ cấm
//   node .github/scripts/repo-rules.mjs guard <base> <head>     phát hiện thay đổi làm yếu bước kiểm tra
//   node .github/scripts/repo-rules.mjs pr                      mô tả PR điền đủ mục (đọc sự kiện PR của GitHub Actions)
//   node .github/scripts/repo-rules.mjs template                in khuôn mô tả PR
// Luật đọc từ .github/repo-rules.json. Từ cấm và đường dẫn cấm riêng tư đọc từ biến môi trường REPO_RULES_PRIVATE
// (JSON { "words": [...], "paths": [...] }, đặt ở biến của repo trên GitHub), không ghi trong repo.
import { execFileSync } from 'node:child_process';
import { appendFileSync, readFileSync } from 'node:fs';

const rules = JSON.parse(readFileSync(new URL('../repo-rules.json', import.meta.url), 'utf8'));
const RULE_FILES = /^\.github\/(repo-rules\.json|scripts\/repo-rules\.mjs)$/;
const DECLARE = 'Đổi-kiểm-tra:';
const LAYERS = Object.keys(rules.layers);

const TEMPLATE = `Loại: <tính năng | sửa lỗi | cải tiến | khác>

## Thay đổi
<làm gì, vì sao>

## Bằng chứng
<lệnh đã chạy và kết quả: test, verify, ảnh chụp>

## Chỗ cần soi
<2–3 chỗ rủi ro nhất cho người duyệt, kèm lý do>

## Phân tích lỗi
(chỉ bắt buộc khi Loại là sửa lỗi; không phải thì xoá cả mục)
- Triệu chứng: <người dùng thấy gì>
- Nguyên nhân gốc: <sai ở đâu, vì sao viết sai>
- Thiếu thông tin: <người viết không biết điều gì>
- Vì sao lưới không bắt: <chưa có test? test có nhưng kiểm thiếu?>
- Chặn ở lớp: <một hoặc vài lớp, cách nhau dấu phẩy: ${LAYERS.join(', ')}>
- Bằng chứng chặn: <tên test, luật hoặc mục kiểm chứng mới; "đã có lớp bắt" thì ghi bước CI đã báo đỏ>

## Đổi kiểm tra
(chỉ khi sửa tệp cấu hình kiểm tra, thêm Skip/eslint-disable… hoặc bớt test; không thì xoá cả mục)
<sửa gì, vì sao cần, vì sao không làm yếu lưới>
`;

const failures = [];
const notes = [];
const fail = (title, lines = []) => failures.push({ title, lines });

const git = (...args) => execFileSync('git', args, { encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
const any = (patterns, text) => patterns.some((p) => new RegExp(p, 'i').test(text));

function privateRules() {
  const raw = process.env.REPO_RULES_PRIVATE;
  if (!raw) {
    fail('Thiếu biến REPO_RULES_PRIVATE (từ cấm và đường dẫn cấm riêng tư)', ['Đặt ở GitHub: Settings → Secrets and variables → Actions → Variables']);
    return { words: [], paths: [] };
  }
  const p = JSON.parse(raw);
  return { words: p.words ?? [], paths: p.paths ?? [] };
}

/** Khoảng commit cần soát; lần đẩy nhánh mới (base toàn số 0) hoặc sau force push thì lấy từ chỗ rẽ khỏi master */
function resolveBase(base, head) {
  const known = base && !/^0+$/.test(base) && (() => {
    try {
      git('cat-file', '-e', `${base}^{commit}`);
      return true;
    } catch {
      return false;
    }
  })();
  if (known) return base;
  const fork = git('merge-base', 'origin/master', head).trim();
  return fork === git('rev-parse', head).trim() ? `${head}^` : fork;
}

/** Dòng thêm mới theo từng tệp trong khoảng base..head */
function addedLines(base, head) {
  const out = [];
  let file = '';
  for (const line of git('diff', '-U0', '--no-color', '--no-renames', base, head).split('\n')) {
    if (line.startsWith('+++ ')) file = line.slice(6);
    else if (line.startsWith('+') && !line.startsWith('+++')) out.push({ file, text: line.slice(1) });
  }
  return out;
}

function checkCommits(base, head) {
  const priv = privateRules();
  const blocked = [...rules.blockedPaths, ...priv.paths];
  for (const sha of git('rev-list', '--reverse', `${base}..${head}`).split('\n').filter(Boolean)) {
    const short = sha.slice(0, 7);
    const message = git('log', '-1', '--format=%B', sha);
    const subject = message.split('\n')[0];
    if (!new RegExp(rules.commitSubjectSkip).test(subject) && !new RegExp(rules.commitSubject).test(subject))
      fail(`${short}: dòng đầu lời commit phải có dạng "<Phân hệ>: <việc>"`, [`Đang là: ${subject}`, `Ví dụ: ${rules.commitSubjectExample}`]);
    const words = message.split('\n').filter((l) => any(priv.words, l));
    if (words.length) fail(`${short}: lời commit có từ cấm`, words);
    const files = git('diff-tree', '--no-commit-id', '--name-only', '-r', '--diff-filter=ACMR', sha)
      .split('\n')
      .filter((f) => f && any(blocked, f) && !any(rules.allowedPaths, f));
    if (files.length) fail(`${short}: có tệp không được đưa vào repo`, files);
  }
  const words = addedLines(base, head).filter((l) => any(priv.words, l.text));
  if (words.length) fail('Mã thêm mới có từ cấm', words.map((l) => `${l.file}: ${l.text.trim().slice(0, 100)}`));
}

function countTests(rev) {
  let total = 0;
  for (const { files, pattern } of rules.testCount) {
    const paths = git('ls-tree', '-r', '--name-only', rev).split('\n').filter((f) => new RegExp(files).test(f));
    for (const f of paths) total += (git('show', `${rev}:${f}`).match(new RegExp(pattern, 'gm')) ?? []).length;
  }
  return total;
}

/** Thay đổi có thể làm yếu lưới kiểm tra: không chặn hẳn, nhưng bắt phải khai báo lý do để người duyệt soi */
function guardFindings(base, head) {
  const found = [];
  const changed = git('diff', '--name-status', '--no-renames', base, head).split('\n').filter(Boolean).map((l) => l.split('\t'));
  const deletedTests = changed.filter(([s, f]) => s === 'D' && any(rules.testFiles, f)).map(([, f]) => f);
  if (deletedTests.length) found.push({ title: 'Xoá tệp test', lines: deletedTests });
  const before = countTests(base);
  const after = countTests(head);
  if (after < before) found.push({ title: `Số test giảm từ ${before} xuống ${after}`, lines: [] });
  // Phân biệt hoa thường: .Skip( của LINQ không phải bỏ qua test. Dấu bỏ qua test chỉ xét trong tệp test;
  // tệp cấu hình kiểm tra đã bị bắt riêng ở dưới, tệp sinh tự động (migration) không tính
  const has = (patterns, text) => patterns.some((p) => new RegExp(p).test(text));
  const markers = addedLines(base, head).filter(
    (l) =>
      !any(rules.checkConfig, l.file) &&
      !any(rules.generatedFiles, l.file) &&
      (has(rules.weakenMarkers, l.text) || (any(rules.testFiles, l.file) && has(rules.skipMarkers, l.text))),
  );
  if (markers.length) found.push({ title: 'Thêm dấu bỏ qua test hoặc tắt luật', lines: markers.map((l) => `${l.file}: ${l.text.trim().slice(0, 100)}`) });
  const config = changed.filter(([, f]) => any(rules.checkConfig, f)).map(([s, f]) => `${s} ${f}`);
  if (config.length) found.push({ title: 'Sửa tệp cấu hình kiểm tra', lines: config });
  return { found, after, before };
}

function declaredReason(base, head, body) {
  const fromCommits = git('log', '--format=%B', `${base}..${head}`).split('\n').filter((l) => l.startsWith(DECLARE));
  const fromBody = section(body ?? '', 'Đổi kiểm tra');
  return [...fromCommits.map((l) => l.slice(DECLARE.length).trim()), fromBody].filter(filled);
}

function checkGuard(base, head, body) {
  const { found, before, after } = guardFindings(base, head);
  notes.push(`Số test: ${before} → ${after}`);
  if (!found.length) return;
  const reasons = declaredReason(base, head, body);
  const lines = found.flatMap((f) => [`• ${f.title}`, ...f.lines.map((l) => `    ${l}`)]);
  if (reasons.length) {
    notes.push('Có thay đổi đụng tới lưới kiểm tra, đã khai báo lý do. Người duyệt cần soi:', ...lines, 'Lý do:', ...reasons.map((r) => `    ${r}`));
  } else {
    fail(`Thay đổi đụng tới lưới kiểm tra mà chưa khai báo lý do (thêm dòng "${DECLARE} <lý do>" vào lời commit, hoặc mục "## Đổi kiểm tra" trong mô tả PR)`, lines);
  }
}

/** Nội dung một mục "## <tên>" trong mô tả PR */
function section(body, name) {
  const m = body.match(new RegExp(`^##\\s*${name}\\s*$([\\s\\S]*?)(?=^##\\s|(?![\\s\\S]))`, 'mi'));
  return m ? m[1].trim() : '';
}
// Chữ mẫu trong khuôn có dạng <vài chữ cách nhau>; kiểu generic như resource<Dto> không có khoảng trắng nên không bị tính
const filled = (text, min = 5) => !!text && !/<[^<>]*\s[^<>]*>/.test(text) && text.replace(/^\(.*\)$/gm, '').trim().length >= min;
const field = (text, name) => text.match(new RegExp(`^-\\s*${name}\\s*:\\s*(.*)$`, 'mi'))?.[1].trim() ?? '';

function checkPr() {
  const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
  const pr = event.pull_request;
  const body = pr.body ?? '';
  const base = pr.base.sha;
  const head = pr.head.sha;
  const priv = privateRules();
  const words = `${pr.title}\n${body}`.split('\n').filter((l) => any(priv.words, l));
  if (words.length) fail('Tiêu đề hoặc mô tả PR có từ cấm', words);

  const kind = body.match(/^Loại\s*:\s*(.+)$/im)?.[1].trim().toLowerCase() ?? '';
  if (!['tính năng', 'sửa lỗi', 'cải tiến', 'khác'].includes(kind)) fail('Thiếu dòng "Loại: tính năng | sửa lỗi | cải tiến | khác"', [`Đang là: ${kind || '(trống)'}`]);
  for (const name of ['Thay đổi', 'Bằng chứng', 'Chỗ cần soi']) if (!filled(section(body, name))) fail(`Mục "## ${name}" còn trống hoặc còn chữ mẫu <...>`);

  if (kind === 'sửa lỗi') {
    const analysis = section(body, 'Phân tích lỗi');
    for (const name of ['Triệu chứng', 'Nguyên nhân gốc', 'Thiếu thông tin', 'Vì sao lưới không bắt', 'Chặn ở lớp', 'Bằng chứng chặn'])
      if (!filled(field(analysis, name), 2)) fail(`Sửa lỗi: "${name}" trong mục "## Phân tích lỗi" còn trống`);
    const layers = field(analysis, 'Chặn ở lớp').toLowerCase().split(',').map((s) => s.trim()).filter(Boolean);
    const unknown = layers.filter((l) => !LAYERS.includes(l));
    if (unknown.length) fail(`Lớp không hợp lệ: ${unknown.join(', ')}`, [`Chọn trong: ${LAYERS.join(', ')}`]);
    const changed = git('diff', '--name-only', '--diff-filter=ACMR', base, head).split('\n').filter(Boolean);
    for (const layer of layers.filter((l) => rules.layers[l]?.length)) {
      if (!changed.some((f) => any(rules.layers[layer], f))) fail(`Ghi chặn ở lớp "${layer}" nhưng PR không có thay đổi nào thuộc lớp đó`, rules.layers[layer]);
    }
  }
  checkGuard(base, head, body);
}

const [mode, base, head] = process.argv.slice(2);
if (mode === 'template') {
  process.stdout.write(TEMPLATE);
  process.exit(0);
}
if (mode === 'commits') checkCommits(resolveBase(base, head), head);
else if (mode === 'guard') checkGuard(resolveBase(base, head), head);
else if (mode === 'pr') checkPr();
else {
  console.error('Dùng: repo-rules.mjs commits|guard <base> <head> | pr | template');
  process.exit(2);
}

const report = [
  ...notes,
  ...failures.flatMap((f) => [`✗ ${f.title}`, ...f.lines.map((l) => `    ${l}`)]),
  failures.length ? `\n${failures.length} lỗi.` : '✓ Đạt.',
];
console.log(report.join('\n'));
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `### ${mode}\n\`\`\`\n${report.join('\n')}\n\`\`\`\n`);
process.exit(failures.length ? 1 : 0);
