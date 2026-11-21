import { afterNextRender, Component, computed, HostListener, inject, Injector, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { catchError, map, of } from 'rxjs';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { reloadOnLive } from '../core/live';
import { Latest, Submit, VndPipe } from '../shared/ui';
import { askConfirm, askText } from '../../../../customer/src/app/core/dialog';
import { buyerBody, emptyBuyer, InvoiceForm } from '../../../../customer/src/app/shared/invoice-form';

type TableCard = Schemas['DineInTableDto'];
type Bill = Schemas['DineInBillDto'];
type Line = Bill['items'][number];
type Receipt = Schemas['DineInReceiptDto'];
type MenuItem = Schemas['PublicMenuItemDto'];
type OptionGroup = Schemas['PublicOptionGroupDto'];

// Bảng chọn tùy chọn cho một món trước khi thêm vào đợt gọi
interface Picking {
  item: MenuItem;
  groups: OptionGroup[];
  selected: Set<number>;
  quantity: number;
}

// Phiếu in: tạm tính lúc khách xem hoặc phiếu thanh toán sau khi thu tiền
interface Slip {
  title: string;
  tableName: string;
  guestCount: number;
  code: string;
  items: Pick<Line, 'id' | 'quantity' | 'name' | 'lineTotal' | 'options'>[];
  subTotal: number;
  memberDiscount: number;
  voucherDiscount: number;
  totalAmount: number;
  paymentLabel: string;
}

const TABLE_STATUS: Record<string, string> = { available: 'Trống', reserved: 'Đã đặt', occupied: 'Có khách', cleaning: 'Đang dọn' };

function minutesSince(utc: string | null | undefined): number {
  return utc ? Math.max(0, Math.round((Date.now() - new Date(utc).getTime()) / 60000)) : 0;
}

function clock(utc: string): string {
  return new Date(utc).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
}

// Sơ đồ gọi món: bàn theo tầng/khu vực; bàn có phiếu thì vào phiếu, bàn trống thì mở bàn
@Component({
  selector: 'ot-dine-in',
  imports: [FormsModule, RouterLink, VndPipe],
  template: `
    <div class="a-head">
      <h1>Gọi món tại bàn</h1>
      <div class="a-btns">
        <button type="button" class="ot-btn ot-btn-primary" (click)="startCounter()">+ Đơn mang về tại quầy</button>
        <button type="button" class="ot-chip" (click)="load()">Tải lại</button>
      </div>
    </div>
    @if (form.error() || loader.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    @if (counter().length > 0) {
      <section class="a-card mb-6">
        <header class="a-card-h"><span>Mua mang về tại quầy <small class="ml-1 font-normal text-muted">{{ counter().length }} đơn</small></span></header>
        <div class="a-card-b grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 2xl:grid-cols-6">
          @for (c of counter(); track c.id) {
            <button type="button" class="a-dine-table" [attr.data-status]="c.paid ? 'reserved' : 'occupied'" (click)="openBill(c.id)">
              <b class="text-lg">{{ c.customerName }}</b>
              <span class="text-sm">{{ since(c.openedAtUtc) }} phút · {{ c.subTotal | vnd }}</span>
              @if (c.paid) { <span class="a-badge self-start">Đã trả tiền, chờ món</span> }
              @else if (c.unservedCount > 0) { <span class="a-badge self-start">{{ c.unservedCount }} món chờ bếp</span> }
            </button>
          }
        </div>
      </section>
    }
    <p class="mb-4 text-sm text-muted">Bấm bàn trống để mở bàn, bấm bàn có khách để gọi thêm món hoặc thanh toán. Khách mua mang đi thì bấm "+ Đơn mang về tại quầy".</p>
    @for (g of groups(); track g.name) {
      <section class="a-card mb-6">
        <header class="a-card-h"><span>{{ g.name }} <small class="ml-1 font-normal text-muted">{{ g.tables.length }} bàn</small></span></header>
        <div class="a-card-b grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 2xl:grid-cols-6">
          @for (t of g.tables; track t.tableId) {
            <button type="button" class="a-dine-table" [attr.data-status]="t.billId ? 'occupied' : t.status" (click)="pick(t)">
              <b class="text-lg">{{ t.tableName }}</b>
              @if (t.billId) {
                <span class="text-sm">{{ t.guestCount }} khách · {{ since(t.openedAtUtc) }} phút</span>
                <span class="font-semibold">{{ t.subTotal ?? 0 | vnd }}</span>
                @if (t.unservedCount > 0) { <span class="a-badge self-start">{{ t.unservedCount }} món chờ bếp</span> }
              } @else {
                <span class="text-sm">{{ label(t.status) }} · tối đa {{ t.maxCapacity }} khách</span>
              }
            </button>
          }
        </div>
      </section>
    } @empty {
      @if (loaded()) { <div class="a-card a-card-b text-center text-muted">Chưa có bàn nào. Quản lý thêm bàn ở <a class="text-primary" routerLink="/floor">Sơ đồ bàn</a>.</div> }
    }

    @if (counterForm()) {
      <div class="a-drawer" role="dialog" aria-modal="true">
        <form class="a-drawer-panel" (ngSubmit)="openCounter()">
          <div class="a-drawer-h">Đơn mang về tại quầy</div>
          <div class="a-drawer-b">
            <label>Tên gọi khách khi xong món (không bắt buộc)<input class="ot-input" name="counterName" maxlength="50" placeholder="Ví dụ: Chị Lan, số 12" [(ngModel)]="counterName" /></label>
            <label>Ghi chú (không bắt buộc)<input class="ot-input" name="counterNote" maxlength="500" [(ngModel)]="note" /></label>
            @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
          </div>
          <div class="a-drawer-f">
            <button type="button" class="ot-btn ot-btn-outline" (click)="counterForm.set(false)">Đóng</button>
            <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Tạo đơn</button>
          </div>
        </form>
      </div>
    }

    @if (opening(); as t) {
      <div class="a-drawer" role="dialog" aria-modal="true">
        <form class="a-drawer-panel" (ngSubmit)="open(t)">
          <div class="a-drawer-h">Mở bàn {{ t.tableName }}</div>
          <div class="a-drawer-b">
            <label>Số khách<input class="ot-input" name="guests" type="number" min="1" max="50" required [(ngModel)]="guests" /></label>
            <label>Ghi chú (không bắt buộc)<input class="ot-input" name="note" maxlength="500" [(ngModel)]="note" /></label>
            @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
          </div>
          <div class="a-drawer-f">
            <button type="button" class="ot-btn ot-btn-outline" (click)="opening.set(null)">Đóng</button>
            <button class="ot-btn ot-btn-primary" [disabled]="form.busy()">Mở bàn</button>
          </div>
        </form>
      </div>
    }
  `,
})
export class DineIn {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected readonly tables = signal<TableCard[]>([]);
  protected readonly counter = signal<Schemas['CounterBillDto'][]>([]);
  protected readonly counterForm = signal(false);
  protected counterName = '';
  protected readonly loaded = signal(false);
  protected readonly opening = signal<TableCard | null>(null);
  protected readonly form = new Submit();
  protected readonly loader = new Latest();
  protected guests = 2;
  protected note = '';
  protected readonly groups = computed(() => {
    const byArea = new Map<string, TableCard[]>();
    for (const t of this.tables()) {
      byArea.set(t.areaName, [...(byArea.get(t.areaName) ?? []), t]);
    }

    return [...byArea].map(([name, tables]) => ({ name, tables }));
  });

  constructor() {
    this.load();
    reloadOnLive(() => this.load());
  }

  protected load(): void {
    this.loader.run(this.api.get<TableCard[]>('dine-in/tables'), (t) => {
      this.tables.set(t);
      this.loaded.set(true);
    });
    this.api.get<Schemas['CounterBillDto'][]>('dine-in/counter').subscribe({ next: (c) => this.counter.set(c), error: () => undefined });
  }

  protected label(status: string): string {
    return TABLE_STATUS[status] ?? status;
  }

  protected since(utc: string | null): number {
    return minutesSince(utc);
  }

  protected openBill(id: string): void {
    void this.router.navigate(['/dine-in', id]);
  }

  protected startCounter(): void {
    this.form.error.set(null);
    this.counterName = '';
    this.note = '';
    this.counterForm.set(true);
  }

  protected openCounter(): void {
    this.form.run(this.api.post<Bill>('dine-in/counter', { customerName: this.counterName.trim() || null, note: this.note.trim() || null }), (bill) => {
      this.counterForm.set(false);
      this.openBill(bill.id);
    });
  }

  protected async pick(t: TableCard): Promise<void> {
    this.form.error.set(null);
    if (t.billId) {
      void this.router.navigate(['/dine-in', t.billId]);
      return;
    }

    if (t.status === 'cleaning') {
      if (!(await askConfirm(`Bàn ${t.tableName} đang dọn. Đánh dấu đã dọn xong và mở bàn?`))) {
        return;
      }

      this.form.run(this.api.post(`dine-in/tables/${t.tableId}/clean`), () => this.load());
    }

    this.guests = Math.min(2, t.maxCapacity);
    this.note = '';
    this.opening.set(t);
  }

  protected open(t: TableCard): void {
    this.form.run(this.api.post<Bill>('dine-in', { tableId: t.tableId, guestCount: Number(this.guests), note: this.note || null }), (bill) => {
      this.opening.set(null);
      void this.router.navigate(['/dine-in', bill.id]);
    });
  }
}

interface DraftLine {
  key: number;
  menuItemId: number;
  name: string;
  unitPrice: number;
  quantity: number;
  optionIds: number[];
  options: string[];
  note: string;
}

// Phiếu bàn: chọn món (bên trái) thành một đợt, gửi bếp; xem món theo đợt, đánh dấu đã bưng ra, hủy món (quản lý),
// chuyển/gộp bàn, in tạm tính, thanh toán (mọi nhân viên phục vụ bàn)
@Component({
  selector: 'ot-dine-in-bill',
  imports: [FormsModule, RouterLink, VndPipe, InvoiceForm],
  template: `
    <div class="a-head">
      <h1>@if (bill(); as b) { {{ b.tableName }} <small class="font-normal text-muted">@if (b.guestCount) { · {{ b.guestCount }} khách }· mở lúc {{ time(b.openedAtUtc) }}</small> } @else { Phiếu bàn }</h1>
      <div class="a-btns a-desk-only"><a class="ot-btn ot-btn-outline" routerLink="/dine-in">‹ Sơ đồ bàn</a></div>
    </div>
    @if (form.error() || loader.error(); as e) { <p class="ot-error mb-3" role="alert">{{ e }}</p> }

    @if (bill(); as b) {
      <div class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_420px]">
        <section class="a-card">
          <header class="a-card-h">Chọn món</header>
          @if (b.paid) { <p class="a-card-b font-semibold">Đơn đã thanh toán, đang chờ bếp ra món. Không gọi thêm vào đơn này; khách mua thêm thì tạo đơn mới.</p> } @else {
          <div class="a-card-b grid gap-3">
            <input class="ot-input" name="q" placeholder="Tìm món" [ngModel]="query()" (ngModelChange)="query.set($event)" />
            <!-- Danh mục dính khi cuộn: hàng ngang trên điện thoại, cột trái từ máy tính bảng; bấm thì cuộn tới nhóm -->
            <div class="a-pos-menu">
              @if (!query().trim()) {
                <nav class="a-pos-cats" aria-label="Danh mục">
                  @for (g of groups(); track g.id) {
                    <button type="button" class="ot-chip" [id]="'pos-chip-' + g.id" [class.ot-chip-active]="activeCat() === g.id" (click)="jumpCat(g.id)">{{ g.name }}</button>
                  }
                </nav>
              }
              <div class="min-w-0">
                @if (query().trim()) {
                  <div class="grid grid-cols-2 gap-2 sm:grid-cols-3 2xl:grid-cols-4">
                    @for (m of shown(); track m.id) {
                      <button type="button" class="a-dine-item" [disabled]="m.status !== 'available'" (click)="choose(m)">
                        <span class="font-semibold">{{ m.name }}</span>
                        <span class="text-sm text-muted">{{ m.status === 'available' ? (m.price | vnd) : 'Hết món' }}</span>
                      </button>
                    } @empty { <p class="text-muted">Không có món phù hợp.</p> }
                  </div>
                } @else {
                  @for (g of groups(); track g.id) {
                    <section class="a-pos-section" [id]="'pos-cat-' + g.id">
                      <h3 class="mb-2 font-semibold">{{ g.name }}</h3>
                      <div class="grid grid-cols-2 gap-2 sm:grid-cols-3 2xl:grid-cols-4">
                        @for (m of g.items; track m.id) {
                          <button type="button" class="a-dine-item" [disabled]="m.status !== 'available'" (click)="choose(m)">
                            <span class="font-semibold">{{ m.name }}</span>
                            <span class="text-sm text-muted">{{ m.status === 'available' ? (m.price | vnd) : 'Hết món' }}</span>
                          </button>
                        }
                      </div>
                    </section>
                  } @empty { <p class="text-muted">Chưa có món.</p> }
                }
              </div>
            </div>

            @if (draft().length > 0) {
              <div class="a-pos-send">
                <button type="button" class="ot-btn ot-btn-outline" (click)="showDraft()">Xem {{ draftCount() }} món đang chọn</button>
                <button type="button" class="ot-btn ot-btn-primary" [disabled]="form.busy()" (click)="send(b)">Gửi bếp</button>
              </div>
            }

            @if (draft().length > 0) {
              <div id="pos-draft" class="rounded-md border border-line p-3">
                <h2 class="mb-2 font-semibold">Món đang chọn (chưa gửi bếp)</h2>
                @for (d of draft(); track d.key) {
                  <div class="flex flex-wrap items-center gap-2 border-b border-line py-2 last:border-0">
                    <div class="min-w-0 flex-1"><b>{{ d.name }}</b>@for (o of d.options; track $index) { <small class="block text-muted">{{ o }}</small> }</div>
                    <div class="flex items-center gap-1">
                      <button type="button" class="ot-chip" aria-label="Bớt" (click)="bump(d, -1)">−</button>
                      <span class="w-6 text-center">{{ d.quantity }}</span>
                      <button type="button" class="ot-chip" aria-label="Thêm" (click)="bump(d, 1)">+</button>
                    </div>
                    <span class="w-24 text-right">{{ d.unitPrice * d.quantity | vnd }}</span>
                    <input class="ot-input basis-full" [name]="'note' + d.key" maxlength="200" placeholder="Ghi chú cho bếp, ví dụ: ít cay" [(ngModel)]="d.note" />
                  </div>
                }
                <button type="button" class="ot-btn ot-btn-primary mt-3 w-full" [disabled]="form.busy()" (click)="send(b)">Gửi bếp ({{ draftCount() }} món)</button>
              </div>
            }
          </div>
          }
        </section>

        <section class="a-card self-start">
          <header class="a-card-h"><span>Phiếu {{ b.code }}</span><span>{{ b.subTotal | vnd }}</span></header>
          <div class="a-card-b">
            @if (b.note) { <p class="mb-2 text-sm text-muted">Ghi chú: {{ b.note }}</p> }
            @for (r of rounds(); track r.round) {
              <h3 class="mt-3 text-sm font-semibold text-muted">Đợt {{ r.round }} · {{ time(r.sentAtUtc) }}</h3>
              @for (i of r.items; track i.id) {
                <div class="border-b border-line py-2 last:border-0" [class.opacity-60]="i.voided">
                  <div class="flex justify-between gap-2">
                    <span [class.line-through]="i.voided"><b>{{ i.quantity }} ×</b> {{ i.name }}</span>
                    <span>{{ i.lineTotal | vnd }}</span>
                  </div>
                  @for (o of i.options; track $index) { <small class="block text-muted">{{ o }}</small> }
                  @if (i.note) { <small class="block">* {{ i.note }}</small> }
                  <div class="mt-1 flex flex-wrap items-center gap-2 text-sm">
                    @if (i.voided) { <span class="text-danger">Đã hủy: {{ i.voidReason }}</span> }
                    @else if (i.servedAtUtc) { <span class="a-badge">Đã ra món</span> }
                    @else {
                      <span class="a-badge">Chờ bếp</span>
                      <button type="button" class="text-primary" (click)="served(b, i)">Đã bưng ra</button>
                    }
                    @if (!i.voided && canVoid() && !b.paid) { <button type="button" class="text-danger" (click)="voidItem(b, i)">Hủy món</button> }
                  </div>
                </div>
              }
            } @empty { <p class="text-muted">Chưa gọi món nào.</p> }

            <div class="mt-4 flex justify-between text-lg font-semibold"><span>Tạm tính</span><span>{{ b.subTotal | vnd }}</span></div>
            <div class="mt-3 grid grid-cols-2 gap-2">
              <button type="button" class="ot-btn ot-btn-outline" [disabled]="activeCount() === 0" (click)="printBill(b)">In tạm tính</button>
              @if (b.channel === 'dine_in') {
                <button type="button" class="ot-btn ot-btn-outline" (click)="startMove()">Chuyển bàn</button>
                <button type="button" class="ot-btn ot-btn-outline" (click)="startMerge()">Gộp bàn</button>
              }
              @if (activeCount() === 0) { <button type="button" class="ot-btn ot-btn-outline text-danger" (click)="cancelBill(b)">Hủy phiếu</button> }
            </div>
            @if (canPay() && !b.paid) { <button type="button" class="ot-btn ot-btn-primary mt-2 w-full" [disabled]="activeCount() === 0" (click)="startCheckout(b)">Thanh toán</button> }
          </div>
        </section>
      </div>
    }

    @if (picking(); as p) {
      <div class="a-drawer" role="dialog" aria-modal="true">
        <form class="a-drawer-panel" (ngSubmit)="addPicked(p)">
          <div class="a-drawer-h">{{ p.item.name }}</div>
          <div class="a-drawer-b">
            @for (g of p.groups; track g.id) {
              <fieldset>
                <legend>{{ g.name }} <small class="font-normal text-muted">{{ g.isRequired ? 'bắt buộc' : 'không bắt buộc' }}{{ g.selectionType === 'many' ? ', tối đa ' + g.maxSelect : '' }}</small></legend>
                @for (o of g.options; track o.id) {
                  <label class="flex items-center gap-2 font-normal">
                    <input [type]="g.selectionType === 'one' ? 'radio' : 'checkbox'" [name]="'g' + g.id" [disabled]="o.status !== 'available'" [checked]="p.selected.has(o.id)" (change)="toggle(p, g, o.id)" />
                    {{ o.name }}@if (o.priceDelta) { <small class="text-muted">+{{ o.priceDelta | vnd }}</small> }@if (o.status !== 'available') { <small class="text-muted">(hết)</small> }
                  </label>
                }
              </fieldset>
            }
            <label>Số lượng<input class="ot-input" name="qty" type="number" min="1" max="99" [(ngModel)]="p.quantity" /></label>
            @if (pickError(); as e) { <p class="ot-error">{{ e }}</p> }
          </div>
          <div class="a-drawer-f">
            <button type="button" class="ot-btn ot-btn-outline" (click)="picking.set(null)">Đóng</button>
            <button class="ot-btn ot-btn-primary">Thêm</button>
          </div>
        </form>
      </div>
    }

    @if (moving(); as mode) {
      <div class="a-drawer" role="dialog" aria-modal="true">
        <div class="a-drawer-panel">
          <div class="a-drawer-h">{{ mode === 'move' ? 'Chuyển sang bàn trống' : 'Gộp phiếu bàn khác vào bàn này' }}</div>
          <div class="a-drawer-b">
            @for (t of targets(); track t.tableId) {
              <button type="button" class="ot-btn ot-btn-outline justify-between" (click)="mode === 'move' ? move(t) : merge(t)">
                <span>{{ t.areaName }} · {{ t.tableName }}</span>@if (t.billId) { <span>{{ t.subTotal ?? 0 | vnd }}</span> }
              </button>
            } @empty { <p class="text-muted">{{ mode === 'move' ? 'Không còn bàn trống.' : 'Không có bàn nào khác đang có khách.' }}</p> }
            @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
          </div>
          <div class="a-drawer-f"><button type="button" class="ot-btn ot-btn-outline" (click)="moving.set(null)">Đóng</button></div>
        </div>
      </div>
    }

    @if (paying(); as b) {
      <div class="a-drawer" role="dialog" aria-modal="true">
        <form class="a-drawer-panel" (ngSubmit)="checkout(b)">
          <div class="a-drawer-h">Thanh toán {{ b.tableName }}</div>
          <div class="a-drawer-b">
            @if (receipt(); as r) {
              <p class="text-lg font-semibold">Đã thu {{ r.totalAmount | vnd }}</p>
              @if (r.memberName) { <p>Thành viên {{ r.memberName }} được cộng {{ r.pointsEarned }} điểm.</p> }
              <button type="button" class="ot-btn ot-btn-primary" (click)="printReceipt()">In phiếu thanh toán</button>
              <button type="button" class="ot-btn ot-btn-outline" (click)="done()">Về sơ đồ bàn</button>
            } @else {
              <label>SĐT thành viên (giảm theo hạng, tích điểm)<input class="ot-input" name="memberPhone" type="tel" [(ngModel)]="pay.memberPhone" (change)="quote(b)" /></label>
              <label>Mã giảm giá<input class="ot-input" name="voucher" [(ngModel)]="pay.voucherCode" (change)="quote(b)" /></label>
              <button type="button" class="ot-btn ot-btn-outline" [disabled]="quoter.busy()" (click)="quote(b)">Tính lại</button>
              @if (quoter.error(); as e) { <p class="ot-error">{{ e }}</p> }
              @if (quoted(); as q) {
                <div class="grid gap-1 rounded-md bg-surface-2 p-3">
                  @if (q.memberName) { <p>Thành viên: <b>{{ q.memberName }}</b></p> }
                  <p class="flex justify-between"><span>Tạm tính</span><span>{{ q.subTotal | vnd }}</span></p>
                  @if (q.memberDiscount) { <p class="flex justify-between"><span>Giảm theo hạng</span><span>−{{ q.memberDiscount | vnd }}</span></p> }
                  @if (q.voucherDiscount) { <p class="flex justify-between"><span>Mã giảm giá</span><span>−{{ q.voucherDiscount | vnd }}</span></p> }
                  <p class="flex justify-between text-lg font-semibold"><span>Cần thu</span><span>{{ q.totalAmount | vnd }}</span></p>
                </div>
              }
              <fieldset class="flex gap-4">
                <legend>Hình thức</legend>
                <label class="flex items-center gap-2 font-normal"><input type="radio" name="method" value="cash" [(ngModel)]="pay.paymentMethod" /> Tiền mặt</label>
                <label class="flex items-center gap-2 font-normal"><input type="radio" name="method" value="bank_transfer" [(ngModel)]="pay.paymentMethod" /> Chuyển khoản</label>
              </fieldset>
              <label class="flex items-center gap-2 font-normal"><input type="checkbox" name="wantInvoice" [(ngModel)]="wantInvoice" /> Khách lấy hóa đơn</label>
              @if (wantInvoice) { <ot-invoice-form [model]="buyer" /> }
              @if (form.error(); as e) { <p class="ot-error">{{ e }}</p> }
            }
          </div>
          @if (!receipt()) {
            <div class="a-drawer-f">
              <button type="button" class="ot-btn ot-btn-outline" (click)="paying.set(null)">Đóng</button>
              <button class="ot-btn ot-btn-primary" [disabled]="form.busy() || !quoted()">Đã thu tiền</button>
            </div>
          }
        </form>
      </div>
    }

    @if (printing(); as p) {
      <div class="ot-ticket">
        <p><b>{{ shop().name }}</b></p>
        @if (shop().address) { <p>{{ shop().address }}</p> }
        @if (shop().phone) { <p>ĐT: {{ shop().phone }}</p> }
        <hr />
        <p><b>{{ p.title }}</b></p>
        <p>{{ p.tableName }}@if (p.guestCount) { · {{ p.guestCount }} khách } · {{ p.code }}</p>
        <p>{{ p.at }}</p>
        <hr />
        @for (i of p.items; track i.id) {
          <p>{{ i.quantity }} × {{ i.name }} <span style="float: right">{{ i.lineTotal | vnd }}</span></p>
          @for (o of i.options; track $index) { <p>- {{ o }}</p> }
        }
        <hr />
        <p>Tạm tính <span style="float: right">{{ p.subTotal | vnd }}</span></p>
        @if (p.memberDiscount) { <p>Giảm theo hạng <span style="float: right">−{{ p.memberDiscount | vnd }}</span></p> }
        @if (p.voucherDiscount) { <p>Mã giảm giá <span style="float: right">−{{ p.voucherDiscount | vnd }}</span></p> }
        <p><b>Tổng cộng <span style="float: right">{{ p.totalAmount | vnd }}</span></b></p>
        @if (p.paymentLabel) { <p>{{ p.paymentLabel }}</p> }
        <hr />
        <p>Phiếu này không phải hóa đơn GTGT. Cảm ơn quý khách.</p>
      </div>
    }
  `,
})
export class DineInBill {
  private readonly api = inject(Api);
  private readonly injector = inject(Injector);
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  private readonly details = new Map<string, OptionGroup[]>();
  private nextKey = 1;
  protected readonly bill = signal<Bill | null>(null);
  protected readonly menu = signal<MenuItem[]>([]);
  protected readonly categories = signal<Schemas['PublicCategoryDto'][]>([]);
  protected readonly activeCat = signal<number | null>(null);
  protected readonly query = signal('');
  private spyFrame = 0;
  private jumpingUntil = 0;
  protected readonly draft = signal<DraftLine[]>([]);
  protected readonly picking = signal<Picking | null>(null);
  protected readonly pickError = signal<string | null>(null);
  protected readonly moving = signal<'move' | 'merge' | null>(null);
  protected readonly targets = signal<TableCard[]>([]);
  protected readonly paying = signal<Bill | null>(null);
  protected readonly quoted = signal<Schemas['DineInQuoteDto'] | null>(null);
  protected readonly receipt = signal<Receipt | null>(null);
  protected readonly printing = signal<(Slip & { at: string }) | null>(null);
  protected readonly form = new Submit();
  protected readonly quoter = new Submit();
  protected readonly loader = new Latest();
  protected pay = { memberPhone: '', voucherCode: '', paymentMethod: 'cash' };
  protected wantInvoice = false;
  protected buyer = emptyBuyer();
  protected readonly canVoid = computed(() => this.auth.has('orders_void'));
  protected readonly canPay = computed(() => this.auth.has('dine_in'));
  protected readonly shop = toSignal(
    this.api.publicSettings().pipe(
      map((s) => ({ name: s['restaurant.name'] || 'Obento Tensho', address: s['restaurant.address'] || '', phone: s['restaurant.phone'] || '' })),
      catchError(() => of({ name: 'Obento Tensho', address: '', phone: '' })),
    ),
    { initialValue: { name: 'Obento Tensho', address: '', phone: '' } },
  );
  protected readonly shown = computed(() => {
    const q = this.query().trim().toLowerCase();
    return this.menu().filter((m) => !q || m.name.toLowerCase().includes(q));
  });
  // Món chia nhóm theo thứ tự danh mục của thực đơn
  protected readonly groups = computed(() =>
    this.categories()
      .map((c) => ({ id: c.id, name: c.name, items: this.menu().filter((m) => m.categoryId === c.id) }))
      .filter((g) => g.items.length > 0),
  );
  protected readonly draftCount = computed(() => this.draft().reduce((n, d) => n + d.quantity, 0));
  protected readonly activeCount = computed(() => (this.bill()?.items ?? []).filter((i) => !i.voided).length);
  protected readonly rounds = computed(() => {
    const byRound = new Map<number, Line[]>();
    for (const i of this.bill()?.items ?? []) {
      byRound.set(i.round, [...(byRound.get(i.round) ?? []), i]);
    }

    return [...byRound].map(([round, items]) => ({ round, sentAtUtc: items[0].sentAtUtc, items }));
  });

  constructor() {
    this.load();
    reloadOnLive(() => this.load());
    this.api.publicGet<Schemas['PublicCategoryDto'][]>('menu/categories').subscribe({ next: (c) => this.categories.set(c), error: () => undefined });
    this.api.publicGet<MenuItem[]>('menu/items', { pageSize: 100 }).subscribe({ next: (m) => this.menu.set(m), error: () => this.form.error.set('Không tải được thực đơn.') });
  }

  protected load(): void {
    this.loader.run(this.api.get<Bill>(`dine-in/${this.id}`), (b) => this.bill.set(b));
  }

  protected jumpCat(id: number): void {
    this.activeCat.set(id);
    this.jumpingUntil = Date.now() + 800;
    document.getElementById(`pos-chip-${id}`)?.scrollIntoView({ inline: 'center', block: 'nearest' });
    document.getElementById(`pos-cat-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  protected showDraft(): void {
    document.getElementById('pos-draft')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // Nhóm đang xem = nhóm cuối có tiêu đề đã chạm mép dưới thanh danh mục dính
  @HostListener('window:scroll')
  protected onScroll(): void {
    if (this.spyFrame || Date.now() < this.jumpingUntil || this.query().trim()) {
      return;
    }

    this.spyFrame = requestAnimationFrame(() => {
      this.spyFrame = 0;
      const nav = document.querySelector('.a-pos-cats');
      const edge = (window.innerWidth < 768 && nav ? nav.getBoundingClientRect().bottom : 86) + 8;
      let current: number | null = null;
      for (const g of this.groups()) {
        if ((document.getElementById(`pos-cat-${g.id}`)?.getBoundingClientRect().top ?? Infinity) <= edge) {
          current = g.id;
        }
      }

      const next = current ?? this.groups()[0]?.id ?? null;
      if (next !== this.activeCat()) {
        this.activeCat.set(next);
        document.getElementById(`pos-chip-${next}`)?.scrollIntoView({ inline: 'center', block: 'nearest' });
      }
    });
  }

  protected time(utc: string): string {
    return clock(utc);
  }

  // Món không có tùy chọn thì thêm luôn; có thì mở bảng chọn
  protected choose(m: MenuItem): void {
    const cached = this.details.get(m.slug);
    if (cached) {
      this.pick(m, cached);
      return;
    }

    this.form.run(this.api.publicGet<Schemas['PublicMenuItemDetailDto']>(`menu/items/${m.slug}`).pipe(map((d) => d.optionGroups)), (groups) => {
      this.details.set(m.slug, groups);
      this.pick(m, groups);
    });
  }

  private pick(m: MenuItem, groups: OptionGroup[]): void {
    if (groups.length === 0) {
      this.addDraft(m, [], [], 1);
      return;
    }

    const selected = new Set<number>(groups.flatMap((g) => g.options.filter((o) => o.isDefault && o.status === 'available').map((o) => o.id)));
    this.pickError.set(null);
    this.picking.set({ item: m, groups, selected, quantity: 1 });
  }

  protected toggle(p: Picking, g: OptionGroup, optionId: number): void {
    if (g.selectionType === 'one') {
      for (const o of g.options) {
        p.selected.delete(o.id);
      }

      p.selected.add(optionId);
    } else if (p.selected.has(optionId)) {
      p.selected.delete(optionId);
    } else {
      p.selected.add(optionId);
    }
  }

  protected addPicked(p: Picking): void {
    for (const g of p.groups) {
      const count = g.options.filter((o) => p.selected.has(o.id)).length;
      if (count < g.minSelect || count > g.maxSelect) {
        this.pickError.set(`Nhóm "${g.name}" cần chọn ${g.minSelect === g.maxSelect ? g.minSelect : `${g.minSelect}–${g.maxSelect}`} lựa chọn.`);
        return;
      }
    }

    const chosen = p.groups.flatMap((g) => g.options.filter((o) => p.selected.has(o.id)).map((o) => ({ g, o })));
    this.addDraft(
      p.item,
      chosen.map((x) => x.o.id),
      chosen.map((x) => `${x.g.name}: ${x.o.name}`),
      Math.min(99, Math.max(1, Number(p.quantity) || 1)),
      chosen.reduce((sum, x) => sum + x.o.priceDelta, 0),
    );
    this.picking.set(null);
  }

  private addDraft(m: MenuItem, optionIds: number[], options: string[], quantity: number, optionsPrice = 0): void {
    const same = this.draft().find((d) => d.menuItemId === m.id && d.optionIds.join() === optionIds.join() && !d.note);
    if (same) {
      this.bump(same, quantity);
      return;
    }

    this.draft.update((d) => [...d, { key: this.nextKey++, menuItemId: m.id, name: m.name, unitPrice: m.price + optionsPrice, quantity, optionIds, options, note: '' }]);
  }

  protected bump(d: DraftLine, delta: number): void {
    this.draft.update((all) => all.map((x) => (x.key === d.key ? { ...x, quantity: Math.min(99, x.quantity + delta) } : x)).filter((x) => x.quantity > 0));
  }

  protected send(b: Bill): void {
    const items = this.draft().map((d) => ({ menuItemId: d.menuItemId, quantity: d.quantity, optionIds: d.optionIds, note: d.note.trim() || null }));
    this.form.run(this.api.post<Bill>(`dine-in/${b.id}/items`, { items }), (bill) => {
      this.bill.set(bill);
      this.draft.set([]);
    });
  }

  protected served(b: Bill, i: Line): void {
    this.form.run(this.api.post<Bill>(`dine-in/${b.id}/items/${i.id}/served-at-table`), (bill) => this.bill.set(bill));
  }

  protected async voidItem(b: Bill, i: Line): Promise<void> {
    const reason = await askText(`Lý do hủy "${i.quantity} × ${i.name}"?`);
    if (reason?.trim()) {
      this.form.run(this.api.post<Bill>(`dine-in/${b.id}/items/${i.id}/void`, { reason }), (bill) => this.bill.set(bill));
    }
  }

  protected async cancelBill(b: Bill): Promise<void> {
    const reason = await askText(`Hủy phiếu ${b.tableName}? Nhập lý do:`);
    if (reason?.trim()) {
      this.form.run(this.api.post(`dine-in/${b.id}/cancel`, { reason }), () => void this.router.navigateByUrl('/dine-in'));
    }
  }

  protected startMove(): void {
    this.openTargets('move', (t) => !t.billId && t.status !== 'cleaning');
  }

  protected startMerge(): void {
    this.openTargets('merge', (t) => !!t.billId && t.billId !== this.id);
  }

  private openTargets(mode: 'move' | 'merge', keep: (t: TableCard) => boolean): void {
    this.form.error.set(null);
    this.form.run(this.api.get<TableCard[]>('dine-in/tables'), (all) => {
      this.targets.set(all.filter(keep));
      this.moving.set(mode);
    });
  }

  protected move(t: TableCard): void {
    this.form.run(this.api.post<Bill>(`dine-in/${this.id}/move`, { tableId: t.tableId }), (bill) => {
      this.bill.set(bill);
      this.moving.set(null);
    });
  }

  protected async merge(t: TableCard): Promise<void> {
    if (await askConfirm(`Gộp ${t.tableName} vào ${this.bill()?.tableName}? Phiếu ${t.tableName} sẽ đóng.`)) {
      this.form.run(this.api.post<Bill>(`dine-in/${this.id}/merge`, { fromBillId: t.billId }), (bill) => {
        this.bill.set(bill);
        this.moving.set(null);
      });
    }
  }

  protected startCheckout(b: Bill): void {
    this.form.error.set(null);
    this.pay = { memberPhone: '', voucherCode: '', paymentMethod: 'cash' };
    this.wantInvoice = false;
    this.buyer = emptyBuyer();
    this.receipt.set(null);
    this.quoted.set(null);
    this.paying.set(b);
    this.quote(b);
  }

  protected quote(b: Bill): void {
    this.quoted.set(null);
    this.quoter.run(this.api.post<Schemas['DineInQuoteDto']>(`dine-in/${b.id}/quote`, this.payBody()), (q) => this.quoted.set(q));
  }

  protected checkout(b: Bill): void {
    this.form.run(this.api.post<Receipt>(`dine-in/${b.id}/checkout`, { ...this.payBody(), paymentMethod: this.pay.paymentMethod, invoice: this.wantInvoice ? buyerBody(this.buyer) : null }), (r) => {
      this.receipt.set(r);
      this.printReceipt();
    });
  }

  private payBody(): { memberPhone: string | null; voucherCode: string | null } {
    return { memberPhone: this.pay.memberPhone.trim() || null, voucherCode: this.pay.voucherCode.trim() || null };
  }

  protected done(): void {
    this.paying.set(null);
    void this.router.navigateByUrl('/dine-in');
  }

  protected printBill(b: Bill): void {
    const items = b.items.filter((i) => !i.voided);
    this.print({ title: 'PHIẾU TẠM TÍNH', tableName: b.tableName, guestCount: b.guestCount, code: b.code, items, subTotal: b.subTotal, memberDiscount: 0, voucherDiscount: 0, totalAmount: b.subTotal, paymentLabel: '' });
  }

  protected printReceipt(): void {
    const r = this.receipt();
    if (r) {
      const items = r.items.filter((i) => !i.voided);
      this.print({ ...r, title: 'PHIẾU THANH TOÁN', items, paymentLabel: r.paymentMethod === 'cash' ? 'Đã thu tiền mặt' : 'Đã thu chuyển khoản' });
    }
  }

  private print(p: Slip): void {
    this.printing.set({ ...p, at: new Date().toLocaleString('vi-VN') });
    // In sau khi phiếu đã vẽ ra trang, không in ra trang trắng
    afterNextRender(() => window.print(), { injector: this.injector });
  }
}
