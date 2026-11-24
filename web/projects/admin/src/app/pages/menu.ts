import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { ChevronLeft, LucideAngularModule } from 'lucide-angular';

import { Api } from '../core/api';
import { altOf, Submit, VndPipe } from '../shared/ui';
import { askConfirm } from '../../../../customer/src/app/core/dialog';

type Group = Schemas['AdminOptionGroupDto'];
type Option = Schemas['AdminOptionDto'];
type Image = Schemas['ImageDto'];
// Ô sửa: nhóm/tùy chọn mới chưa có id, phiên bản, thứ tự
type GroupDraft = Omit<Group, 'id' | 'version' | 'displayOrder' | 'options'> & Partial<Pick<Group, 'id' | 'version'>>;
type OptionDraft = Omit<Option, 'id' | 'version' | 'displayOrder'> & Partial<Pick<Option, 'id' | 'version'>>;

@Component({
  selector: 'ot-option-groups',
  imports: [FormsModule, VndPipe],
  template: `
    <div class="a-head"><h1>Nhóm tùy chọn</h1><button type="button" class="ot-btn ot-btn-primary" (click)="newGroup()">Thêm nhóm</button></div>
    @if (form.error(); as e) { <p class="ot-error mt-2">{{ e }}</p> }
    @for (g of groups(); track g.id) {
      <section class="a-card mb-4 p-4">
        <div class="flex flex-wrap items-center justify-between gap-2"><b>{{ g.name }}</b><span class="text-sm text-muted">{{ g.selectionType === 'one' ? 'Chọn một' : 'Chọn nhiều' }} · {{ g.isRequired ? 'Bắt buộc' : 'Tùy ý' }} · {{ g.minSelect }}–{{ g.maxSelect }}</span>
          <span><button type="button" class="mr-3 inline-flex min-h-10 items-center text-primary" (click)="editGroup(g)">Sửa</button><button type="button" class="mr-3 inline-flex min-h-10 items-center text-primary" (click)="newOption(g)">Thêm tùy chọn</button><button type="button" class="inline-flex min-h-10 items-center text-danger" (click)="removeGroup(g)">Xóa</button></span></div>
        <ul class="mt-2">@for (o of g.options; track o.id) { <li class="flex justify-between gap-3 border-t border-line py-2"><span>{{ o.name }} · +{{ o.priceDelta | vnd }} · {{ optionStatus[o.status] }}@if (o.isDefault) { · mặc định }</span><span><button type="button" class="mr-3 inline-flex min-h-10 items-center text-primary" (click)="editOption(o)">Sửa</button><button type="button" class="inline-flex min-h-10 items-center text-danger" (click)="removeOption(o)">Xóa</button></span></li> }</ul>
      </section>
    }
    @if (group(); as m) {
      <div class="fixed inset-0 z-40 flex items-center justify-center bg-ink/50 p-4"><form class="ot-card grid w-full max-w-md gap-3 p-4" (ngSubmit)="saveGroup(m)">
        <label>Tên<input class="ot-input" name="n" required [(ngModel)]="m.name" /></label>
        <label>Kiểu<select class="ot-input" name="s" [(ngModel)]="m.selectionType"><option value="one">Chọn một</option><option value="many">Chọn nhiều</option></select></label>
        <label class="flex items-center gap-2"><input type="checkbox" name="r" [(ngModel)]="m.isRequired" /> Bắt buộc</label>
        <label>Tối thiểu<input class="ot-input" name="a" type="number" [(ngModel)]="m.minSelect" /></label><label>Tối đa<input class="ot-input" name="b" type="number" [(ngModel)]="m.maxSelect" /></label>
        <label class="flex items-center gap-2"><input type="checkbox" name="i" [(ngModel)]="m.isActive" /> Hoạt động</label>
        <div class="flex gap-2"><button class="ot-btn ot-btn-primary flex-1">Lưu</button><button type="button" class="ot-btn ot-btn-outline flex-1" (click)="group.set(null)">Đóng</button></div>
      </form></div>
    }
    @if (option(); as m) {
      <div class="fixed inset-0 z-40 flex items-center justify-center bg-ink/50 p-4"><form class="ot-card grid w-full max-w-md gap-3 p-4" (ngSubmit)="saveOption(m)">
        <label>Tên<input class="ot-input" name="n" required [(ngModel)]="m.name" /></label>
        <label>Giá cộng thêm<input class="ot-input" name="p" type="number" [(ngModel)]="m.priceDelta" /></label>
        <label class="flex items-center gap-2"><input type="checkbox" name="d" [(ngModel)]="m.isDefault" /> Mặc định</label>
        <label>Trạng thái<select class="ot-input" name="s" [(ngModel)]="m.status"><option value="available">Đang bán</option><option value="sold_out">Hết</option><option value="hidden">Ẩn</option></select></label>
        <div class="flex gap-2"><button class="ot-btn ot-btn-primary flex-1">Lưu</button><button type="button" class="ot-btn ot-btn-outline flex-1" (click)="option.set(null)">Đóng</button></div>
      </form></div>
    }
  `,
})
export class OptionGroups {
  protected readonly optionStatus: Record<string, string> = { available: 'Đang bán', sold_out: 'Hết', hidden: 'Ẩn' };
  private readonly api = inject(Api);
  protected readonly form = new Submit();
  protected readonly groups = signal<Group[]>([]);
  protected readonly group = signal<GroupDraft | null>(null);
  protected readonly option = signal<OptionDraft | null>(null);

