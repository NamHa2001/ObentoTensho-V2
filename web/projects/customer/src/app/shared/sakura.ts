import { Component } from '@angular/core';

// Hoa năm cánh, lấy màu theo chữ của phần tử cha
@Component({
  selector: 'ot-sakura',
  host: { class: 'inline-block shrink-0 align-middle' },
  template: `
    <svg viewBox="-12 -12 24 24" class="block size-full" aria-hidden="true">
      @for (angle of petals; track angle) {
        <path [attr.transform]="'rotate(' + angle + ')'" fill="currentColor" d="M0 -1C-4 -3 -5.5 -8 -2.6 -11L0 -9.2L2.6 -11C5.5 -8 4 -3 0 -1Z" />
      }
      <circle r="1.6" fill="var(--ot-surface)" />
    </svg>
  `,
})
export class Sakura {
  protected readonly petals = [0, 72, 144, 216, 288];
}
