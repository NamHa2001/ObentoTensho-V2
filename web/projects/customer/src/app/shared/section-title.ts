import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ChevronRight, LucideAngularModule } from 'lucide-angular';

import { Sakura } from './sakura';

@Component({
  selector: 'ot-section-title',
  imports: [RouterLink, LucideAngularModule, Sakura],
  host: { class: 'flex items-center gap-3 md:gap-4' },
  template: `
    <ot-sakura class="size-6 text-primary md:size-8" />
    <h2 class="text-2xl whitespace-nowrap md:text-[32px]">{{ title() }}</h2>
    <span class="h-px flex-1 bg-accent/50"></span>
    @if (link(); as path) {
      <a [routerLink]="path" class="flex min-h-11 items-center gap-1 text-sm font-semibold whitespace-nowrap text-primary md:text-base">
        Xem tất cả <lucide-icon [img]="chevron" [size]="16" />
      </a>
    }
  `,
})
export class SectionTitle {
  readonly title = input.required<string>();
  readonly link = input<string | null>(null);
  protected readonly chevron = ChevronRight;
}
