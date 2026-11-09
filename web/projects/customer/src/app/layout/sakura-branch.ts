import { Component } from '@angular/core';

type Pt = [number, number];

interface Twig {
  points: Pt[];
  from: number;
  to: number;
}

interface Bloom {
  x: number;
  y: number;
  size: number;
  rotate: number;
  image: number;
  twig: number;
  back?: boolean;
}

interface Bud {
  x: number;
  y: number;
  rotate: number;
  twig: number;
}

// Khung vẽ 420x560, gốc ở góc trên trái, thân rủ dọc theo lề; nhánh 0 là thân chính
const TWIGS: Twig[] = [
  { points: [[-24, 4], [60, 60], [110, 140], [130, 240], [150, 340], [190, 430], [240, 522]], from: 28, to: 5 },
  { points: [[60, 60], [140, 50], [220, 30], [304, 40]], from: 10, to: 2 },
  { points: [[110, 140], [180, 150], [252, 192]], from: 8, to: 2 },
  { points: [[130, 240], [72, 290], [38, 342]], from: 7, to: 2 },
  { points: [[150, 340], [220, 352], [284, 392]], from: 7, to: 2 },
  { points: [[190, 430], [150, 482]], from: 5, to: 1.5 },
];

const IMAGES = [
  { src: '/decor/6a.webp', ratio: 195 / 220 },
  { src: '/decor/6b.webp', ratio: 171 / 212 },
  { src: '/decor/6c.webp', ratio: 173 / 233 },
];


const BUDS: Bud[] = [
  { x: 320, y: 34, rotate: 70, twig: 1 },
  { x: 232, y: 18, rotate: 20, twig: 1 },
  { x: 270, y: 204, rotate: 120, twig: 2 },
  { x: 30, y: 358, rotate: 200, twig: 3 },
  { x: 300, y: 404, rotate: 120, twig: 4 },
  { x: 140, y: 500, rotate: 200, twig: 5 },
  { x: 252, y: 542, rotate: 160, twig: 0 },
];

const SAMPLES = 10;

// Đường cong Catmull-Rom qua các điểm, lấy mẫu dày để vẽ thân thon dần
function sample(points: Pt[]): Pt[] {
  const out: Pt[] = [];
  for (let i = 0; i < points.length - 1; i++) {
    const p0 = points[Math.max(i - 1, 0)];
    const [p1, p2] = [points[i], points[i + 1]];
    const p3 = points[Math.min(i + 2, points.length - 1)];
    for (let k = 0; k < SAMPLES; k++) {
      const t = k / SAMPLES;
      const t2 = t * t;
      const t3 = t2 * t;
      const at = (a: number, b: number, c: number, d: number) => 0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
      out.push([at(p0[0], p1[0], p2[0], p3[0]), at(p0[1], p1[1], p2[1], p3[1])]);
    }
  }

  out.push(points[points.length - 1]);
  return out;
}

function taper(twig: Twig): { shape: string; line: string } {
  const pts = sample(twig.points);
  const left: string[] = [];
  const right: string[] = [];
  pts.forEach((p, i) => {
    const a = pts[Math.max(i - 1, 0)];
    const b = pts[Math.min(i + 1, pts.length - 1)];
    const len = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1;
    const nx = -(b[1] - a[1]) / len;
    const ny = (b[0] - a[0]) / len;
    const w = (twig.from + (twig.to - twig.from) * Math.pow(i / (pts.length - 1), 0.8)) / 2;
    left.push(`${(p[0] + nx * w).toFixed(1)} ${(p[1] + ny * w).toFixed(1)}`);
    right.unshift(`${(p[0] - nx * w).toFixed(1)} ${(p[1] - ny * w).toFixed(1)}`);
  });
  return { shape: `M${left.join('L')}L${right.join('L')}Z`, line: `M${pts.map((p) => `${(p[0] - 1).toFixed(1)} ${(p[1] - 1.2).toFixed(1)}`).join('L')}` };
}

const SHAPES = TWIGS.map(taper);

// Số ngẫu nhiên cố định theo hạt giống để server và trình duyệt vẽ giống hệt nhau
function seeded(seed: number): () => number {
  let x = seed;
  return () => {
    x = (x * 1103515245 + 12345) % 2147483648;
    return x / 2147483648;
  };
}

// Hoa mọc thành chùm dọc mỗi nhánh, dày hơn ở đầu nhánh; vài bông nằm sau thân cho có chiều sâu
function grow(): Bloom[] {
  const rand = seeded(20261109);
  const blooms: Bloom[] = [];
  TWIGS.forEach((twig, index) => {
    const pts = sample(twig.points);
    const step = index === 0 ? 26 : 18;
    for (let i = index === 0 ? 6 : 3; i < pts.length; i += step / 4) {
      const p = pts[Math.min(Math.round(i), pts.length - 1)];
      const cluster = 2 + Math.floor(rand() * 2);
      for (let k = 0; k < cluster; k++) {
        blooms.push({
          x: p[0] + (rand() - 0.5) * 34,
          y: p[1] + (rand() - 0.5) * 30,
          size: 44 + rand() * 30,
          rotate: Math.round((rand() - 0.5) * 120),
          image: Math.floor(rand() * IMAGES.length),
          twig: index,
          back: rand() < 0.28,
        });
      }
    }
  });
  return blooms;
}

