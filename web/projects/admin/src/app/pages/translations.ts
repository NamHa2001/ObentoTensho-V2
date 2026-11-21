import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Schemas } from '@ot/api';

import { Api } from '../core/api';
import { errorText, Submit } from '../shared/ui';

interface TranslateKind {
  value: string;
  label: string;
  list: string;
  name: string;
  key?: string;
  menu?: string;
  fields: { key: string; label: string; area?: boolean }[];
  flatten?: boolean;
}

const MENU_ALL = [{ key: 'name', label: 'Tên' }, { key: 'shortDesc', label: 'Mô tả ngắn' }, { key: 'fullDescHtml', label: 'Mô tả (HTML)', area: true }];
const MENU_NAME = [{ key: 'name', label: 'Tên' }];
const TRANSLATE: TranslateKind[] = [
  { value: 'items', label: 'Món ăn', list: 'menu/items', name: 'nameVi', menu: 'items', fields: MENU_ALL },
  { value: 'categories', label: 'Danh mục món', list: 'menu/categories', name: 'name', menu: 'categories', fields: MENU_NAME },
  { value: 'option-groups', label: 'Nhóm tùy chọn', list: 'menu/option-groups', name: 'name', menu: 'option-groups', fields: MENU_NAME },
  { value: 'options', label: 'Tùy chọn', list: 'menu/option-groups', name: 'name', menu: 'options', fields: MENU_NAME, flatten: true },
  { value: 'promotion', label: 'Khuyến mãi', list: 'promotions', name: 'title', fields: [{ key: 'title', label: 'Tiêu đề' }, { key: 'bodyHtml', label: 'Nội dung (HTML)', area: true }] },
  { value: 'banner', label: 'Banner', list: 'banners', name: 'title', fields: [{ key: 'title', label: 'Tiêu đề' }, { key: 'summary', label: 'Dòng phụ' }, { key: 'ctaText', label: 'Chữ trên nút' }] },
  { value: 'gallery', label: 'Không gian', list: 'gallery', name: 'caption', fields: [{ key: 'title', label: 'Chú thích' }] },
  { value: 'news', label: 'Tin tức', list: 'news', name: 'title', fields: [{ key: 'title', label: 'Tiêu đề' }, { key: 'summary', label: 'Tóm tắt' }, { key: 'bodyHtml', label: 'Nội dung (HTML)', area: true }, { key: 'metaTitle', label: 'Tiêu đề SEO' }, { key: 'metaDescription', label: 'Mô tả SEO' }] },
  { value: 'static_page', label: 'Trang tĩnh', list: 'pages', name: 'slug', key: 'slug', fields: [{ key: 'title', label: 'Tiêu đề' }, { key: 'bodyHtml', label: 'Nội dung (HTML)', area: true }] },
];

const PROGRESS_LABELS: Record<string, string> = { menu_item: 'Món ăn', menu_category: 'Danh mục món', option_group: 'Nhóm tùy chọn', option: 'Tùy chọn', static_page: 'Trang tĩnh', promotion: 'Khuyến mãi', banner: 'Banner', gallery: 'Không gian', news: 'Tin tức' };

type Row = Record<string, unknown>;

// Bản dịch món ăn và nội dung khác nhau ở các trường nội dung, giống nhau ở ngôn ngữ và trạng thái duyệt
interface Translation extends Row {
  lang: string;
  status: string;
  outdated: boolean;
}

