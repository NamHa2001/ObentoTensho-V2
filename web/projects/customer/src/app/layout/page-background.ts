import { Component } from '@angular/core';

import { PetalRain } from './petal-rain';

// Nền trang trí cố định phía sau mọi trang: giấy washi và cánh hoa rơi.
// Ảnh nằm trong public/decor. Không nhận thao tác, trình đọc màn hình bỏ qua
@Component({
  selector: 'ot-page-background',
  imports: [PetalRain],
  host: { class: 'ot-page-bg', 'aria-hidden': 'true' },
  template: `
    <ot-petal-rain />
  `,
})
export class PageBackground {}
