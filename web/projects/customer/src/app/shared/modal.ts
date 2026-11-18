import { DOCUMENT } from '@angular/common';
import { afterNextRender, Directive, ElementRef, HostListener, inject, input, OnDestroy, output } from '@angular/core';

const FOCUSABLE = 'input:not([disabled]), textarea:not([disabled]), select:not([disabled]), button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])';

// Cửa sổ đang mở, theo thứ tự mở; chỉ cửa sổ trên cùng nhận Esc và Tab (vd hộp xác nhận mở trên một bảng)
const open: Modal[] = [];

// Gắn vào lớp nền của cửa sổ: bấm ra nền hoặc Esc thì đóng, mở ra thì đưa focus vào trong, Tab không ra ngoài,
// đóng thì trả focus về nút đã mở. Một chỗ lo cho mọi cửa sổ để người dùng bàn phím, trình đọc màn hình không bị kẹt ở trang phía sau.
@Directive({ selector: '[otModal]' })
export class Modal implements OnDestroy {
  readonly dismiss = output<void>();
  // Ô nhập dài (lý do huỷ…) thì không đóng khi lỡ bấm ra ngoài để khỏi mất chữ đã gõ
  readonly closeOnBackdrop = input(true);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly document = inject(DOCUMENT);
  private readonly opener = this.document.activeElement as HTMLElement | null;

  constructor() {
    open.push(this);
    afterNextRender(() => this.focusable()[0]?.focus());
  }

  @HostListener('click', ['$event'])
  protected click(event: MouseEvent): void {
    if (event.target === this.host.nativeElement && this.closeOnBackdrop()) {
      this.dismiss.emit();
    }
  }

  @HostListener('document:keydown.escape')
  protected escape(): void {
    if (open.at(-1) === this) {
      this.dismiss.emit();
    }
  }

  // Tab ở phần tử cuối quay về đầu, Shift+Tab ở đầu sang cuối; focus lọt ra ngoài thì kéo về
  @HostListener('document:keydown.tab', ['$event'])
  @HostListener('document:keydown.shift.tab', ['$event'])
  protected tab(event: Event): void {
    const items = this.focusable();
    if (open.at(-1) !== this || items.length === 0) {
      return;
    }

    const active = this.document.activeElement;
    const back = (event as KeyboardEvent).shiftKey;
    const edge = back ? items[0] : items[items.length - 1];
    if (active === edge || !this.host.nativeElement.contains(active)) {
      event.preventDefault();
      (back ? items[items.length - 1] : items[0]).focus();
    }
  }

  ngOnDestroy(): void {
    open.splice(open.indexOf(this), 1);
    if (this.opener?.isConnected) {
      this.opener.focus();
    }
  }

  // Bỏ phần tử nằm trong khối đang ẩn vì không focus được
  private focusable(): HTMLElement[] {
    return [...this.host.nativeElement.querySelectorAll<HTMLElement>(FOCUSABLE)].filter((e) => !e.closest('[hidden], .hidden'));
  }
}
