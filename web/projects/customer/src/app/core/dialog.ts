import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Modal } from '../shared/modal';

interface Ask {
  message: string;
  input: boolean;
  resolve: (value: string | null) => void;
}

const current = signal<Ask | null>(null);

// Hộp thoại trong trang thay cho confirm()/prompt() của trình duyệt (trên điện thoại khó dùng và có thể bị chặn)
function open(ask: Ask): void {
  current()?.resolve(null);
  current.set(ask);
}

export function askConfirm(message: string): Promise<boolean> {
  return new Promise((resolve) => open({ message, input: false, resolve: (v) => resolve(v !== null) }));
}

export function askText(message: string): Promise<string | null> {
  return new Promise((resolve) => open({ message, input: true, resolve }));
}

@Component({
  selector: 'ot-dialog-host',
  imports: [FormsModule, Modal],
  template: `
    @if (ask(); as a) {
      <div class="ot-dialog-back" role="dialog" aria-modal="true" [attr.aria-label]="a.message" otModal [closeOnBackdrop]="false" (dismiss)="close(null)">
        <form class="ot-dialog" (ngSubmit)="ok(a)">
          <p class="font-semibold">{{ a.message }}</p>
          @if (a.input) { <textarea class="ot-input" name="v" rows="3" maxlength="500" required [(ngModel)]="text"></textarea> }
          <div class="mt-4 flex justify-end gap-2">
            <button type="button" class="ot-btn ot-btn-outline" (click)="close(null)">Hủy</button>
            <button class="ot-btn ot-btn-primary" [disabled]="a.input && !text.trim()">Đồng ý</button>
          </div>
        </form>
      </div>
    }
  `,
})
export class DialogHost {
  protected readonly ask = current;
  protected text = '';

  protected ok(a: Ask): void {
    if (!a.input || this.text.trim()) {
      this.close(a.input ? this.text.trim() : '');
    }
  }

  protected close(value: string | null): void {
    current()?.resolve(value);
    current.set(null);
    this.text = '';
  }
}
