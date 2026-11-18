import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';
import { from, map, Observable, of, shareReplay, switchMap } from 'rxjs';

import { Api } from './api';

interface TurnstileApi {
  render(el: HTMLElement, options: Record<string, unknown>): string;
  remove(id: string): void;
}

const CHALLENGE_TIMEOUT_MS = 120000;
const SCRIPT = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';

// SEC-22: mỗi lần gửi lấy một token mới (token dùng một lần). Chưa cấu hình khóa site thì không gửi gì; server tự quyết theo cấu hình
@Injectable({ providedIn: 'root' })
export class Turnstile {
  private readonly doc = inject(DOCUMENT);
  private script: Promise<void> | null = null;
  private readonly siteKey = inject(Api)
    .getOr<Record<string, string>>('public/settings', {})
    .pipe(
      map((s) => s['security.turnstileSiteKey'] ?? ''),
      shareReplay(1),
    );

  headers(): Observable<Record<string, string>> {
    return this.siteKey.pipe(switchMap((key): Observable<Record<string, string>> => (key ? from(this.token(key)).pipe(map((t) => ({ 'X-Turnstile-Token': t }))) : of({}))));
  }

  private load(): Promise<void> {
    this.script ??= new Promise((resolve, reject) => {
      const el = this.doc.createElement('script');
      el.src = SCRIPT;
      el.async = true;
      el.onload = () => resolve();
      el.onerror = () => {
        this.script = null;
        reject(new Error('turnstile'));
      };
      this.doc.head.appendChild(el);
    });
    return this.script;
  }

  // Thường chạy ngầm; chỉ hiện ô ở đáy màn hình khi Cloudflare cần khách tương tác
  private async token(siteKey: string): Promise<string> {
    await this.load();
    const api = (this.doc.defaultView as unknown as { turnstile: TurnstileApi }).turnstile;
    const box = this.doc.createElement('div');
    box.style.cssText = 'position:fixed;left:50%;bottom:calc(72px + env(safe-area-inset-bottom,0px));transform:translateX(-50%);z-index:60';
    this.doc.body.appendChild(box);
    return new Promise((resolve, reject) => {
      const done = (act: () => void) => {
        api.remove(id);
        box.remove();
        act();
      };
      const id = api.render(box, {
        sitekey: siteKey,
        appearance: 'interaction-only',
        callback: (token: string) => done(() => resolve(token)),
        'error-callback': () => done(() => reject(new Error('turnstile'))),
        'expired-callback': () => done(() => reject(new Error('turnstile'))),
        'timeout-callback': () => done(() => reject(new Error('turnstile'))),
      });
      // Khách bỏ dở thử thách: sau một lúc thì gỡ ô và báo lỗi để nút gửi không bị khóa mãi
      setTimeout(() => {
        if (box.isConnected) {
          done(() => reject(new Error('turnstile')));
        }
      }, CHALLENGE_TIMEOUT_MS);
    });
  }
}
