import { DOCUMENT } from '@angular/common';
import { afterNextRender, Directive, ElementRef, HostListener, inject, input, OnDestroy, output } from '@angular/core';

const FOCUSABLE = 'input:not([disabled]), textarea:not([disabled]), select:not([disabled]), button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])';

// Gắn vào lớp nền của cửa sổ: bấm ra nền hoặc Esc thì đóng, mở ra thì đưa focus vào trong, đóng thì trả focus về nút đã mở.
// Một chỗ lo cho mọi cửa sổ để người dùng bàn phím, trình đọc màn hình không bị kẹt ở trang phía sau.
@Directive({ selector: '[otModal]' })
export class Modal implements OnDestroy {
  readonly dismiss = output<void>();
  // Ô nhập dài (lý do huỷ…) thì không đóng khi lỡ bấm ra ngoài để khỏi mất chữ đã gõ
  readonly closeOnBackdrop = input(true);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly opener = inject(DOCUMENT).activeElement as HTMLElement | null;

  constructor() {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(FOCUSABLE)?.focus());
  }

  @HostListener('click', ['$event'])
  protected click(event: MouseEvent): void {
    if (event.target === this.host.nativeElement && this.closeOnBackdrop()) {
      this.dismiss.emit();
    }
  }

  @HostListener('document:keydown.escape')
  protected escape(): void {
    this.dismiss.emit();
  }

  ngOnDestroy(): void {
    if (this.opener?.isConnected) {
      this.opener.focus();
    }
  }
}
