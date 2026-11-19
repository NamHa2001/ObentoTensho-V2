// Quy tắc chung của repo, chạy trong CI cho mọi lần push và PR (ai đẩy cũng bị kiểm như nhau):
//   node .github/scripts/repo-rules.mjs commits <base> <head>   lời commit đúng dạng, không có tệp bị chặn, không có từ cấm
//   node .github/scripts/repo-rules.mjs guard <base> <head>     phát hiện thay đổi làm yếu bước kiểm tra
//   node .github/scripts/repo-rules.mjs pr                      mô tả PR điền đủ mục (đọc sự kiện PR của GitHub Actions)
//   node .github/scripts/repo-rules.mjs template                in khuôn mô tả PR
//   node .github/scripts/repo-rules.mjs redfirst-plan           PR sửa lỗi: tìm test mà PR thêm hoặc sửa
//   node .github/scripts/repo-rules.mjs redfirst-run <dotnet|angular>   chạy các test đó trên code trước khi sửa
//   node .github/scripts/repo-rules.mjs redfirst-verdict        test phải đỏ trên code trước khi sửa
// Luật đọc từ .github/repo-rules.json. Từ cấm và đường dẫn cấm riêng tư đọc từ biến môi trường REPO_RULES_PRIVATE
// (JSON { "words": [...], "paths": [...] }, lưu ở secret của repo trên GitHub), không ghi trong repo.
import { execFileSync, spawnSync } from 'node:child_process';
import { appendFileSync, existsSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const rules = JSON.parse(readFileSync(new URL('../repo-rules.json', import.meta.url), 'utf8'));
const DECLARE = 'Đổi-kiểm-tra:';
const NO_RED = 'Không chạy đỏ trước';
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

## ${NO_RED}
(chỉ khi sửa lỗi mà test mới không thể chạy trên code trước khi sửa, ví dụ bản sửa buộc đổi chữ ký hàm; không thì xoá cả mục)
<vì sao không viết được test đỏ trên code cũ, và lấy gì chứng minh test bắt được lỗi>

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
    fail('Thiếu biến REPO_RULES_PRIVATE (từ cấm và đường dẫn cấm riêng tư)', ['Đặt ở GitHub: Settings → Secrets and variables → Actions → Secrets (lưu dạng secret để không in ra log)']);
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
// Chỉ coi là chưa điền khi còn nguyên chữ mẫu của khuôn; nội dung thật có thể chứa <...> (kiểu generic, dạng "<Phân hệ>: <việc>")
const PLACEHOLDERS = TEMPLATE.match(/<[^<>\n]+>/g);
const filled = (text, min = 5) => !!text && !PLACEHOLDERS.some((p) => text.includes(p)) && text.replace(/^\(.*\)$/gm, '').trim().length >= min;
const field = (text, name) => text.match(new RegExp(`^-\\s*${name}\\s*:\\s*(.*)$`, 'mi'))?.[1].trim() ?? '';

const prEvent = () => JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8')).pull_request;
const prKind = (body) => body.match(/^Loại\s*:\s*(.+)$/im)?.[1].trim().toLowerCase() ?? '';

function checkPr() {
  const pr = prEvent();
  const body = pr.body ?? '';
  const base = pr.base.sha;
  const head = pr.head.sha;
  const priv = privateRules();
  const words = `${pr.title}\n${body}`.split('\n').filter((l) => any(priv.words, l));
  if (words.length) fail('Tiêu đề hoặc mô tả PR có từ cấm', words);

  const kind = prKind(body);
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

// Đỏ trước – xanh sau: test mà PR sửa lỗi thêm hoặc sửa phải đỏ trên code trước khi sửa, tức là test thật sự bắt được lỗi.
// Xanh sau khi sửa do workflow CI thường kiểm. Đỏ chỉ tính khi test chạy và báo sai: không biên dịch được, hay bộ lọc
// không khớp test nào, đều không chứng minh được gì.
const setOutputs = (values) => {
  const lines = Object.entries(values).map(([k, v]) => `${k}=${v}`);
  if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, `${lines.join('\n')}\n`);
  else notes.push(...lines);
};
const STACKS = { dotnet: '.NET', angular: 'Angular' };

function redFirstPlan() {
  const pr = prEvent();
  const head = pr.head.sha;
  const out = { run: 'false', note: '', base: '', head, dotnet_files: '', dotnet_filter: '', angular_files: '' };
  if (prKind(pr.body ?? '') !== 'sửa lỗi') {
    out.note = 'Không phải PR sửa lỗi, bỏ qua.';
  } else {
    out.base = git('merge-base', pr.base.sha, head).trim();
    // Tên tệp đi vào dòng lệnh của bước sau, chỉ nhận ký tự an toàn
    const changed = git('diff', '--name-only', '--diff-filter=ACMR', out.base, head).split('\n').filter((f) => /^[\w./-]+$/.test(f));
    const dotnet = changed.filter((f) => new RegExp(rules.redFirst.dotnet.files).test(f));
    const filter = dotnet.flatMap((f) => {
      const src = git('show', `${head}:${f}`);
      if (!/\[(Fact|Theory)\b/.test(src)) return [];
      const ns = src.match(/^namespace\s+([\w.]+)/m)?.[1];
      return [...src.matchAll(/\bclass\s+(\w+)/g)].map((m) => `FullyQualifiedName~${ns ? `${ns}.` : ''}${m[1]}.`);
    });
    const angular = changed.filter((f) => new RegExp(rules.redFirst.angular.files).test(f));
    Object.assign(out, { dotnet_files: dotnet.join(' '), dotnet_filter: filter.join('|'), angular_files: angular.join(' ') });
    out.run = String(!!(filter.length || angular.length));
    out.note = out.run === 'true' ? '' : 'PR sửa lỗi không thêm hay sửa test nào, không có gì để chạy đỏ trước (lớp chặn do job "Mô tả PR" kiểm).';
  }
  setOutputs(out);
  notes.push(out.note || `Test sẽ chạy trên code trước khi sửa (${out.base.slice(0, 7)}):`, ...[out.dotnet_filter, out.angular_files].filter(Boolean));
}

/** Chạy lệnh, in log ra màn hình; npm trên Windows là tệp .cmd nên cần shell (tham số đều cố định) */
const run = (cmd, args, cwd) => spawnSync(cmd, args, { cwd, stdio: 'inherit', shell: cmd === 'npm' && process.platform === 'win32' }).status === 0;

const summarize = (total, failed, broken = []) => {
  if (failed > 0) return { state: 'đỏ', total, failed, detail: broken.length ? `không biên dịch: ${broken.join(', ')}` : '' };
  if (broken.length) return { state: 'không biên dịch', total, failed, detail: broken.join(', ') };
  return { state: total ? 'xanh' : 'không chạy test nào', total, failed, detail: '' };
};

function runDotnet(dir) {
  const sln = rules.redFirst.dotnet.solution;
  if (!run('dotnet', ['restore', sln, '--locked-mode'], dir)) return { state: 'lỗi hạ tầng', detail: 'dotnet restore thất bại' };
  if (!run('dotnet', ['build', sln, '--configuration', 'Release', '--no-restore'], dir)) return summarize(0, 0, [sln]);
  const results = join(dir, '.red-first');
  run('dotnet', ['test', sln, '--configuration', 'Release', '--no-build', '--filter', process.env.RF_FILTER, '--logger', 'trx', '--results-directory', results], dir);
  let total = 0;
  let failed = 0;
  for (const f of existsSync(results) ? readdirSync(results).filter((n) => n.endsWith('.trx')) : []) {
    const counters = readFileSync(join(results, f), 'utf8').match(/<Counters [^>]*>/)?.[0] ?? '';
    total += Number(counters.match(/\btotal="(\d+)"/)?.[1] ?? 0);
    failed += Number(counters.match(/\bfailed="(\d+)"/)?.[1] ?? 0);
  }
  return summarize(total, failed);
}

function runAngular(dir, files) {
  const { dir: webDir, files: pattern } = rules.redFirst.angular;
  const web = join(dir, webDir);
  if (!run('npm', ['ci', '--no-audit', '--no-fund'], web)) return { state: 'lỗi hạ tầng', detail: 'npm ci thất bại' };
  const byProject = new Map();
  for (const f of files) {
    const [, include, project] = f.match(new RegExp(pattern));
    byProject.set(project, [...(byProject.get(project) ?? []), include]);
  }
  let total = 0;
  let failed = 0;
  const broken = [];
  for (const [project, includes] of byProject) {
    // Báo cáo JSON chỉ có khi test đã biên dịch và chạy; không có báo cáo nghĩa là lỗi biên dịch
    const report = join(dir, `.red-first-${project}.json`);
    const args = ['test', project, '--watch=false', '--reporters', 'json', '--reporters', 'default', '--output-file', report, ...includes.flatMap((i) => ['--include', i])];
    run(process.execPath, [join(web, 'node_modules/@angular/cli/bin/ng.js'), ...args], web);
    if (!existsSync(report)) {
      broken.push(project);
      continue;
    }
    const r = JSON.parse(readFileSync(report, 'utf8'));
    total += r.numTotalTests;
    failed += r.numFailedTests;
  }
  return summarize(total, failed, broken);
}

function redFirstRun(stack) {
  const { RF_BASE: base, RF_HEAD: head } = process.env;
  const files = (process.env.RF_FILES ?? '').split(' ').filter(Boolean);
  // Thư mục riêng (git worktree) chứa code trước khi sửa, đặt tệp test của PR lên trên; bản checkout chính giữ nguyên
  const dir = join(process.env.RUNNER_TEMP ?? tmpdir(), 'red-first');
  try {
    git('worktree', 'remove', '--force', dir);
  } catch {
    rmSync(dir, { recursive: true, force: true });
  }
  git('worktree', 'add', '--detach', dir, base);
  git('-C', dir, 'checkout', head, '--', ...files);
  const result = { stack, ...(stack === 'dotnet' ? runDotnet(dir) : runAngular(dir, files)) };
  setOutputs({ result: JSON.stringify(result) });
  notes.push(`${STACKS[stack]} trên code trước khi sửa: ${result.state} (${result.failed ?? 0}/${result.total ?? 0} test đỏ)${result.detail ? `, ${result.detail}` : ''}`);
}

function redFirstVerdict() {
  const needs = JSON.parse(process.env.RF_NEEDS);
  const plan = needs.plan;
  if (plan.result !== 'success') return fail('Bước tìm test của PR bị lỗi, xem log job đó');
  if (plan.outputs.run !== 'true') return notes.push(plan.outputs.note);
  const results = Object.keys(STACKS)
    .filter((s) => needs[s].result !== 'skipped')
    .map((s) => (needs[s].result === 'success' ? JSON.parse(needs[s].outputs.result) : { stack: s, state: 'lỗi hạ tầng', detail: `job ${needs[s].result}` }));
  const lines = results.map((r) => `${STACKS[r.stack]}: ${r.state} (${r.failed ?? 0}/${r.total ?? 0} test đỏ)${r.detail ? `, ${r.detail}` : ''}`);
  if (results.some((r) => r.state === 'đỏ')) {
    notes.push('Test của PR đỏ trên code trước khi sửa: test bắt được lỗi. Xanh sau khi sửa do workflow CI kiểm.', ...lines);
    return;
  }
  if (results.some((r) => r.state === 'lỗi hạ tầng')) return fail('Lỗi khi dựng môi trường chạy test, chưa kết luận được. Chạy lại job.', lines);
  const reason = section(prEvent().body ?? '', NO_RED);
  if (filled(reason)) {
    notes.push(`Không có test nào đỏ trên code trước khi sửa, đã khai báo lý do ở mục "## ${NO_RED}". Người duyệt cần soi:`, ...lines, 'Lý do:', `    ${reason}`);
    return;
  }
  const hint = {
    xanh: 'Test của PR vẫn xanh trên code trước khi sửa, tức là chưa tái hiện được lỗi. Viết test đi đúng đường gây lỗi, thấy đỏ trên code cũ rồi mới sửa.',
    'không biên dịch': 'Test của PR không biên dịch được trên code trước khi sửa (gọi hàm hoặc kiểu mà bản sửa mới thêm), nên chưa chứng minh được test bắt lỗi. Viết test qua API đã có.',
    'không chạy test nào': 'Không có test nào chạy (bộ lọc không khớp, hoặc tệp test lỗi khi nạp).',
  };
  fail('Chưa chứng minh được test bắt được lỗi', [...lines, ...new Set(results.map((r) => hint[r.state]))]);
  notes.push(`Nếu thật sự không thể viết test đỏ trên code cũ, ghi lý do ở mục "## ${NO_RED}" trong mô tả PR.`);
}

const [mode, base, head] = process.argv.slice(2);
if (mode === 'template') {
  process.stdout.write(TEMPLATE);
  process.exit(0);
}
if (mode === 'commits') checkCommits(resolveBase(base, head), head);
else if (mode === 'guard') checkGuard(resolveBase(base, head), head);
else if (mode === 'pr') checkPr();
else if (mode === 'redfirst-plan') redFirstPlan();
else if (mode === 'redfirst-run' && STACKS[base]) redFirstRun(base);
else if (mode === 'redfirst-verdict') redFirstVerdict();
else {
  console.error('Dùng: repo-rules.mjs commits|guard <base> <head> | pr | template | redfirst-plan | redfirst-run dotnet|angular | redfirst-verdict');
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