  constructor() {
    this.load();
  }

  private load(): void {
    this.form.run(this.api.get<Group[]>('menu/option-groups'), (g) => this.groups.set(g));
  }

  protected newGroup(): void {
    this.group.set({ name: '', selectionType: 'one', isRequired: true, minSelect: 1, maxSelect: 1, isActive: true });
  }

  protected editGroup(g: Group): void {
    this.group.set({ ...g });
  }

  protected saveGroup(m: GroupDraft): void {
    const body = { name: m.name, selectionType: m.selectionType, isRequired: m.isRequired, minSelect: Number(m.minSelect), maxSelect: Number(m.maxSelect), isActive: m.isActive };
    this.form.run(m.id ? this.api.put(`menu/option-groups/${m.id}`, body, m.version) : this.api.post('menu/option-groups', body), () => {
      this.group.set(null);
      this.load();
    });
  }

  protected async removeGroup(g: Group): Promise<void> {
    if (await askConfirm('Xóa nhóm này?')) this.form.run(this.api.delete(`menu/option-groups/${g.id}`, g.version), () => this.load());
  }

  protected newOption(g: Group): void {
    this.option.set({ groupId: g.id, name: '', priceDelta: 0, isDefault: false, status: 'available', allergens: [] });
  }

  protected editOption(o: Option): void {
    this.option.set({ ...o });
  }

  protected saveOption(m: OptionDraft): void {
    const body = { name: m.name, priceDelta: Number(m.priceDelta), isDefault: m.isDefault, status: m.status, allergens: m.allergens ?? [] };
    this.form.run(m.id ? this.api.put(`menu/options/${m.id}`, body, m.version) : this.api.post(`menu/option-groups/${m.groupId}/options`, body), () => {
      this.option.set(null);
      this.load();
    });
  }

  protected async removeOption(o: Option): Promise<void> {
    if (await askConfirm('Xóa tùy chọn này?')) this.form.run(this.api.delete(`menu/options/${o.id}`, o.version), () => this.load());
  }
}

@Component({
  selector: 'ot-item-images',
  imports: [RouterLink, LucideAngularModule],
  template: `
    <a class="a-desk-only inline-flex items-center gap-1 text-muted" routerLink="/r/items"><lucide-icon [img]="icons.back" [size]="16" />Món ăn</a>
    <h1>Ảnh món</h1>
    @if (form.error(); as e) { <p class="ot-error mt-2">{{ e }}</p> }
    <div class="mt-3 grid grid-cols-2 gap-3 md:grid-cols-5">
      @for (i of all(); track i.id) {
        <figure class="ot-card p-2"><img class="aspect-square w-full rounded-sm object-cover" [src]="url(i)" [alt]="i.altText" />
          <figcaption class="mt-1 text-sm">@if (main() === i.id) { <b>Ảnh chính</b> } @else { <button type="button" class="text-primary" (click)="setMain(i.id)">Đặt làm chính</button> }<button type="button" class="ml-3 text-danger" (click)="drop(i.id)">Bỏ</button></figcaption>
        </figure>
      }
    </div>
    <label class="a-file mt-4 block max-w-sm">Thêm ảnh (tối đa 1 chính + 4 phụ)<span class="ot-btn ot-btn-outline mt-1 flex w-fit">Chọn ảnh</span><input class="sr-only" type="file" accept="image/*" (change)="add($event)" /></label>
    @if (form.busy()) { <p class="mt-2">Đang xử lý…</p> }
  `,
})
export class ItemImages {
  protected readonly icons = { back: ChevronLeft };
  private readonly api = inject(Api);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  protected readonly form = new Submit();
  protected readonly all = signal<Image[]>([]);
  protected readonly main = signal<string | null>(null);

  constructor() {
    this.load();
  }

  private load(): void {
    this.form.run(this.api.get<Schemas['MenuItemImagesDto']>(`menu/items/${this.id}/images`), (d) => {
      this.main.set(d.mainImage?.id ?? null);
      this.all.set([...(d.mainImage ? [d.mainImage] : []), ...d.images.filter((i) => i.id !== d.mainImage?.id)]);
    });
  }

  protected url(i: Image): string {
    return i.variants?.at(-1)?.url ?? '';
  }

  private save(main: string | null, ids: string[]): void {
    this.form.run(this.api.put(`menu/items/${this.id}/images`, { mainImageId: main, imageIds: ids.filter((x) => x !== main) }), () => this.load());
  }

  protected setMain(id: string): void {
    this.save(id, this.all().map((i) => i.id));
  }

  protected drop(id: string): void {
    this.save(this.main() === id ? null : this.main(), this.all().map((i) => i.id).filter((x) => x !== id));
  }

  protected add(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const data = new FormData();
    data.append('file', file);
    data.append('altText', altOf(file));
    this.form.run(this.api.post<Image>('images', data), (image) => this.save(this.main() ?? image.id, [...this.all().map((i) => i.id), image.id]));
  }
}
