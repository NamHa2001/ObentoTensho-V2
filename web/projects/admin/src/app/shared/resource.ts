import { CdkDrag, CdkDragDrop, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { ChevronDown, LucideAngularModule } from 'lucide-angular';
import { finalize, Observable } from 'rxjs';

import { Api, Params, saveBlob } from '../core/api';
import { RichEditor } from './rich-editor';
import { altOf, errorText, Latest, Submit, utcFromVnInput, vnInputFromUtc } from './ui';
import { askConfirm, askText } from '../../../../customer/src/app/core/dialog';
import { Modal } from '../../../../customer/src/app/shared/modal';

// Một dòng bất kỳ của bảng chung; từng mục trong resources.ts khai báo kiểu dòng thật của API
export type Row = Record<string, unknown>;

export interface Option {
  value: string | number | boolean | null;
  label: string;
}

export interface Field<T = Row> {
  key: string;
  label: string;
  type?: 'text' | 'textarea' | 'rich' | 'number' | 'bool' | 'date' | 'datetime' | 'time' | 'select' | 'multi' | 'image' | 'email';
  options?: Option[];
  optionsFrom?: { path: string; value: string; label: string };
  required?: boolean;
  default?: unknown;
  // Lấy giá trị từ dòng dữ liệu khi tên trong DTO khác tên gửi lên
  read?: (row: T) => unknown;
  createOnly?: boolean;
}

export interface Action<T = Row> {
  label: string;
  run: (api: Api, row: T, note: string | null) => Observable<unknown>;
  when?: (row: T) => boolean;
  prompt?: string;
  confirm?: string;
  link?: (row: T) => unknown[];
}

export interface Tool {
  label: string;
  kind: 'download' | 'import' | 'reason' | 'form';
  path: string;
  file?: string;
  message?: string;
  fields?: Field[];
  toBody?: (model: Row) => unknown;
}

// Tên cột phải là trường có thật trong dòng dữ liệu
export interface Column<T = Row> {
  key: Extract<keyof T, string>;
  label: string;
  map?: (row: T) => unknown;
  thumb?: (row: T) => string | null | undefined;
}

export interface Resource<T = Row> {
  title: string;
  path: string;
  id?: string;
  paged?: boolean;
  columns: Column<T>[];
  fields?: Field<T>[];
  canCreate?: boolean;
  canEdit?: boolean;
  canDelete?: boolean;
  // Danh sách thiếu cột: lấy bản đầy đủ khi mở form sửa
  detail?: boolean;
  actions?: Action<T>[];
  filters?: Field[];
  toBody?: (model: Row) => unknown;
  // Dòng luôn có dù CSDL chưa có bản ghi (vd trang tĩnh chưa viết); bản ghi thật đè lên theo khóa
  seedRows?: Partial<T>[];
  tools?: Tool[];
  // API có PATCH {path}/reorder
  reorder?: boolean;
}

// Khai báo một mục kèm kiểu dòng thật của API để tên cột và các hàm đọc dòng được kiểm tra;
// màn hình chung chỉ cần biết dòng là bản ghi nên đổi về Resource<Row> tại đây
export function resource<T>(config: Resource<T>): Resource {
  return config as unknown as Resource;
}

function text(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

const NULLABLE = new Set(['text', 'textarea', 'rich', 'date', 'datetime', 'time', 'number', 'select', 'image', 'email']);

const STATUS_TEXT: Record<string, string> = {
  draft: 'Nháp', published: 'Đã đăng', hidden: 'Đã ẩn', scheduled: 'Đã hẹn', sending: 'Đang gửi', finished: 'Đã gửi',
  available: 'Trống', reserved: 'Đã giữ', occupied: 'Có khách', cleaning: 'Đang dọn', queued: 'Đang tạo', ready: 'Sẵn sàng', expired: 'Hết hạn',
};

function display(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'boolean') return value ? 'Có' : 'Không';
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) {
    return new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
  }

  return String(value);
}