@Component({
  selector: 'ot-translations',
  imports: [FormsModule],
  template: `
    <h1>Bản dịch</h1>
    @for (lang of langs; track lang) {
      @if (data()[lang]; as p) {
        <h2 class="mt-4 text-lg">{{ lang.toUpperCase() }} · {{ p.enabled ? 'Đang bật' : 'Chưa bật' }} · {{ p.ready ? 'Đủ điều kiện' : 'Chưa đủ' }}</h2>
        <div class="a-table mb-4"><table class="w-full text-sm"><tr class="bg-surface-2 text-left"><th class="p-2">Loại</th><th class="p-2">Tổng</th><th class="p-2">Đã duyệt</th><th class="p-2">Lỗi thời</th><th class="p-2">Nháp</th><th class="p-2">Thiếu</th></tr>@for (k of p.kinds; track k.kind) { <tr class="border-b border-line"><td class="p-2">{{ kindLabel(k.kind) }}</td><td class="p-2">{{ k.total }}</td><td class="p-2">{{ k.approved }}</td><td class="p-2">{{ k.outdated }}</td><td class="p-2">{{ k.draft }}</td><td class="p-2">{{ k.missing }}</td></tr> }</table></div>
      }
    }

    <h2 class="mt-6 text-xl">Nhập bản dịch</h2>
    <form class="ot-stack max-w-xl" (ngSubmit)="save()">
      <label>Loại nội dung<select class="ot-input" name="kind" [ngModel]="kind().value" (ngModelChange)="pickKind($event)">@for (k of kinds; track k.value) { <option [value]="k.value">{{ k.label }}</option> }</select></label>
      <label>Mục<select class="ot-input" name="item" [ngModel]="itemKey()" (ngModelChange)="pickItem($event)"><option [ngValue]="null">Chọn mục</option>@for (r of rows(); track r.key) { <option [ngValue]="r.key">{{ r.name }}</option> }</select></label>
      <label>Ngôn ngữ<select class="ot-input" name="lang" [ngModel]="lang()" (ngModelChange)="pickLang($event)">@for (l of langs; track l) { <option [value]="l">{{ l.toUpperCase() }}</option> }</select></label>
      @if (itemKey() !== null) {
        @if (status(); as st) { <p class="text-sm text-muted">Hiện tại: {{ st }}</p> }
        @for (f of kind().fields; track f.key) {
          <div>
            <label [for]="'tr-' + f.key">{{ f.label }}</label>
            @if (f.area) { <textarea class="ot-input" rows="6" [id]="'tr-' + f.key" [name]="f.key" [(ngModel)]="model[f.key]"></textarea> } @else { <input class="ot-input" [id]="'tr-' + f.key" [name]="f.key" [(ngModel)]="model[f.key]" /> }
          </div>
        }
        <label class="flex items-center gap-2"><input type="checkbox" name="approve" [(ngModel)]="approve" /> Duyệt bản dịch này</label>
        @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
        @if (saved()) { <p>Đã lưu.</p> }
        <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Lưu bản dịch</button>
      }
    </form>
  `,
})
export class Translations {
  private readonly api = inject(Api);
  protected readonly langs = ['en', 'ja'];
  protected readonly kinds = TRANSLATE;
  protected readonly form = new Submit();
  protected readonly data = signal<Record<string, Schemas['TranslationProgressDto']>>({});
  protected readonly kind = signal(TRANSLATE[0]);
  protected readonly rows = signal<{ key: string; name: string }[]>([]);
  protected readonly itemKey = signal<string | null>(null);
  protected readonly lang = signal('en');
  protected readonly status = signal<string | null>(null);
  protected readonly saved = signal(false);
  protected model: Record<string, string | null> = {};
  protected approve = false;

  constructor() {
    this.loadProgress();
    this.pickKind(TRANSLATE[0].value);
  }

  protected kindLabel(kind: string): string {
    return PROGRESS_LABELS[kind] ?? kind;
  }

  private loadProgress(): void {
    for (const lang of this.langs) {
      this.api.get<Schemas['TranslationProgressDto']>('translations/progress', { lang }).subscribe({ next: (p) => this.data.update((d) => ({ ...d, [lang]: p })), error: () => undefined });
    }
  }

  protected pickKind(value: string): void {
    const kind = TRANSLATE.find((k) => k.value === value) ?? TRANSLATE[0];
    this.kind.set(kind);
    this.itemKey.set(null);
    this.rows.set([]);
    // Mỗi loại nội dung có danh sách khác nhau; chỉ đọc trường khoá và tên khai báo trong TRANSLATE
    this.api.raw<Row[]>(kind.list, { page: 1, pageSize: 50 }).subscribe({
      next: (r) => {
        const source = kind.flatten ? r.data.flatMap((g) => g['options'] as Row[]) : r.data;
        this.rows.set(source.filter((x) => x[kind.name]).map((x) => ({ key: String(x[kind.key ?? 'id']), name: String(x[kind.name]) })));
      },
      error: () => this.rows.set([]),
    });
  }

  protected pickItem(key: string | null): void {
    this.itemKey.set(key);
    this.loadCurrent();
  }

  protected pickLang(lang: string): void {
    this.lang.set(lang);
    this.loadCurrent();
  }

  private path(): string {
    const kind = this.kind();
    return kind.menu ? `menu/${kind.menu}/${this.itemKey()}/translations` : `translations/${kind.value}/${this.itemKey()}`;
  }

  private loadCurrent(): void {
    this.model = {};
    this.status.set(null);
    this.saved.set(false);
    this.approve = false;
    if (this.itemKey() === null) {
      return;
    }

    this.api.get<Translation[]>(this.path()).subscribe({
      next: (list) => {
        const found = list.find((t) => t.lang === this.lang());
        for (const f of this.kind().fields) {
          this.model[f.key] = (found?.[f.key] as string | null | undefined) ?? '';
        }

        this.approve = found?.status === 'approved' && !found?.outdated;
        this.status.set(found ? `${found.status === 'approved' ? 'Đã duyệt' : 'Nháp'}${found.outdated ? ' · lỗi thời' : ''}` : 'Chưa dịch');
      },
      error: (e: unknown) => this.form.error.set(errorText(e)),
    });
  }

  protected save(): void {
    this.saved.set(false);
    const kind = this.kind();
    const empty = kind.menu ? { name: null, shortDesc: null, fullDescHtml: null } : { title: null, summary: null, bodyHtml: null, ctaText: null, metaTitle: null, metaDescription: null };
    const filled = Object.fromEntries(Object.entries(this.model).map(([k, v]) => [k, v || null]));
    this.form.run(this.api.put(`${this.path()}/${this.lang()}`, { ...empty, ...filled, approve: this.approve }), () => {
      this.saved.set(true);
      this.loadProgress();
    });
  }
}
