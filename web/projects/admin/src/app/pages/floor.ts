import { CdkDrag, CdkDragEnd, CdkDragHandle } from '@angular/cdk/drag-drop';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Schemas } from '@ot/api';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { reloadOnLive } from '../core/live';
import { Latest, Submit } from '../shared/ui';
import { askConfirm } from '../../../../customer/src/app/core/dialog';

const TABLE_W = 150;
const TABLE_H = 124;

type Area = Schemas['FloorPlanAreaDto'];
type Table = Schemas['TableDto'];
// Bàn mới chưa có id, phiên bản, vị trí
type TableDraft = Pick<Table, 'areaId' | 'name' | 'minCapacity' | 'maxCapacity'> & Partial<Table>;

@Component({
  selector: 'ot-floor',
  imports: [FormsModule, CdkDrag, CdkDragHandle],
  template: `
    <div class="a-head">
      <h1>Sơ đồ bàn</h1>
      @if (canMove()) {
        <div class="a-btns">
          <button type="button" class="ot-btn ot-btn-outline" (click)="newArea()">Thêm tầng / khu vực</button>
          <button type="button" class="ot-btn ot-btn-primary" [disabled]="areas().length === 0" (click)="newTable()">Thêm bàn</button>
        </div>
      }
    </div>
    @if (form.error() || loader.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (areas().length === 0) {
      <div class="a-card a-card-b text-center text-muted">Chưa có tầng hoặc khu vực nào. Bấm "Thêm tầng / khu vực" để bắt đầu.</div>
    }
    @for (a of areas(); track a.id) {
      <section class="a-card mb-6">
        <header class="a-card-h">
          <span>{{ a.name }} <small class="ml-1 font-normal text-muted">{{ a.tables.length }} bàn</small></span>
          @if (canMove()) { <button type="button" class="a-link" (click)="newTable(a.id)">Thêm bàn vào đây</button> }
        </header>
        <div class="a-card-b">
          @if (canMove() && a.tables.length > 0) { <p class="mb-2 text-sm text-muted">Kéo tên bàn để đổi vị trí.</p> }
          <div class="relative h-[460px] overflow-auto rounded-md border border-line bg-surface-2">
            @for (t of a.tables; track t.id + ':' + t.version; let i = $index) {
              <div class="ot-card absolute p-2" style="width: 150px" [style.left.px]="pos(t, i).x" [style.top.px]="pos(t, i).y" cdkDrag cdkDragBoundary=".overflow-auto" [cdkDragDisabled]="!canMove()" (cdkDragEnded)="moved(t, i, $event)">
                <b class="block" [class.cursor-move]="canMove()" cdkDragHandle>{{ t.name }}</b><small class="block text-muted">{{ t.minCapacity }}–{{ t.maxCapacity }} khách</small>
                <select class="ot-input" [name]="'t' + t.id" [attr.aria-label]="'Trạng thái ' + t.name" [ngModel]="t.status" (ngModelChange)="set(t, $event)">@for (s of statuses; track s.value) { <option [value]="s.value">{{ s.label }}</option> }</select>
                @if (canMove()) { <div class="mt-1 flex justify-between text-sm"><button type="button" class="text-primary" (click)="editTable(t, a.id)">Sửa</button><button type="button" class="text-danger" (click)="removeTable(t)">Xóa</button></div> }
              </div>
            } @empty {
              <p class="p-4 text-muted">Chưa có bàn trong khu vực này.</p>
            }
          </div>
        </div>
      </section>
    }

    @if (area(); as m) {
      <div class="a-drawer" role="dialog" aria-modal="true">
        <form class="a-drawer-panel" (ngSubmit)="saveArea(m)">
          <div class="a-drawer-h">Thêm tầng / khu vực</div>
          <div class="a-drawer-b">
            <label>Tên (ví dụ: Tầng 1, Sân vườn)<input class="ot-input" name="name" required [(ngModel)]="m.name" /></label>
            @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
          </div>
          <div class="a-drawer-f"><button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Lưu</button><button type="button" class="ot-btn ot-btn-outline" (click)="area.set(null)">Đóng</button></div>
        </form>
      </div>
    }

    @if (table(); as m) {
      <div class="a-drawer" role="dialog" aria-modal="true">
        <form class="a-drawer-panel" (ngSubmit)="saveTable(m)">
          <div class="a-drawer-h">{{ m.id ? 'Sửa bàn' : 'Thêm bàn' }}</div>
          <div class="a-drawer-b">
            <label>Tầng / khu vực<select class="ot-input" name="area" required [(ngModel)]="m.areaId">@for (a of areas(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }</select></label>
            <label>Tên bàn<input class="ot-input" name="name" required [(ngModel)]="m.name" /></label>
            <label>Số khách ít nhất<input class="ot-input" name="min" type="number" min="1" required [(ngModel)]="m.minCapacity" /></label>
            <label>Số khách nhiều nhất<input class="ot-input" name="max" type="number" min="1" required [(ngModel)]="m.maxCapacity" /></label>
            @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
          </div>
          <div class="a-drawer-f"><button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Lưu</button><button type="button" class="ot-btn ot-btn-outline" (click)="table.set(null)">Đóng</button></div>
        </form>
      </div>
    }

  `,
})
export class Floor {
  private readonly api = inject(Api);
  private readonly auth = inject(Auth);
  protected readonly form = new Submit();
  protected readonly loader = new Latest();
  protected readonly areas = signal<Area[]>([]);
  protected readonly canMove = computed(() => this.auth.has('tables_manage'));
  protected readonly area = signal<{ name: string } | null>(null);
  protected readonly table = signal<TableDraft | null>(null);
  protected readonly statuses = [{ value: 'available', label: 'Trống' }, { value: 'reserved', label: 'Đã đặt' }, { value: 'occupied', label: 'Có khách' }, { value: 'cleaning', label: 'Đang dọn' }];

