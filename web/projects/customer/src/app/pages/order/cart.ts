import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Schemas } from '@ot/api';
import { catchError, EMPTY, from, map, of, switchMap } from 'rxjs';
import { LucideAngularModule, Minus, Plus } from 'lucide-angular';

import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { Cart as CartStore, NOTE_MAX_LENGTH } from '../../core/cart';
import { askConfirm } from '../../core/dialog';
import { VndPipe } from '../../shared/vnd.pipe';

const MENU_PAGE = 100;

export function linesOf(cart: CartStore) {
  return cart.lines().map((l) => ({ menuItemId: l.menuItemId, quantity: l.quantity, optionIds: l.optionIds, note: l.note || null }));
}

@Component({
  selector: 'ot-cart',
  imports: [FormsModule, RouterLink, VndPipe, LucideAngularModule],
  template: `
    <div class="ot-page-split">
      <h1>Giỏ hàng</h1>
      @if (cart.lines().length === 0) {
        <div class="ot-panel mt-5">
          <p>Giỏ hàng đang trống.</p>
          <a class="ot-btn ot-btn-primary mt-3" routerLink="/menu">Xem thực đơn</a>
        </div>
      } @else if (quote(); as q) {
        <div class="ot-split">
          <section class="ot-panel">
            <h2>Món đã chọn</h2>
            <div class="mt-4">
            <ul class="grid gap-3">
              @for (row of q.lines; track row.index) {
                <li class="ot-card flex gap-3 p-3 md:gap-4">
                  @let line = lineOf(row);
                  @let info = menu().get(line?.menuItemId ?? 0);
                  <a class="ot-frame block size-20 shrink-0 overflow-hidden rounded-md border-0 md:size-24" [routerLink]="info ? '/menu/' + info.slug : '/menu'" [attr.aria-label]="line?.name ?? 'Thực đơn'">
                    @if (info?.image; as img) {
                      <img class="size-full object-cover" [src]="img.variants[0]?.url" [alt]="line?.name ?? ''" width="96" height="96" loading="lazy" decoding="async" />
                    }
                  </a>
                  <div class="min-w-0 flex-1">
                    <p class="font-medium">{{ line?.name ?? 'Món không còn phục vụ' }}</p>
                    @for (o of line?.options ?? []; track $index) { <p class="text-sm text-muted">{{ o.groupName }}: {{ o.optionName }}</p> }
                    @let note = cart.lines()[row.index]?.note;
                    @if (editing() === row.index) {
                      <form class="mt-2 flex flex-wrap items-center gap-2" (ngSubmit)="saveNote(row.index)">
                        <input class="ot-input min-w-0 flex-1" name="note" [maxlength]="noteMax" placeholder="Ví dụ: ít cay, không hành" aria-label="Ghi chú cho món" [(ngModel)]="draft" />
                        <button class="ot-btn ot-btn-primary min-h-11 px-4">Lưu</button>
                        <button type="button" class="px-2 font-medium text-muted" (click)="editing.set(null)">Hủy</button>
                      </form>
                    } @else if (note) {
                      <p class="mt-1 text-sm"><span class="text-muted">Ghi chú:</span> {{ note }} <button type="button" class="ml-1 font-medium text-primary" (click)="editNote(row.index)">Sửa</button></p>
                    } @else {
                      <button type="button" class="mt-1 -my-2 inline-flex items-center gap-1 py-2 text-sm font-medium text-primary" (click)="editNote(row.index)"><lucide-icon [img]="icons.plus" [size]="16" />Thêm ghi chú</button>
                    }
                    @if (row.errorCode) { <p class="ot-error mt-1">Món hoặc tùy chọn này hiện không đặt được, vui lòng xóa.</p> }
                    <div class="mt-2 flex flex-wrap items-center justify-between gap-3">
                      <div class="flex items-center gap-1">
                        <button class="ot-chip" type="button" aria-label="Giảm" (click)="change(row.index, -1, line?.name)"><lucide-icon [img]="icons.minus" [size]="18" /></button>
                        <span class="w-8 text-center">{{ cart.lines()[row.index]?.quantity }}</span>
                        <button class="ot-chip" type="button" aria-label="Tăng" (click)="change(row.index, 1)"><lucide-icon [img]="icons.plus" [size]="18" /></button>
                      </div>
                      @if (line; as l) { <b>{{ l.lineTotal | vnd }}</b> }
                      <button class="text-danger max-md:hidden" type="button" (click)="cart.remove(row.index)">Xóa</button>
                    </div>
                  </div>
                </li>
              }
            </ul>
            </div>
          </section>
          <aside class="ot-panel">
            <h2>Tóm tắt</h2>
            <div class="mt-3">
            <p class="ot-sum"><span>Tạm tính</span><span>{{ q.subTotal | vnd }}</span></p>
            @if (q.memberDiscount > 0) { <p class="ot-sum"><span>Giảm hạng thành viên</span><span>-{{ q.memberDiscount | vnd }}</span></p> }
            <p class="ot-sum border-t border-line pt-3 text-lg font-semibold"><span>Tổng</span><span class="text-primary">{{ q.totalAmount | vnd }}</span></p>
            @if (q.isValid) {
              <a class="ot-btn ot-btn-primary mt-4 w-full" routerLink="/thanh-toan">{{ auth.profile() ? 'Đặt món' : 'Đăng nhập để đặt món' }}</a>
              @if (auth.ready() && !auth.profile()) {
                <p class="mt-2 text-center text-sm text-muted">Giỏ hàng được giữ nguyên sau khi bạn đăng nhập.</p>
              }
            }
            </div>
            <a class="ot-btn ot-btn-outline mt-2 w-full" routerLink="/menu">Chọn thêm món</a>
          </aside>
        </div>
      } @else if (failed()) {
        <p class="ot-error mt-3">Chưa kiểm tra được giá, vui lòng tải lại trang. Nếu vẫn lỗi, hãy xóa giỏ và chọn món lại.</p>
        <button type="button" class="ot-btn ot-btn-outline mt-3" (click)="cart.clear()">Xóa giỏ hàng</button>
      } @else {
        <p class="mt-3">Đang kiểm tra giá…</p>
      }
    </div>
  `,
})
export class CartPage {
  protected readonly icons = { minus: Minus, plus: Plus };
  private readonly api = inject(Api);
  protected readonly auth = inject(Auth);
  protected readonly cart = inject(CartStore);
  // Báo giá không kèm ảnh: lấy ảnh và đường dẫn từ danh sách món công khai, món không có trong đó thì để khung trống
  protected readonly menu = toSignal(
    this.api.getOr<Schemas['PublicMenuItemDto'][]>('menu/items', [], { pageSize: MENU_PAGE }).pipe(map((items) => new Map(items.map((i) => [i.id, i])))),
    { initialValue: new Map<number, Schemas['PublicMenuItemDto']>() },
  );
  protected readonly quote = signal<Schemas['OrderQuoteDto'] | null>(null);
  protected readonly failed = signal(false);
  protected readonly editing = signal<number | null>(null);
  protected readonly noteMax = NOTE_MAX_LENGTH;
  protected draft = '';
  protected readonly items = computed(() => linesOf(this.cart));

