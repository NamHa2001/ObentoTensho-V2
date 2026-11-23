import { Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Schemas } from '@ot/api';
import { Check, LucideAngularModule, Minus, Plus, X } from 'lucide-angular';

import { Api } from '../core/api';
import { Cart, MAX_QUANTITY, NOTE_MAX_LENGTH } from '../core/cart';
import { defaultSelection, isComplete, isOrderable, requiredCount, Selection, toggle, unitPrice } from './option-selection';
import { Modal } from './modal';
import { VndPipe } from './vnd.pipe';

type Detail = Schemas['PublicMenuItemDetailDto'];
type Group = Schemas['PublicOptionGroupDto'];
type Option = Schemas['PublicOptionDto'];

// Bảng chọn khi bấm thêm món, như các app đặt món: tùy chọn, ghi chú cho quán, số lượng.
// Dùng chung cho thẻ món (chưa có tùy chọn thì tự tải) và trang chi tiết (đã có sẵn)
@Component({
  selector: 'ot-add-to-cart',
  imports: [FormsModule, LucideAngularModule, VndPipe, Modal],
  template: `
    <div class="ot-sheet-back" role="dialog" aria-modal="true" [attr.aria-label]="'Thêm ' + name()" otModal (dismiss)="closed.emit()">
      <form class="ot-sheet" (ngSubmit)="add()">
        <div class="ot-sheet-head">
          <div class="flex min-w-0 items-center gap-3">
            @if (thumb(); as url) { <img class="size-14 shrink-0 rounded-md object-cover" [src]="url" alt="" width="56" height="56" /> }
            <div class="min-w-0"><p class="truncate font-heading text-lg font-semibold">{{ name() }}</p><p class="text-sm font-semibold text-primary">{{ price() | vnd }}</p></div>
          </div>
          <button type="button" class="flex size-11 shrink-0 items-center justify-center" aria-label="Đóng" (click)="closed.emit()"><lucide-icon [img]="icons.close" [size]="18" /></button>
        </div>
        <div class="ot-sheet-body">
          @if (state() === 'loading') {
            <p class="text-muted">Đang tải tùy chọn…</p>
          } @else if (state() === 'failed') {
            <p class="ot-error">Chưa tải được món này, vui lòng thử lại.</p>
          } @else if (detail(); as d) {
            @for (group of d.optionGroups; track group.id) {
              <fieldset>
                <legend class="flex w-full items-baseline justify-between gap-3">
                  <span class="font-heading text-lg font-semibold text-heading">{{ group.name }}</span>
                  <span class="text-sm whitespace-nowrap text-muted">{{ rule(group) }}</span>
                </legend>
                <div class="mt-2 grid grid-cols-1 gap-2">
                  @for (option of group.options; track option.id) {
                    <label class="ot-option" [class.ot-option-on]="selected(group, option)" [class.ot-option-off]="disabled(group, option)">
                      <input class="sr-only" [type]="group.selectionType === 'one' ? 'radio' : 'checkbox'" [name]="'group-' + group.id" [checked]="selected(group, option)" [disabled]="disabled(group, option)" (change)="choose(group, option)" />
                      <span class="ot-option-mark" [class.rounded-full]="group.selectionType === 'one'">
                        @if (selected(group, option)) { <lucide-icon [img]="icons.check" [size]="16" /> }
                      </span>
                      <span class="min-w-0 flex-1">{{ option.name }}</span>
                      <span class="text-sm whitespace-nowrap text-muted tabular-nums">
                        @if (option.status !== 'available') { Hết } @else if (option.priceDelta > 0) { +{{ option.priceDelta | vnd }} }
                      </span>
                    </label>
                  }
                </div>
              </fieldset>
            }
            <label class="block">
              <span class="flex items-baseline justify-between gap-3"><span class="font-heading text-lg font-semibold text-heading">Ghi chú cho quán</span><span class="text-sm text-muted">Không bắt buộc</span></span>
              <textarea class="ot-input mt-2" name="note" rows="2" [maxlength]="noteMax" placeholder="Ví dụ: ít cay, không hành, sốt để riêng" [(ngModel)]="note"></textarea>
              <span class="mt-1 block text-right text-xs text-muted tabular-nums">{{ note.length }}/{{ noteMax }}</span>
            </label>
          }
        </div>
        <div class="flex items-center gap-3 border-t border-line px-5 py-3">
          <div class="flex h-12 shrink-0 items-center rounded-full border border-line bg-surface">
            <button type="button" class="flex size-11 items-center justify-center disabled:opacity-40" aria-label="Giảm số lượng" [disabled]="quantity() <= 1" (click)="changeQuantity(-1)"><lucide-icon [img]="icons.minus" [size]="18" /></button>
            <span class="w-7 text-center font-semibold tabular-nums" aria-live="polite">{{ quantity() }}</span>
            <button type="button" class="flex size-11 items-center justify-center disabled:opacity-40" aria-label="Tăng số lượng" [disabled]="quantity() >= maxQuantity" (click)="changeQuantity(1)"><lucide-icon [img]="icons.plus" [size]="18" /></button>
          </div>
          <button class="ot-btn ot-btn-primary min-w-0 flex-1 px-3 disabled:opacity-50" [disabled]="!canAdd()">
            @if (detail() && !complete()) { Chọn đủ tùy chọn } @else { <span class="whitespace-nowrap">Thêm vào giỏ · {{ total() | vnd }}</span> }
          </button>
        </div>
      </form>
    </div>
  `,
})
export class AddToCart implements OnInit {
  // Thẻ món chỉ có thông tin rút gọn; trang chi tiết truyền sẵn bản đầy đủ để khỏi tải lại
  readonly item = input.required<Schemas['PublicMenuItemDto']>();
  readonly preset = input<Detail | null>(null);
  readonly added = output<void>();
  readonly closed = output<void>();