  constructor() {
    this.load();
    reloadOnLive(() => this.load());
  }

  private load(): void {
    this.loader.run(this.api.get<Area[]>('tables/floor-plan'), (a) => this.areas.set(a));
  }

  // Bàn chưa xếp chỗ thì tự dàn theo lưới
  protected pos(t: Table, i: number): { x: number; y: number } {
    const l = t.layout;
    return l && (l.x > 0 || l.y > 0) ? { x: l.x, y: l.y } : { x: 8 + (i % 4) * (TABLE_W + 8), y: 8 + Math.floor(i / 4) * (TABLE_H + 8) };
  }

  protected set(t: Table, status: string): void {
    this.form.run(this.api.patch(`tables/${t.id}/status`, { status }, t.version), () => this.load());
  }

  protected newArea(): void {
    this.form.error.set(null);
    this.area.set({ name: '' });
  }

  protected saveArea(m: { name: string }): void {
    this.form.run(this.api.post('table-areas', { name: m.name, isActive: true }), () => {
      this.area.set(null);
      this.load();
    });
  }

  protected newTable(areaId?: number): void {
    this.form.error.set(null);
    this.table.set({ areaId: areaId ?? this.areas()[0].id, name: '', minCapacity: 1, maxCapacity: 4 });
  }

  protected editTable(t: Table, areaId: number): void {
    this.form.error.set(null);
    this.table.set({ ...t, areaId });
  }

  protected saveTable(m: TableDraft): void {
    const body = { areaId: m.areaId, name: m.name, minCapacity: Number(m.minCapacity), maxCapacity: Number(m.maxCapacity), layout: m.layout ?? { x: 0, y: 0, width: TABLE_W, height: TABLE_H, rotation: 0 }, isActive: m.isActive ?? true };
    this.form.run(m.id ? this.api.put(`tables/${m.id}`, body, m.version) : this.api.post('tables', body), () => {
      this.table.set(null);
      this.load();
    });
  }

  protected async removeTable(t: Table): Promise<void> {
    if (await askConfirm(`Xóa bàn ${t.name}?`)) {
      this.form.run(this.api.delete(`tables/${t.id}`, t.version), () => this.load());
    }
  }

  protected moved(t: Table, i: number, event: CdkDragEnd): void {
    const base = this.pos(t, i);
    const drag = event.source.getFreeDragPosition();
    const layout = { x: Math.max(1, Math.round(base.x + drag.x)), y: Math.max(1, Math.round(base.y + drag.y)), width: TABLE_W, height: TABLE_H, rotation: 0 };
    const body = { areaId: t.areaId, name: t.name, minCapacity: t.minCapacity, maxCapacity: t.maxCapacity, layout, isActive: t.isActive };
    this.form.run(this.api.put(`tables/${t.id}`, body, t.version), () => this.load());
  }
}
