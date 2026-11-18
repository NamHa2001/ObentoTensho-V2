import { Component, HostListener, input, model, output } from '@angular/core';

import { Modal } from './modal';

@Component({
  selector: 'ot-lightbox',
  imports: [Modal],
  template: `
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-ink/90" role="dialog" aria-modal="true" aria-label="Xem ảnh" otModal (dismiss)="closed.emit()">
      <img class="max-h-[90dvh] max-w-full object-contain" [src]="images()[index()]" alt="" />
      <button type="button" class="ot-chip absolute top-3 right-3" style="margin-top: var(--ot-safe-top)" (click)="closed.emit()">Đóng</button>
      @if (images().length > 1) {
        <button type="button" class="ot-chip absolute top-1/2 left-3" aria-label="Ảnh trước" (click)="step(-1); $event.stopPropagation()">‹</button>
        <button type="button" class="ot-chip absolute top-1/2 right-3" aria-label="Ảnh sau" (click)="step(1); $event.stopPropagation()">›</button>
      }
    </div>
  `,
})
export class Lightbox {
  readonly images = input.required<string[]>();
  readonly index = model(0);
  readonly closed = output<void>();
  protected step(delta: number): void {
    const count = this.images().length;
    this.index.update((i) => (i + delta + count) % count);
  }

  @HostListener('document:keydown', ['$event'])
  protected key(event: KeyboardEvent): void {
    if (event.key === 'ArrowLeft' && this.images().length > 1) {
      this.step(-1);
    } else if (event.key === 'ArrowRight' && this.images().length > 1) {
      this.step(1);
    }
  }
}
