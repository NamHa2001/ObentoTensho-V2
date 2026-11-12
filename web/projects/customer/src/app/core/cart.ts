import { afterNextRender, computed, Injectable, signal } from '@angular/core';

// docs/srs/12: giỏ trong trình duyệt chỉ lưu id món, id tùy chọn, số lượng và ghi chú; giá luôn hỏi lại server khi mở giỏ
export interface CartLine {
  menuItemId: number;
  optionIds: number[];
  quantity: number;
  note?: string;
}

const KEY = 'ot.cart.v1';
export const MAX_QUANTITY = 99;
const MAX_LINES = 50;
const MAX_OPTIONS = 20;
// Bằng OrderCart.NoteMaxLength phía máy chủ
export const NOTE_MAX_LENGTH = 200;

// Ghi chú gõ tay hoặc bị sửa trong bộ nhớ: gộp khoảng trắng, bỏ ký tự điều khiển, cắt đúng độ dài máy chủ nhận
export function cleanNote(value: unknown): string {
  return typeof value === 'string' ? value.replace(/[\u0000-\u001f\u007f]/g, ' ').replace(/\s+/g, ' ').trim().slice(0, NOTE_MAX_LENGTH) : '';
}

function sameOptions(a: number[], b: number[]): boolean {
  return a.length === b.length && a.every((id, i) => id === b[i]);
}

// Giỏ đọc từ bộ nhớ trình duyệt có thể bị sửa tay: bỏ dòng hỏng, ép số lượng về 1–99, giới hạn số dòng như máy chủ
export function cleanLines(value: unknown): CartLine[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .filter(isLine)
    .filter((l) => l.menuItemId > 0 && l.optionIds.length <= MAX_OPTIONS && l.optionIds.every((id) => id > 0))
    .slice(0, MAX_LINES)
    .map((l) => withNote({ menuItemId: l.menuItemId, optionIds: [...l.optionIds].sort((a, b) => a - b), quantity: Math.min(MAX_QUANTITY, Math.max(1, l.quantity)) }, l.note));
}

function withNote(line: CartLine, note: unknown): CartLine {
  const clean = cleanNote(note);
  return clean ? { ...line, note: clean } : line;
}

function isLine(value: unknown): value is CartLine {
  const line = value as CartLine;
  return Number.isInteger(line?.menuItemId) && Array.isArray(line?.optionIds) && line.optionIds.every(Number.isInteger) && Number.isInteger(line?.quantity) && line.quantity >= 1;
}

// Cùng món, cùng tùy chọn, cùng ghi chú thì cộng dồn; khác ghi chú ("ít cay" và "bình thường") là hai dòng riêng như các app đặt món
export function addLine(lines: CartLine[], menuItemId: number, optionIds: number[], quantity: number, note = ''): CartLine[] {
  const options = [...optionIds].sort((a, b) => a - b);
  const clean = cleanNote(note);
  const index = lines.findIndex((l) => l.menuItemId === menuItemId && sameOptions(l.optionIds, options) && (l.note ?? '') === clean);
  if (index >= 0) {
    return lines.map((l, i) => (i === index ? { ...l, quantity: Math.min(MAX_QUANTITY, l.quantity + quantity) } : l));
  }

  return lines.length >= MAX_LINES ? lines : [...lines, withNote({ menuItemId, optionIds: options, quantity: Math.min(MAX_QUANTITY, quantity) }, clean)];
}

// Sửa ghi chú ở giỏ: trùng với một dòng khác (cùng món, tùy chọn, ghi chú) thì gộp vào dòng đó
export function setLineNote(lines: CartLine[], index: number, note: string): CartLine[] {
  const line = lines[index];
  if (!line) {
    return lines;
  }

  const rest = lines.filter((_, i) => i !== index);
  const merged = addLine(rest, line.menuItemId, line.optionIds, line.quantity, note);
  // Giữ vị trí cũ của dòng khi không gộp
  return merged.length > rest.length ? [...rest.slice(0, index), merged.at(-1)!, ...rest.slice(index)] : merged;
}

@Injectable({ providedIn: 'root' })
export class Cart {
  readonly lines = signal<CartLine[]>([]);
  readonly count = computed(() => this.lines().reduce((sum, l) => sum + l.quantity, 0));

  // Đọc sau lần vẽ đầu để khớp HTML dựng sẵn từ server
  constructor() {
    afterNextRender(() => {
      this.lines.set(this.read());
      addEventListener('storage', (e) => {
        if (e.key === KEY) {
          this.lines.set(this.read());
        }
      });
    });
  }

  add(menuItemId: number, optionIds: number[], quantity: number, note = ''): void {
    this.lines.update((lines) => addLine(lines, menuItemId, optionIds, quantity, note));
    this.save();
  }

  setNote(index: number, note: string): void {
    this.lines.update((lines) => setLineNote(lines, index, note));
    this.save();
  }

  setQuantity(index: number, quantity: number): void {
    this.lines.update((lines) => lines.map((l, i) => (i === index ? { ...l, quantity: Math.min(MAX_QUANTITY, Math.max(1, quantity)) } : l)));
    this.save();
  }

  remove(index: number): void {
    this.lines.update((lines) => lines.filter((_, i) => i !== index));
    this.save();
  }

  clear(): void {
    this.lines.set([]);
    this.save();
  }

  private read(): CartLine[] {
    try {
      const parsed: unknown = JSON.parse(localStorage.getItem(KEY) ?? '[]');
      return cleanLines(parsed);
    } catch {
      return [];
    }
  }

  private save(): void {
    try {
      localStorage.setItem(KEY, JSON.stringify(this.lines()));
    } catch {
      // Trình duyệt chặn lưu trữ (chế độ riêng tư): giỏ vẫn dùng được tới khi đóng trang
    }
  }
}