// Mọi mục danh sách + thêm/sửa/xóa dùng chung một màn hình, khai báo ở resources.ts
@Component({
  selector: 'ot-resource',
  imports: [FormsModule, RouterLink, CdkDropList, CdkDrag, LucideAngularModule, RichEditor, Modal],
  template: `
    @if (config(); as c) {
      <div class="a-head">
        <h1>{{ c.title }}</h1>
        <div class="a-btns">
          @for (t of c.tools ?? []; track t.label) {
            @if (t.kind === 'import') {
              <label class="ot-btn ot-btn-outline cursor-pointer">{{ t.label }}<input class="sr-only" type="file" accept=".xlsx" (change)="importFile($event, t)" /></label>
            } @else {
              <button type="button" class="ot-btn ot-btn-outline" (click)="runTool(t)">{{ t.label }}</button>
            }
          }
          @if (c.canCreate) { <button type="button" class="ot-btn ot-btn-primary" (click)="create()">Thêm mới</button> }
        </div>
      </div>
      @if (c.filters?.length) {
        <div class="a-filters">
          @for (f of c.filters; track f.key) {
            @if (f.options || f.optionsFrom) {
              <select class="ot-input" [name]="f.key" [attr.aria-label]="f.label" [ngModel]="filter()[f.key] ?? ''" (ngModelChange)="setFilter(f.key, $event)"><option value="">{{ f.label }}</option>@for (o of optionsOf(f); track o.value) { <option [value]="o.value">{{ o.label }}</option> }</select>
            } @else {
              <input class="ot-input" [type]="f.type ?? 'text'" [placeholder]="f.label" [attr.aria-label]="f.label" [name]="f.key" [ngModel]="filter()[f.key] ?? ''" (change)="setFilter(f.key, $any($event.target).value)" />
            }
          }
        </div>
      }
      @if (error() || loader.error(); as e) { <p class="ot-error mb-3" role="alert">{{ e }}</p> }
      @if (notice(); as n) { <p class="mb-3 font-semibold" role="status">{{ n }}</p> }

      <div class="a-table">
        <table>
          <thead><tr>@for (col of c.columns; track col.key) { <th>{{ col.label }}</th> }@if (hasActions(c)) { <th class="a-actions">Thao tác</th> }</tr></thead>
          <tbody cdkDropList [cdkDropListDisabled]="!c.reorder" (cdkDropListDropped)="drop($event)">
            @for (row of rows(); track $index) {
              <tr cdkDrag [cdkDragDisabled]="!c.reorder" [class.cursor-move]="c.reorder">
                @for (col of c.columns; track col.key) {
                  <td>
                    @if (col.thumb) {
                      @if (col.thumb(row); as src) { <img class="a-thumb" [src]="src" alt="" /> } @else { <span class="a-thumb"></span> }
                    } @else if (tone(col, row); as t) {
                      <span class="a-badge" [class.a-badge-good]="t === 'good'" [class.a-badge-warn]="t === 'warn'" [class.a-badge-bad]="t === 'bad'">{{ cell(col, row) }}</span>
                    } @else {
                      {{ cell(col, row) }}
                    }
                  </td>
                }
                @if (hasActions(c)) { <td class="a-actions"><button type="button" class="ot-chip gap-1" (click)="openMenu($event, row)">Thao tác <lucide-icon [img]="more" [size]="16" /></button></td> }
              </tr>
            } @empty {
              <tr><td class="text-center text-muted" [attr.colspan]="c.columns.length + 1" style="padding-block: 40px">{{ loading() ? 'Đang tải…' : 'Chưa có dữ liệu.' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (c.reorder) { <p class="mt-3 text-sm text-muted">Kéo từng dòng để đổi thứ tự hiển thị.</p> }
      @if (c.paged && total() > pageSize) {
        <div class="a-pager">
          <span class="text-sm text-muted">Trang {{ page() }} / {{ pages() }} · {{ total() }} dòng</span>
          <button type="button" class="ot-chip" [disabled]="page() <= 1" (click)="go(page() - 1)">Trước</button>
          <button type="button" class="ot-chip" [disabled]="page() >= pages()" (click)="go(page() + 1)">Sau</button>
        </div>
      }

      @if (menu(); as open) {
        <div class="fixed inset-0 z-40" otModal (dismiss)="menu.set(null)"></div>
        <div class="a-menu" [style.top.px]="open.top" [style.right.px]="open.right">
          @if (c.canEdit) { <button type="button" class="a-drop-item" (click)="menu.set(null); edit(open.row)">Sửa</button> }
          @for (a of c.actions ?? []; track a.label) {
            @if (!a.when || a.when(open.row)) {
              @if (a.link) { <a class="a-drop-item" [routerLink]="a.link(open.row)" (click)="menu.set(null)">{{ a.label }}</a> } @else { <button type="button" class="a-drop-item" (click)="menu.set(null); act(a, open.row)">{{ a.label }}</button> }
            }
          }
          @if (c.canDelete) { <button type="button" class="a-drop-item text-danger" (click)="menu.set(null); remove(open.row)">Xóa</button> }
        </div>
      }

      @if (editing(); as m) {
        <div class="a-drawer" role="dialog" aria-modal="true">
          <form class="a-drawer-panel" (ngSubmit)="save()">
            <div class="a-drawer-h">{{ m['__tool'] ?? (m['__id'] === undefined ? 'Thêm mới' : 'Sửa') }} · {{ c.title }}</div>
            <div class="a-drawer-b">
              @for (f of visibleFields(m); track f.key) {
                <div>
                  @switch (f.type) {
                    @case ('bool') { <label class="flex min-h-7 items-center gap-2"><input type="checkbox" [name]="f.key" [(ngModel)]="m[f.key]" /> {{ f.label }}</label> }
                    @case ('textarea') { <label>{{ f.label }}<textarea class="ot-input" rows="6" [name]="f.key" [(ngModel)]="m[f.key]"></textarea></label> }
                    @case ('rich') { <p>{{ f.label }}</p><ot-rich-editor class="mt-1 block" [label]="f.label" [value]="asText(m[f.key])" (valueChange)="m[f.key] = $event" /> }
                    @case ('select') { <label>{{ f.label }}<select class="ot-input" [name]="f.key" [(ngModel)]="m[f.key]" [required]="!!f.required">@if (!f.required) { <option [ngValue]="null">Không chọn</option> }@for (o of optionsOf(f); track o.value) { <option [ngValue]="o.value">{{ o.label }}</option> }</select></label> }
                    @case ('multi') { <fieldset><legend>{{ f.label }}</legend><div class="mt-1 grid grid-cols-2 gap-2">@for (o of optionsOf(f); track o.value) { <label class="flex items-center gap-2 font-normal"><input type="checkbox" [name]="f.key + o.value" [ngModel]="checked(m, f.key, o.value)" (ngModelChange)="toggle(m, f.key, o.value, $event)" /> {{ o.label }}</label> }</div></fieldset> }
                    @case ('image') {
                      <label>{{ f.label }}<input class="ot-input" type="file" accept="image/*" (change)="upload($event, m, f.key)" /></label>
                      @if (m[f.key]) { <p class="mt-1 text-sm text-muted">Đã có ảnh</p> @if (previews()[f.key]; as src) { <img class="mt-1 h-24 rounded-sm" [src]="src" alt="" /> } }
                      @if (uploading()) { <p class="mt-1 text-sm">Đang tải ảnh lên…</p> }
                    }
                    @default { <label>{{ f.label }}<input class="ot-input" [type]="inputType(f)" [step]="f.type === 'number' ? 'any' : null" [name]="f.key" [(ngModel)]="m[f.key]" [required]="!!f.required" /></label> }
                  }
                </div>
              }
              @if (form.error(); as e) { <p class="ot-error" role="alert">{{ e }}</p> }
            </div>
            <div class="a-drawer-f"><button class="ot-btn ot-btn-primary" [disabled]="form.busy() || uploading()">Lưu</button><button type="button" class="ot-btn ot-btn-outline" (click)="editing.set(null)">Đóng</button></div>
          </form>
        </div>
      }
    }
  `,
})
export class ResourcePage {
  private readonly api = inject(Api);
  protected readonly more = ChevronDown;
  readonly resource = input<Resource | null>(null);
  private readonly routeConfig = inject(ActivatedRoute, { optional: true });
  protected readonly config = signal<Resource | null>(null);
  protected readonly rows = signal<Row[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly filter = signal<Params>({});
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly editing = signal<Row | null>(null);
  protected readonly previews = signal<Record<string, string>>({});
  protected readonly uploading = signal(false);
  protected readonly menu = signal<{ row: Row; top: number; right: number } | null>(null);
  protected readonly lookups = signal<Record<string, Option[]>>({});
  protected readonly form = new Submit();
  protected readonly loader = new Latest();
  protected readonly notice = signal<string | null>(null);
  protected readonly pageSize = 20;
  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));

  constructor() {
    this.routeConfig?.data.subscribe((d) => this.use(d['resource'] as Resource));
    effect(() => {
      const r = this.resource();
      if (r) this.use(r);
    });
  }

  private use(r: Resource | undefined): void {
    if (!r) return;
    this.config.set(r);
    this.page.set(1);
    this.filter.set({});
    this.rows.set([]);
    this.editing.set(null);
    this.load();
    for (const f of [...(r.fields ?? []), ...(r.filters ?? [])]) {
      if (f.optionsFrom) {
        const from = f.optionsFrom;
        this.api.raw<Row[]>(from.path, { pageSize: 100 }).subscribe({ next: (x) => this.lookups.update((l) => ({ ...l, [f.key]: x.data.map((i) => ({ value: i[from.value] as Option['value'], label: String(i[from.label]) })) })), error: () => undefined });
      }
    }
  }

  protected load(): void {
    const c = this.config();
    if (!c) return;
    this.loading.set(true);
    this.error.set(null);
    this.loader.run(this.api.raw<Row[]>(c.path, { ...this.filter(), ...(c.paged ? { page: this.page(), pageSize: this.pageSize } : {}) }).pipe(finalize(() => this.loading.set(false))), (r) => {
        const rows = Array.isArray(r.data) ? r.data : [];
        const key = c.id ?? 'id';
        this.rows.set(c.seedRows ? c.seedRows.map((seed) => rows.find((x) => x[key] === seed[key]) ?? seed) : rows);
        this.total.set(r.meta?.total ?? 0);
    });
  }

  protected runTool(t: Tool): void {
    this.notice.set(null);
    if (t.kind === 'download') {
      this.api.download(t.path).subscribe({ next: (r) => saveBlob(r, t.file ?? 'tep.xlsx'), error: (e: unknown) => this.error.set(errorText(e)) });
    } else if (t.kind === 'reason') {
      void this.reasonTool(t);
    } else {
      const model: Row = { __tool: t.label, __toolRef: t };
      for (const f of t.fields ?? []) {
        model[f.key] = f.default !== undefined ? f.default : f.type === 'bool' ? false : null;
      }

      this.form.error.set(null);
      this.editing.set(model);
    }
  }

  private async reasonTool(t: Tool): Promise<void> {
    const reason = await askText(t.message ?? 'Lý do');
    if (reason) {
      this.api.post(t.path, { reason }).subscribe({ next: () => this.notice.set('Đang tạo tệp. Liên kết tải sẽ gửi tới email của bạn.'), error: (e: unknown) => this.error.set(errorText(e)) });
    }
  }

  // BR-MNU-14: gửi lần đầu chỉ kiểm tra, người dùng đồng ý mới ghi
  protected importFile(event: Event, t: Tool): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (file) {
      const data = new FormData();
      data.append('file', file);
      this.notice.set(null);
      this.api.post<Schemas['MenuImportReport']>(t.path, data).subscribe({ next: (report) => void this.confirmImport(report, data, t), error: (e: unknown) => this.error.set(errorText(e)) });
    }
  }

  private async confirmImport(report: Schemas['MenuImportReport'], data: FormData, t: Tool): Promise<void> {
    if (report.failed > 0) {
      const lines = report.rows.filter((r) => r.errors.length > 0).slice(0, 5).map((r) => `Dòng ${r.row}: ${r.errors.map((e) => e.column).join(', ')}`);
      this.error.set(`Tệp có ${report.failed} dòng lỗi, chưa ghi gì. ${lines.join('; ')}`);
      return;
    }

    if (await askConfirm(`Thêm ${report.created} món, cập nhật ${report.updated} món?`)) {
      this.api.post(`${t.path}?commit=true`, data).subscribe({
        next: () => {
          this.notice.set('Đã nhập xong.');
          this.load();
        },
        error: (e: unknown) => this.error.set(errorText(e)),
      });
    }
  }

  protected go(page: number): void {
    this.page.set(page);
    this.load();
  }

  protected setFilter(key: string, value: string): void {
    this.filter.update((f) => ({ ...f, [key]: value }));
    this.page.set(1);
    this.load();
  }

  protected cell(col: Column, row: Row): string {
    return display(col.map ? col.map(row) : col.key === 'status' ? (STATUS_TEXT[String(row['status'])] ?? row['status']) : row[col.key]);
  }

  protected hasActions(c: Resource): boolean {
    return !!c.canEdit || !!c.canDelete || (c.actions?.length ?? 0) > 0;
  }

  // Trạng thái và giá trị có/không hiện dạng nhãn màu
  protected tone(col: Column, row: Row): 'good' | 'warn' | 'bad' | 'plain' | null {
    const value = row[col.key];
    if (typeof value === 'boolean') {
      return value ? 'good' : 'plain';
    }

    if (col.key !== 'status' && col.key !== 'approvalStatus') {
      return null;
    }

    if (typeof value !== 'string') return 'plain';

    if (['available', 'approved', 'published', 'finished', 'ready', 'active'].includes(value)) return 'good';
    if (['pending', 'draft', 'sold_out', 'scheduled', 'sending', 'queued', 'reserved', 'cleaning'].includes(value)) return 'warn';
    if (['rejected', 'cancelled', 'expired', 'occupied'].includes(value)) return 'bad';
    return 'plain';
  }

  // Đặt theo màn hình để không bị khung cuộn của bảng cắt mất; gần đáy thì mở lên trên
  protected openMenu(event: MouseEvent, row: Row): void {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const height = 40 * (2 + (this.config()?.actions?.length ?? 0)) + 12;
    const top = rect.bottom + height + 8 > innerHeight ? Math.max(8, rect.top - height - 4) : rect.bottom + 4;
    this.menu.set({ row, top, right: Math.max(8, innerWidth - rect.right) });
  }

  protected optionsOf(f: Field): Option[] {
    return f.options ?? this.lookups()[f.key] ?? [];
  }

  protected inputType(f: Field): string {
    return f.type === 'datetime' ? 'datetime-local' : (f.type ?? 'text');
  }

  protected asText(value: unknown): string | null {
    return text(value);
  }

  protected checked(m: Row, key: string, value: unknown): boolean {
    const current = m[key];
    return Array.isArray(current) && current.includes(value);
  }

  protected visibleFields(m: Row): Field[] {
    const tool = m['__toolRef'] as Tool | undefined;
    return tool?.fields ?? (this.config()?.fields ?? []).filter((f) => !f.createOnly || m['__id'] === undefined);
  }

  protected toggle(m: Row, key: string, value: unknown, on: boolean): void {
    const current: unknown[] = Array.isArray(m[key]) ? m[key] : [];
    m[key] = on ? [...current, value] : current.filter((v) => v !== value);
  }

  protected create(): void {
    const model: Row = {};
    for (const f of this.config()?.fields ?? []) {
      model[f.key] = f.default !== undefined ? f.default : f.type === 'bool' ? false : f.type === 'multi' ? [] : null;
    }

    this.previews.set({});
    this.form.error.set(null);
    this.editing.set(model);
  }

  protected edit(row: Row): void {
    const c = this.config() as Resource;
    const id = row[c.id ?? 'id'];
    if (c.detail) {
      this.api.get<Row>(`${c.path}/${id}`).subscribe({ next: (full) => this.open({ ...row, ...full }, id), error: (e: { status?: number }) => (c.seedRows && e.status === 404 ? this.open(row, id) : this.error.set(errorText(e))) });
    } else {
      this.open(row, id);
    }
  }

  private open(row: Row, id: unknown): void {
    const model: Row = { __id: id, __version: row['version'] };
    for (const f of this.config()?.fields ?? []) {
      let v = f.read ? f.read(row) : row[f.key];
      if (f.type === 'datetime' && typeof v === 'string') v = vnInputFromUtc(v);
      if (f.type === 'time' && typeof v === 'string') v = v.slice(0, 5);
      model[f.key] = v ?? (f.type === 'multi' ? [] : null);
    }

    this.previews.set({});
    this.form.error.set(null);
    this.editing.set(model);
  }

  protected upload(event: Event, m: Row, key: string): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const data = new FormData();
    data.append('file', file);
    data.append('altText', altOf(file));
    this.uploading.set(true);
    this.form.error.set(null);
    this.api.post<{ id: string }>('images', data).subscribe({
      next: (image) => {
        m[key] = image.id;
        this.previews.update((p) => ({ ...p, [key]: URL.createObjectURL(file) }));
        this.uploading.set(false);
      },
      error: (e: unknown) => {
        this.form.error.set(errorText(e));
        this.uploading.set(false);
      },
    });
  }

  protected save(): void {
    const c = this.config() as Resource;
    const m = this.editing() as Row;
    const tool = m['__toolRef'] as Tool | undefined;
    const body: Row = {};
    for (const f of tool?.fields ?? c.fields ?? []) {
      let v = m[f.key];
      if (v === '' && NULLABLE.has(f.type ?? 'text')) v = null;
      if (f.type === 'number' && v !== null) v = Number(v);
      if (f.type === 'datetime') v = utcFromVnInput(text(v));
      if (f.createOnly && m['__id'] !== undefined) continue;
      body[f.key] = v;
    }

    if (tool) {
      this.form.run(this.api.post<Partial<Schemas['BulkGenerateResultDto']> | null>(tool.path, tool.toBody ? tool.toBody(body) : body), (r) => {
        this.editing.set(null);
        this.notice.set(r?.count ? `Đã tạo ${r.count} mã.` : 'Đã xong.');
        this.load();
      });
      return;
    }

    const payload = c.toBody ? c.toBody(body) : body;
    const request = m['__id'] === undefined ? this.api.post(c.path, payload) : this.api.put(`${c.path}/${m['__id']}`, payload, text(m['__version']));
    this.form.run(request, () => {
      this.editing.set(null);
      this.load();
    });
  }

  protected drop(event: CdkDragDrop<unknown>): void {
    const c = this.config() as Resource;
    const rows = [...this.rows()];
    moveItemInArray(rows, event.previousIndex, event.currentIndex);
    this.rows.set(rows);
    this.api.patch(`${c.path}/reorder`, { ids: rows.map((r) => r['id']) }).subscribe({
      error: (e: unknown) => {
        this.error.set(errorText(e));
        this.load();
      },
    });
  }

  protected async remove(row: Row): Promise<void> {
    const c = this.config() as Resource;
    if (await askConfirm('Bạn chắc chắn muốn xóa?')) {
      this.api.delete(`${c.path}/${row[c.id ?? 'id']}`, text(row['version'])).subscribe({ next: () => this.load(), error: (e: unknown) => this.error.set(errorText(e)) });
    }
  }

  protected async act(a: Action, row: Row): Promise<void> {
    let note: string | null = null;
    if (a.prompt) {
      note = await askText(a.prompt);
      if (note === null) return;
    } else if (a.confirm && !await askConfirm(a.confirm)) {
      return;
    }

    a.run(this.api, row, note).subscribe({ next: () => this.load(), error: (e: unknown) => this.error.set(errorText(e)) });
  }
}
