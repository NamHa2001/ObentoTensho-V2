import { Location } from '@angular/common';
import { inject, Injectable, signal } from '@angular/core';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

// Nút quay lại trên thanh tiêu đề: trang con khai báo data.back (trang cha) trong routes.
// Vừa đi từ trang cha sang thì lùi lịch sử để giữ bộ lọc và chỗ đang cuộn; vào thẳng từ link ngoài thì mở trang cha.
@Injectable({ providedIn: 'root' })
export class Back {
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private previous: string | null = null;
  private current: string | null = null;
  readonly target = signal<string | null>(null);

  constructor() {
    this.target.set(deepest(this.router.routerState.snapshot.root).data['back'] ?? null);
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => {
        this.previous = this.current;
        this.current = e.urlAfterRedirects;
        this.target.set(deepest(this.router.routerState.snapshot.root).data['back'] ?? null);
      });
  }

  go(): void {
    const target = this.target();
    if (!target) {
      return;
    }

    if (this.previous?.split(/[?#]/)[0] === target) {
      this.location.back();
    } else {
      void this.router.navigateByUrl(target);
    }
  }
}

function deepest(route: ActivatedRouteSnapshot): ActivatedRouteSnapshot {
  return route.firstChild ? deepest(route.firstChild) : route;
}
