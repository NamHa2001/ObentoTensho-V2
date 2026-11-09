import { afterNextRender, Component, DestroyRef, ElementRef, inject, viewChild } from '@angular/core';

interface Petal {
  x: number;
  y: number;
  size: number;
  speed: number;
  sway: number;
  swaySpeed: number;
  phase: number;
  angle: number;
  spin: number;
  flip: number;
  flipSpeed: number;
  depth: number;
  sprite: number;
}

const PETAL_SPRITES = ['5a', '5b', '5c', '5d', '5e', '5f', '5g', '5h'];
const FLOWER_SPRITES = ['6a', '6b', '6c'];
const FLOWER_CHANCE = 0.07;
const DESKTOP_COUNT = 26;
const PHONE_COUNT = 10;
const PHONE_WIDTH = 768;
const MAX_STEP_S = 0.05;
const EDGE = 60;
// Từ cỡ màn hình có cành hoa, khoảng một phần ba cánh rụng ra từ cành trên
const BRANCH_WIDTH = 1280;
const BRANCH_SHARE = 0.35;
const CONTENT_WIDTH = 1200;

// Cánh hoa rơi vẽ bằng canvas từ ảnh cánh thật: gió thổi từng đợt, cánh lật khi rơi, cánh gần to và nhòe, cánh xa nhỏ và nhạt.
// Chỉ chạy ở trình duyệt; tắt khi người dùng chọn giảm chuyển động, dừng vẽ khi tab bị ẩn
@Component({
  selector: 'ot-petal-rain',
  host: { class: 'ot-petal-rain', 'aria-hidden': 'true' },
  template: '<canvas #canvas class="block size-full"></canvas>',
})
export class PetalRain {
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  constructor() {
    const destroy = inject(DestroyRef);
    afterNextRender(() => destroy.onDestroy(this.start()));
  }

  private start(): () => void {
    const canvas = this.canvas().nativeElement;
    const ctx = canvas.getContext('2d');
    if (!ctx || matchMedia('(prefers-reduced-motion: reduce)').matches) {
      return () => undefined;
    }

    const load = (name: string) => {
      const image = new Image();
      image.src = `/decor/${name}.webp`;
      return image;
    };
    const petalImages = PETAL_SPRITES.map(load);
    const flowerImages = FLOWER_SPRITES.map(load);

    let width = 0;
    let height = 0;
    let frame = 0;
    let last = performance.now();
    const petals: Petal[] = [];

    const spawn = (anywhere: boolean): Petal => {
      const depth = Math.random();
      const flower = Math.random() < FLOWER_CHANCE;
      const fromBranch = !anywhere && width >= BRANCH_WIDTH && Math.random() < BRANCH_SHARE;
      const margin = Math.max((width - CONTENT_WIDTH) / 2, 0);
      return {
        x: fromBranch ? Math.random() * (margin + 60) : Math.random() * width,
        y: fromBranch ? 90 + Math.random() * 320 : anywhere ? Math.random() * height : -EDGE * Math.random(),
        size: (flower ? 22 : 12) + depth * (flower ? 16 : 14),
        speed: 24 + depth * 40,
        sway: 18 + Math.random() * 46,
        swaySpeed: 0.35 + Math.random() * 0.7,
        phase: Math.random() * Math.PI * 2,
        angle: Math.random() * Math.PI * 2,
        spin: (Math.random() - 0.5) * 1.4,
        flip: Math.random() * Math.PI * 2,
        flipSpeed: flower ? 0.6 + Math.random() : 1 + Math.random() * 2.2,
        depth,
        sprite: flower ? -1 - Math.floor(Math.random() * flowerImages.length) : Math.floor(Math.random() * petalImages.length),
      };
    };

    const resize = () => {
      const ratio = Math.min(devicePixelRatio || 1, 2);
      width = innerWidth;
      height = innerHeight;
      canvas.width = Math.round(width * ratio);
      canvas.height = Math.round(height * ratio);
      ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
      const count = width < PHONE_WIDTH ? PHONE_COUNT : DESKTOP_COUNT;
      while (petals.length < count) {
        petals.push(spawn(true));
      }

      petals.length = count;
    };

    const draw = (p: Petal) => {
      const image = p.sprite >= 0 ? petalImages[p.sprite] : flowerImages[-1 - p.sprite];
      if (!image.complete || image.naturalWidth === 0) {
        return;
      }

      const w = p.size;
      const h = (p.size * image.naturalHeight) / image.naturalWidth;
      ctx.save();
      ctx.translate(p.x + Math.sin(p.phase) * p.sway, p.y);
      ctx.rotate(p.angle);
      ctx.scale(1, Math.max(0.2, Math.abs(Math.cos(p.flip))));
      ctx.globalAlpha = 0.55 + p.depth * 0.4;
      ctx.filter = p.depth > 0.86 ? 'blur(1.6px)' : p.depth < 0.18 ? 'blur(0.9px)' : 'none';
      ctx.drawImage(image, -w / 2, -h / 2, w, h);
      ctx.restore();
    };

    const tick = (now: number) => {
      frame = requestAnimationFrame(tick);
      const step = Math.min((now - last) / 1000, MAX_STEP_S);
      last = now;
      if (document.hidden) {
        return;
      }

      const t = now / 1000;
      const wind = Math.sin(t * 0.23) * 28 + Math.sin(t * 0.071) * 18;
      ctx.clearRect(0, 0, width, height);
      for (let i = 0; i < petals.length; i++) {
        const p = petals[i];
        p.y += p.speed * step;
        p.x += wind * step * (0.4 + p.depth);
        p.phase += p.swaySpeed * step;
        p.angle += p.spin * step;
        p.flip += p.flipSpeed * step;
        if (p.y > height + EDGE || p.x < -EDGE * 3 || p.x > width + EDGE * 3) {
          petals[i] = spawn(false);
          continue;
        }

        draw(p);
      }
    };

    resize();
    addEventListener('resize', resize);
    frame = requestAnimationFrame(tick);
    return () => {
      cancelAnimationFrame(frame);
      removeEventListener('resize', resize);
    };
  }
}
