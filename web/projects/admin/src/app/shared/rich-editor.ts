import { Component, ElementRef, inject, input, OnInit, output, signal, viewChild } from '@angular/core';

import { askText } from '../../../../customer/src/app/core/dialog';
import { Api } from '../core/api';
import { altOf, errorText } from './ui';

// Soạn nội dung không cần gõ HTML; server làm sạch HTML khi lưu (SEC-08)
@Component({
  selector: 'ot-rich-editor',
  template: `
    <div class="flex flex-wrap gap-1 rounded-t-md border border-b-0 border-line bg-surface-2 p-1">
      @for (b of buttons; track b.command) {
        <button type="button" class="min-h-9 min-w-9 rounded-sm px-2 font-semibold" [attr.aria-label]="b.title" (mousedown)="$event.preventDefault(); run(b.command, b.arg)">{{ b.label }}</button>
      }
      <button type="button" class="min-h-9 rounded-sm px-2" (mousedown)="$event.preventDefault(); link()">Liên kết</button>
      <label class="flex min-h-9 cursor-pointer items-center rounded-sm px-2 font-normal">Ảnh<input class="sr-only" type="file" accept="image/*" (change)="image($event)" /></label>
    </div>
    <div #box class="ot-rich" contenteditable="true" role="textbox" aria-multiline="true" [attr.aria-label]="label()" [innerHTML]="initial()" (input)="emit()" (blur)="emit()"></div>
    @if (error(); as e) { <p class="ot-error mt-1 text-sm">{{ e }}</p> }
  `,
})
export class RichEditor implements OnInit {
  private readonly api = inject(Api);
  private readonly box = viewChild.required<ElementRef<HTMLElement>>('box');
  readonly value = input<string | null>(null);
  readonly label = input('Nội dung');
  readonly valueChange = output<string | null>();
  protected readonly initial = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly buttons = [
    { command: 'bold', label: 'B', title: 'Đậm' },
    { command: 'italic', label: 'I', title: 'Nghiêng' },
    { command: 'formatBlock', arg: 'h2', label: 'H2', title: 'Tiêu đề' },
    { command: 'formatBlock', arg: 'p', label: 'P', title: 'Đoạn văn' },
    { command: 'insertUnorderedList', label: '•', title: 'Danh sách' },
  ];

  // Chỉ gán nội dung một lần, gán lại khi đang gõ sẽ làm nhảy con trỏ
  ngOnInit(): void {
    this.initial.set(this.value() ?? '');
  }

  protected run(command: string, arg?: string): void {
    document.execCommand(command, false, arg);
    this.emit();
  }

  // Hộp thoại lấy mất con trỏ, phải giữ lại vùng đang bôi đen để gắn liên kết đúng chỗ
  protected async link(): Promise<void> {
    const selection = document.getSelection();
    const range = selection && selection.rangeCount > 0 ? selection.getRangeAt(0).cloneRange() : null;
    const url = await askText('Địa chỉ liên kết (https://...)');
    if (url && range) {
      this.box().nativeElement.focus();
      selection?.removeAllRanges();
      selection?.addRange(range);
      this.run('createLink', url);
    }
  }

  protected image(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const data = new FormData();
    data.append('file', file);
    data.append('altText', altOf(file));
    this.error.set(null);
    this.api.post<{ variants: { url: string }[] }>('images', data).subscribe({
      next: (image) => {
        const url = image.variants.at(-1)?.url;
        if (url) {
          this.box().nativeElement.focus();
          this.run('insertImage', url);
        } else {
          this.error.set('Ảnh đang được xử lý, vui lòng thử chèn lại sau ít phút.');
        }
      },
      error: (e: unknown) => this.error.set(errorText(e)),
    });
    input.value = '';
  }

  protected emit(): void {
    const html = this.box().nativeElement.innerHTML;
    this.valueChange.emit(html === '' || html === '<br>' ? null : html);
  }
}