  private readonly api = inject(Api);
  private readonly cart = inject(Cart);
  protected readonly icons = { check: Check, minus: Minus, plus: Plus, close: X };
  protected readonly maxQuantity = MAX_QUANTITY;
  protected readonly noteMax = NOTE_MAX_LENGTH;
  protected readonly detail = signal<Detail | null>(null);
  protected readonly state = signal<'loading' | 'ready' | 'failed'>('loading');
  protected readonly selection = signal<Selection>({});
  protected readonly quantity = signal(1);
  protected note = '';

  protected readonly name = computed(() => this.item().name);
  protected readonly price = computed(() => this.item().price);
  protected readonly thumb = computed(() => this.item().image?.variants[0]?.url ?? null);
  protected readonly unit = computed(() => {
    const d = this.detail();
    return d ? unitPrice(d.item.price, this.selection(), d.optionGroups) : this.item().price;
  });
  protected readonly total = computed(() => this.unit() * this.quantity());
  protected readonly complete = computed(() => isComplete(this.selection(), this.detail()?.optionGroups ?? []));
  protected readonly canAdd = computed(() => this.state() === 'ready' && this.complete());

  ngOnInit(): void {
    const preset = this.preset();
    if (preset) {
      this.ready(preset);
      return;
    }

    this.api.get<Detail>(`menu/items/${encodeURIComponent(this.item().slug)}`).subscribe({
      next: (d) => this.ready(d),
      error: () => this.state.set('failed'),
    });
  }

  protected selected(group: Group, option: Option): boolean {
    return this.selection()[group.id]?.includes(option.id) ?? false;
  }

  protected disabled(group: Group, option: Option): boolean {
    if (!isOrderable(option)) {
      return true;
    }

    return group.selectionType === 'many' && !this.selected(group, option) && (this.selection()[group.id]?.length ?? 0) >= group.maxSelect;
  }

  protected rule(group: Group): string {
    if (group.selectionType === 'one') {
      return group.isRequired ? 'Bắt buộc · chọn 1' : 'Không bắt buộc · chọn 1';
    }

    const min = requiredCount(group);
    return min > 0 ? `Bắt buộc · chọn ${min}–${group.maxSelect}` : `Không bắt buộc · tối đa ${group.maxSelect}`;
  }

  protected choose(group: Group, option: Option): void {
    this.selection.update((s) => toggle(s, group, option.id));
  }

  protected changeQuantity(delta: number): void {
    this.quantity.update((q) => Math.min(MAX_QUANTITY, Math.max(1, q + delta)));
  }

  protected add(): void {
    const d = this.detail();
    if (!d || !this.canAdd()) {
      return;
    }

    this.cart.add(d.item.id, Object.values(this.selection()).flat(), this.quantity(), this.note);
    this.added.emit();
  }

  private ready(d: Detail): void {
    this.detail.set(d);
    this.selection.set(defaultSelection(d.optionGroups));
    this.state.set('ready');
  }
}