  constructor() {
    toObservable(this.items)
      .pipe(
        switchMap((items) =>
          items.length === 0
            ? of(null)
            : from(this.auth.init()).pipe(
                switchMap(() => this.api.post<Schemas['OrderQuoteDto']>('orders/quote', { items, lang: 'vi' })),
                catchError(() => {
                  this.failed.set(true);
                  return EMPTY;
                }),
              ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((q) => {
        this.quote.set(q);
        this.failed.set(false);
      });
  }

  // API trả line = null với món lỗi, dù kiểu sinh từ OpenAPI chưa ghi nhận
  protected lineOf(row: Schemas['QuoteLineDto']): Schemas['OrderLineDto'] | null {
    return row.line ?? null;
  }

  protected editNote(index: number): void {
    this.draft = this.cart.lines()[index]?.note ?? '';
    this.editing.set(index);
  }

  protected saveNote(index: number): void {
    this.cart.setNote(index, this.draft);
    this.editing.set(null);
  }

  // Trên điện thoại không có nút Xóa riêng: giảm từ 1 xuống 0 thì hỏi xác nhận rồi xóa món
  protected async change(index: number, delta: number, name?: string): Promise<void> {
    const quantity = (this.cart.lines()[index]?.quantity ?? 1) + delta;
    if (quantity >= 1) {
      this.cart.setQuantity(index, quantity);
    } else if (await askConfirm(`Xóa ${name ?? 'món này'} khỏi giỏ hàng?`)) {
      this.cart.remove(index);
    }
  }
}
