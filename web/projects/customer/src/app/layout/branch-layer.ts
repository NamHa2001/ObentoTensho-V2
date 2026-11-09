import { Component } from '@angular/core';

import { SakuraBranch } from './sakura-branch';

// Hai cành hoa anh đào nằm trên nội dung (dưới thanh đầu trang và hộp thoại), rủ dọc theo hai lề.
// Không nhận thao tác nên vẫn bấm được nút bên dưới; chỉ hiện trên màn hình rộng
@Component({
  selector: 'ot-branch-layer',
  imports: [SakuraBranch],
  host: { class: 'ot-branch-layer', 'aria-hidden': 'true' },
  template: `
    <ot-sakura-branch class="ot-branch ot-branch-top" />
    <ot-sakura-branch class="ot-branch ot-branch-bottom" />
  `,
})
export class BranchLayer {}
