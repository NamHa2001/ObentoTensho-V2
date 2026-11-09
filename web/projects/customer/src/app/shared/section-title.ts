import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ChevronRight, LucideAngularModule } from 'lucide-angular';

import { Sakura } from './sakura';

@Component({
  selector: 'ot-section-title',
  imports: [RouterLink, LucideAngularModule, Sakura],
  host: { class: 'relative flex items-center gap-3 md:gap-4', '[class.ot-title-center]': '!!accent()' },
  template: `
    <ot-sakura class="ot-title-flower size-6 text-primary md:size-8" />
    <h2 class="min-w-0 text-xl max-[339px]:flex-1 min-[340px]:whitespace-nowrap min-[400px]:text-2xl md:text-[32px]">@if (accent(); as a) {<span class="text-primary">{{ a }}&ngsp;</span>}{{ title() }}</h2>
    @if (accent()) { <ot-sakura class="ot-title-flower size-8 text-primary max-md:hidden" /> }
    <span class="h-px min-w-0 flex-1 bg-accent/50 max-[339px]:hidden" [class.md:hidden]="!!accent()"></span>
    @if (link(); as path) {
      <a [routerLink]="path" [class.md:absolute]="!!accent()" [class.md:right-0]="!!accent()" class="flex min-h-11 shrink-0 items-center gap-1 text-sm font-semibold whitespace-nowrap text-primary md:text-base">
        Xem tất cả <lucide-icon [img]="chevron" [size]="16" />
      </a>
    }
  `,
})
export class SectionTitle {
  readonly title = input.required<string>();
  // Phần đầu tiêu đề tô màu nhấn; có phần này thì tiêu đề căn giữa từ máy tính bảng trở lên
  readonly accent = input<string | null>(null);
  readonly link = input<string | null>(null);
  protected readonly chevron = ChevronRight;
}
