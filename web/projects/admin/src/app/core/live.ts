import { DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { LiveHub } from '../../../../customer/src/app/core/live';
import { Api } from './api';

export interface Note {
  text: string;
  link: string;
}

const NOTIFY: Record<string, ((p: any) => Note) | null> = {
  'order.created': (p) => ({ text: `Đơn mới ${p.code ?? ''}`, link: `/orders/${p.id}` }),
  'reservation.created': (p) => ({ text: `Đặt bàn mới ${p.code ?? ''}`, link: `/reservations/${p.id}` }),
  'contact.created': () => ({ text: 'Tin liên hệ mới', link: '/r/contact-messages' }),
  'order.statusChanged': null,
  'reservation.statusChanged': null,
  'table.statusChanged': null,
};

const MAX_NOTES = 10;
const TOAST_MS = 4000;
const LOST_AFTER_MS = 8000;

@Injectable({ providedIn: 'root' })
export class Live {
  private readonly api = inject(Api);
  private hub: LiveHub | null = null;
  private audio: AudioContext | null = null;
  private wake: { release(): Promise<void> } | null = null;
  private lostTimer: ReturnType<typeof setTimeout> | null = null;
  readonly changes = signal(0);
  readonly notes = signal<Note[]>([]);
  readonly unread = signal(0);
  readonly connected = signal(true);
  readonly toast = signal<string | null>(null);
  readonly sound = signal(false);

  start(): void {
    if (this.hub) {
      return;
    }

    this.hub = new LiveHub({
      url: () => firstValueFrom(this.api.post<{ ticket: string }>('realtime/ticket')).then((t) => `/hubs/v1/staff?ticket=${encodeURIComponent(t.ticket)}`),
      events: Object.keys(NOTIFY),
      onEvent: (name, payload) => this.handle(name, payload),
      onOpen: (_, reconnected) => {
        if (reconnected) {
          this.changes.update((c) => c + 1);
        }

        return Promise.resolve();
      },
      onState: (connected) => this.setConnected(connected),
    });
    void this.hub.start();
  }

  stop(): void {
    this.hub?.stop();
    this.hub = null;
    this.setConnected(true);
  }

  markRead(): void {
    this.unread.set(0);
  }

  // Trình duyệt chỉ cho phát âm sau một lần bấm; giữ màn hình sáng trong ca
  async toggleSound(): Promise<void> {
    const on = !this.sound();
    this.sound.set(on);
    if (on) {
      this.audio ??= new AudioContext();
      await this.audio.resume();
      this.beep();
      this.wake = await (navigator as unknown as { wakeLock?: { request(t: string): Promise<{ release(): Promise<void> }> } }).wakeLock?.request('screen').catch(() => null) ?? null;
    } else {
      await this.wake?.release();
      this.wake = null;
    }
  }

  // Rớt mạng thoáng qua thì tự nối lại, chỉ báo khi mất hẳn để nhân viên không bị giật mình
  private setConnected(connected: boolean): void {
    if (this.lostTimer) {
      clearTimeout(this.lostTimer);
      this.lostTimer = null;
    }

    if (connected) {
      this.connected.set(true);
    } else {
      this.lostTimer = setTimeout(() => this.connected.set(false), LOST_AFTER_MS);
    }
  }

  private handle(name: string, payload: unknown): void {
    this.changes.update((c) => c + 1);
    const note = NOTIFY[name]?.(payload);
    if (!note) {
      return;
    }

    this.notes.update((n) => [note, ...n].slice(0, MAX_NOTES));
    this.unread.update((u) => u + 1);
    this.toast.set(note.text);
    setTimeout(() => this.toast.set(null), TOAST_MS);
    if (this.sound() && name === 'order.created') {
      this.beep();
    }
  }

  private beep(): void {
    if (!this.audio) {
      return;
    }

    const oscillator = this.audio.createOscillator();
    oscillator.frequency.value = 880;
    oscillator.connect(this.audio.destination);
    oscillator.start();
    oscillator.stop(this.audio.currentTime + 0.3);
  }
}

// Có sự kiện realtime (hoặc vừa nối lại) thì tải lại dữ liệu của màn hình
export function reloadOnLive(load: () => void): void {
  const live = inject(Live);
  let seen = untracked(live.changes);
  const ref = effect(() => {
    const now = live.changes();
    if (now !== seen) {
      seen = now;
      untracked(load);
    }
  });
  inject(DestroyRef).onDestroy(() => ref.destroy());
}