const BLOOMS = grow();


// Cành hoa anh đào dựng từ nhiều mảnh: thân vector thon dần, hoa thật gắn từng bông.
// Thân đung đưa nhẹ, mỗi nhánh lệch pha, mỗi bông khẽ rung quanh cuống theo nhịp riêng
@Component({
  selector: 'ot-sakura-branch',
  template: `
    <svg viewBox="0 0 420 560" class="block size-full overflow-visible">
      <defs>
        <linearGradient id="ot-sb-bark" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stop-color="#3f2118" />
          <stop offset="0.6" stop-color="#5e3426" />
          <stop offset="1" stop-color="#7a4b37" />
        </linearGradient>
        <radialGradient id="ot-sb-bud" cx="40%" cy="30%" r="80%">
          <stop offset="0" stop-color="#f7a9b9" />
          <stop offset="1" stop-color="#c94766" />
        </radialGradient>
        <filter id="ot-sb-texture" x="-5%" y="-5%" width="110%" height="110%">
          <feTurbulence type="fractalNoise" baseFrequency="0.7 0.12" numOctaves="2" seed="7" result="noise" />
          <feDisplacementMap in="SourceGraphic" in2="noise" scale="1.8" />
        </filter>
        <filter id="ot-sb-shadow" x="-20%" y="-20%" width="140%" height="140%">
          <feDropShadow dx="1.5" dy="3" stdDeviation="2.5" flood-color="#7a2e3c" flood-opacity="0.22" />
        </filter>
      </defs>
      <g class="ot-sb-root" filter="url(#ot-sb-shadow)">
        @for (t of twigs; track $index; let i = $index) {
          <g class="ot-sb-twig" [style.transform-origin]="t.points[0][0] + 'px ' + t.points[0][1] + 'px'" [style.animation-delay.s]="-i * 1.3" [style.animation-duration.s]="5 + (i % 3) * 1.4">
            @for (b of bloomsOf(i, true); track $index) {
              <g [attr.transform]="'translate(' + b.x + ' ' + b.y + ') rotate(' + b.rotate + ')'" opacity="0.82">
                <image class="ot-sb-bloom" [attr.href]="images[b.image].src" [attr.x]="-b.size / 2" [attr.y]="(-b.size * images[b.image].ratio) / 2" [attr.width]="b.size" [attr.height]="b.size * images[b.image].ratio" [style.animation-delay.s]="-(b.x % 7) * 0.5" [style.animation-duration.s]="2.6 + (b.y % 5) * 0.4" />
              </g>
            }
            <g filter="url(#ot-sb-texture)">
              <path [attr.d]="shapes[i].shape" fill="url(#ot-sb-bark)" />
              <path [attr.d]="shapes[i].line" fill="none" stroke="#b58a6c" stroke-opacity="0.45" [attr.stroke-width]="i === 0 ? 2.2 : 1" stroke-linecap="round" />
            </g>
            @for (b of budsOf(i); track $index) {
              <g [attr.transform]="'translate(' + b.x + ' ' + b.y + ') rotate(' + b.rotate + ')'">
                <g class="ot-sb-bloom">
                  <ellipse cy="-6" rx="4.2" ry="7" fill="url(#ot-sb-bud)" />
                  <path d="M-4.4 -1Q0 4 4.4 -1L2 2.5L0 1L-2 2.5Z" fill="#6f7f3d" />
                </g>
              </g>
            }
            @for (b of bloomsOf(i, false); track $index) {
              <g [attr.transform]="'translate(' + b.x + ' ' + b.y + ') rotate(' + b.rotate + ')'">
                <image class="ot-sb-bloom" [attr.href]="images[b.image].src" [attr.x]="-b.size / 2" [attr.y]="(-b.size * images[b.image].ratio) / 2" [attr.width]="b.size" [attr.height]="b.size * images[b.image].ratio" [style.animation-delay.s]="-(b.x % 7) * 0.5" [style.animation-duration.s]="2.6 + (b.y % 5) * 0.4" />
              </g>
            }
          </g>
        }
      </g>
    </svg>
  `,
})
export class SakuraBranch {
  protected readonly twigs = TWIGS;
  protected readonly shapes = SHAPES;
  protected readonly images = IMAGES;

  protected bloomsOf(twig: number, back: boolean): Bloom[] {
    return BLOOMS.filter((b) => b.twig === twig && !!b.back === back);
  }

  protected budsOf(twig: number): Bud[] {
    return BUDS.filter((b) => b.twig === twig);
  }
}
