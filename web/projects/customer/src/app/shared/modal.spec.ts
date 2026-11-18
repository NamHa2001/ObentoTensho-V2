import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { Modal } from './modal';

@Component({
  imports: [Modal],
  template: `
    <button id="opener" type="button" (click)="open.set(true)">Mở</button>
    @if (open()) {
      <div id="back" otModal [closeOnBackdrop]="backdrop" (dismiss)="dismissed = dismissed + 1; open.set(false)">
        <div id="sheet"><p>Nội dung</p><input id="first" /><button type="button">Đóng</button></div>
      </div>
    }
  `,
})
class Host {
  readonly open = signal(false);
  backdrop = true;
  dismissed = 0;
}

async function opened(backdrop = true) {
  const fixture = TestBed.createComponent(Host);
  fixture.componentInstance.backdrop = backdrop;
  document.body.append(fixture.nativeElement);
  fixture.detectChanges();
  const opener = fixture.nativeElement.querySelector('#opener') as HTMLButtonElement;
  opener.focus();
  opener.click();
  fixture.detectChanges();
  await fixture.whenStable();
  const el = (id: string) => fixture.nativeElement.querySelector(id) as HTMLElement;
  return { fixture, host: fixture.componentInstance, el, opener };
}

describe('Cửa sổ (otModal)', () => {
  it('mở ra thì focus vào ô đầu tiên trong cửa sổ', async () => {
    const { el } = await opened();
    expect(document.activeElement).toBe(el('#first'));
  });

  it('bấm ra nền thì đóng, bấm vào trong thì không', async () => {
    const { host, el } = await opened();
    el('#sheet').click();
    expect(host.dismissed).toBe(0);
    el('#back').click();
    expect(host.dismissed).toBe(1);
  });

  it('Esc thì đóng và trả focus về nút đã mở', async () => {
    const { fixture, host, opener } = await opened();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(host.dismissed).toBe(1);
    expect(document.activeElement).toBe(opener);
  });

  it('cửa sổ có ô nhập dài thì bấm ra nền không đóng (tránh mất chữ), Esc vẫn đóng', async () => {
    const { host, el } = await opened(false);
    el('#back').click();
    expect(host.dismissed).toBe(0);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(host.dismissed).toBe(1);
  });

  it('Tab ở nút cuối quay về ô đầu, Shift+Tab ở ô đầu sang nút cuối, focus lọt ra ngoài thì kéo về', async () => {
    const { el, opener } = await opened();
    const last = el('#sheet button');
    const tab = (shiftKey = false) => {
      const e = new KeyboardEvent('keydown', { key: 'Tab', shiftKey, cancelable: true });
      document.dispatchEvent(e);
      return e.defaultPrevented;
    };

    last.focus();
    expect(tab()).toBe(true);
    expect(document.activeElement).toBe(el('#first'));

    expect(tab(true)).toBe(true);
    expect(document.activeElement).toBe(last);

    // Giữa cửa sổ thì để trình duyệt tự chuyển
    el('#first').focus();
    expect(tab()).toBe(false);

    opener.focus();
    tab();
    expect(document.activeElement).toBe(el('#first'));
  });
});

@Component({
  imports: [Modal],
  template: `
    <div id="outer" otModal (dismiss)="outer = outer + 1">
      <button id="a" type="button">A</button>
      @if (inner()) {
        <div id="inner" otModal (dismiss)="closed = closed + 1; inner.set(false)"><button id="b" type="button">B</button></div>
      }
    </div>
  `,
})
class Stacked {
  readonly inner = signal(true);
  outer = 0;
  closed = 0;
}

describe('Hai cửa sổ chồng nhau', () => {
  it('Esc chỉ đóng cửa sổ trên cùng, lần Esc sau mới tới cửa sổ dưới', async () => {
    const fixture = TestBed.createComponent(Stacked);
    document.body.append(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    const host = fixture.componentInstance;

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect([host.closed, host.outer]).toEqual([1, 0]);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect([host.closed, host.outer]).toEqual([1, 1]);
    fixture.destroy();
  });
});
