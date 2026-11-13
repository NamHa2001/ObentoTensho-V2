import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { Component, inject, signal } from '@angular/core';

import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { Live } from '../core/live';
import { reloadOnLive } from '../core/live';
import { Latest, ORDER_STATUS, Submit } from '../shared/ui';

@Component({
  selector: 'ot-kitchen',
  imports: [CdkDropListGroup, CdkDropList, CdkDrag],
  template: `
    <h1>Màn hình bếp</h1>
    @if (form.error() || loader.error(); as e) { <p class="ot-error mb-3">{{ e }}</p> }
    <div class="mt-2 flex flex-wrap gap-2">
      <button type="button" class="ot-chip" (click)="load()">Tải lại</button>
      <button type="button" class="ot-chip" [class.ot-chip-active]="live.sound()" (click)="live.toggleSound()">{{ live.sound() ? 'Đang bật âm báo' : 'Bật âm báo' }}</button>
    </div>
    @if (tickets().length > 0) {
      <section class="a-card mt-3 p-3">
        <h2 class="flex justify-between">Tại bàn <span class="a-badge">{{ tickets().length }}</span></h2>
        <div class="mt-2 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          @for (t of tickets(); track t.billId) {
            <article class="ot-card p-3">
              <b>{{ t.tableName }}</b> <small class="text-muted">· {{ t.code }}</small>
              <ul class="mt-1">
                @for (i of t.items; track i.id) {
                  <li class="flex items-start justify-between gap-2 border-b border-line py-1 last:border-0">
                    <span><b>{{ i.quantity }} × {{ i.name }}</b> <small class="text-muted">đợt {{ i.round }} · {{ waited(i.sentAtUtc) }} phút</small>@for (x of i.options; track $index) { <small class="block text-muted">{{ x }}</small> }@if (i.note) { <small class="block">{{ i.note }}</small> }</span>
                    <button type="button" class="ot-btn ot-btn-primary shrink-0" (click)="served(t, i)">Đã ra</button>
                  </li>
                }
              </ul>
            </article>
          }
        </div>
      </section>
    }
    <div class="mt-3 grid gap-4 md:grid-cols-3" cdkDropListGroup>
      @for (lane of lanes; track lane) {
        <section class="a-card min-h-60 p-3" cdkDropList [cdkDropListData]="lane" (cdkDropListDropped)="dropped($event)"><h2 class="flex justify-between">{{ labels[lane] }} <span class="a-badge">{{ of(lane).length }}</span></h2>
          @for (o of of(lane); track o.id) {
            <article class="ot-card mt-2 cursor-move p-3" cdkDrag [cdkDragData]="o"><b>{{ o.code }}</b> · {{ o.pickupTime.slice(0, 5) }} · {{ o.shortName }}
              <ul class="mt-1">@for (i of o.items; track $index) { <li>{{ i.quantity }} × {{ i.name }}@for (x of i.options; track $index) { <small class="block text-muted">{{ x }}</small> }@if (i.note) { <small class="block">{{ i.note }}</small> }</li> }</ul>
              @if (o.note) { <p class="text-sm">{{ o.note }}</p> }
              <div class="mt-2 flex gap-2">
                @if (nextFor(o.status); as n) { <button type="button" class="ot-btn ot-btn-primary flex-1" (click)="move(o, n)">{{ labels[n] }}</button> }
                <button type="button" class="ot-btn ot-btn-outline" (click)="print(o)">In</button>
              </div>
            </article>
          } @empty { <p class="mt-2 text-muted">Trống</p> }
        </section>
      }
    </div>
    @if (printing(); as o) {
      <div class="ot-ticket"><b>{{ o.code }}</b><p>Lấy {{ o.pickupTime.slice(0, 5) }} · {{ o.shortName }}</p><hr />
        @for (i of o.items; track $index) { <p><b>{{ i.quantity }} × {{ i.name }}</b></p>@for (x of i.options; track $index) { <p>- {{ x }}</p> }@if (i.note) { <p>* {{ i.note }}</p> } }
        @if (o.note) { <hr /><p>{{ o.note }}</p> }
      </div>
    }
  `,
})
export class Kitchen {
  private readonly api = inject(Api);
  protected readonly live = inject(Live);
  protected readonly labels = ORDER_STATUS;
  protected readonly lanes = ['pending', 'preparing', 'ready'];
  protected readonly nextOf: Record<string, string> = { pending: 'preparing', preparing: 'ready', ready: 'completed' };
  protected readonly orders = signal<any[]>([]);
  protected readonly tickets = signal<any[]>([]);
  protected readonly printing = signal<any>(null);
  protected readonly form = new Submit();
  protected readonly loader = new Latest();
  private readonly auth = inject(Auth);

  constructor() {
    this.load();
    reloadOnLive(() => this.load());
  }

  protected load(): void {
    this.loader.run(this.api.get<any[]>('orders/kitchen-view'), (o) => this.orders.set(o));
    this.api.get<any[]>('dine-in/kitchen-view').subscribe({ next: (t) => this.tickets.set(t), error: () => undefined });
  }

  protected served(t: any, i: any): void {
    this.form.run(this.api.post(`dine-in/${t.billId}/items/${i.id}/served`), () => this.load());
  }

  protected waited(utc: string): number {
    return Math.max(0, Math.round((Date.now() - new Date(utc).getTime()) / 60000));
  }

  // Bếp nấu xong tới "Sẵn sàng"; giao cho khách là việc của thu ngân
  protected nextFor(status: string): string | null {
    const next = this.nextOf[status];
    return next === 'completed' && !this.auth.has('orders_cashier') ? null : next;
  }

  protected of(status: string): any[] {
    return this.orders().filter((o) => o.status === status);
  }

  protected move(o: any, status: string): void {
    this.form.run(this.api.patch(`orders/${o.id}/status`, { expectedStatus: o.status, status, reason: null }, o.version), () => this.load());
  }

  // Chỉ nhận kéo sang cột kế tiếp; máy trạng thái ở server vẫn là chốt chặn
  protected dropped(event: CdkDragDrop<string>): void {
    const order = event.item.data;
    if (event.previousContainer !== event.container && this.nextFor(order.status) === event.container.data) {
      this.move(order, event.container.data);
    }
  }

  protected print(o: any): void {
    this.printing.set(o);
    setTimeout(() => window.print(), 50);
  }
}
