import { afterNextRender, Component, DestroyRef, effect, ElementRef, inject, input, model, signal, untracked, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { LocateFixed, LucideAngularModule, MapPin } from 'lucide-angular';
import { catchError, debounceTime, from, map, Observable, of, Subject, switchMap } from 'rxjs';

import { LatLng, Leaflet, LeafletMap, LeafletMarker, loadLeaflet } from './leaflet';

export type Pin = LatLng;

interface Suggestion {
  label: string;
  pin: Pin;
}

interface PhotonFeature {
  properties: Record<string, string | undefined>;
  geometry: { coordinates: [number, number] };
}

// Chưa có toạ độ quán thì mở bản đồ ở trung tâm TP.HCM
const FALLBACK_CENTER: Pin = { lat: 10.7769, lng: 106.7009 };
const PHOTON = 'https://photon.komoot.io';

// Địa chỉ giao hàng như các app giao đồ ăn: gõ thì hiện gợi ý (Photon, dữ liệu OpenStreetMap), chọn gợi ý thì ghim lên bản đồ;
// khách kéo ghim hoặc chạm bản đồ để chỉnh đúng cửa nhà. Phí ship tính theo điểm ghim nên nhà trong hẻm vẫn tính đúng.
@Component({
  selector: 'ot-address-picker',
  imports: [FormsModule, LucideAngularModule],
  template: `
    <label class="relative block">
      Địa chỉ giao hàng
      <input
        class="ot-input"
        name="address"
        autocomplete="street-address"
        maxlength="300"
        placeholder="Số nhà, đường, phường/xã, quận/huyện"
        role="combobox"
        aria-autocomplete="list"
        aria-controls="ot-address-suggest"
        [attr.aria-expanded]="open() && suggestions().length > 0"
        [ngModel]="address()"
        (ngModelChange)="type($event)"
        (focus)="open.set(true)"
        (blur)="open.set(false)"
        (keydown.escape)="open.set(false)"
      />
      @if (open() && suggestions().length > 0) {
        <ul id="ot-address-suggest" class="ot-suggest" role="listbox" aria-label="Địa chỉ gợi ý">
          @for (s of suggestions(); track $index) {
            <li role="option" [attr.aria-selected]="false">
              <button type="button" (mousedown)="$event.preventDefault()" (click)="choose(s)"><lucide-icon class="shrink-0 text-muted" [img]="icons.pin" [size]="16" /><span>{{ s.label }}</span></button>
            </li>
          }
        </ul>
      }
    </label>
    <div class="ot-map-wrap mt-3">
      <div #map class="ot-map" role="application" aria-label="Bản đồ ghim vị trí nhận hàng"></div>
      <span #pinIcon hidden><lucide-icon class="ot-map-pin" [img]="icons.pin" [size]="40" /></span>
      @if (failed()) {
        <p class="ot-map-msg">Chưa tải được bản đồ. Bạn vẫn có thể nhập địa chỉ bằng chữ.</p>
      } @else {
        <button type="button" class="ot-map-locate" [disabled]="locating()" (click)="locate()"><lucide-icon [img]="icons.locate" [size]="18" /> {{ locating() ? 'Đang tìm…' : 'Vị trí của tôi' }}</button>
      }
    </div>
    <p class="mt-2 text-sm text-muted">
      {{ pin() ? 'Đã ghim vị trí nhận hàng. Chưa đúng cửa nhà thì kéo ghim hoặc chạm vào bản đồ.' : 'Chọn một địa chỉ gợi ý hoặc chạm vào bản đồ để ghim đúng chỗ nhận hàng.' }}
    </p>
    @if (locateError(); as e) {
      <p class="ot-error mt-1 text-sm">{{ e }}</p>
    }
  `,
})
export class AddressPicker {
  readonly address = model('');
  readonly pin = model<Pin | null>(null);
  readonly center = input<Pin | null>(null);

  protected readonly icons = { pin: MapPin, locate: LocateFixed };
  protected readonly suggestions = signal<Suggestion[]>([]);
  protected readonly open = signal(false);
  protected readonly failed = signal(false);
  protected readonly locating = signal(false);
  protected readonly locateError = signal<string | null>(null);
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('map');
  // Ghim trên bản đồ là icon MapPin của lucide, nhân bản từ phần tử ẩn trong template
  private readonly pinIcon = viewChild.required<ElementRef<HTMLElement>>('pinIcon');
  private readonly typed = new Subject<string>();
  private leaflet: Leaflet | null = null;
  private view: LeafletMap | null = null;
  private marker: LeafletMarker | null = null;
  private resize: ResizeObserver | null = null;

  constructor() {
    const destroyRef = inject(DestroyRef);
    this.typed
      .pipe(
        debounceTime(400),
        switchMap((q) => (q.trim().length < 4 ? of([]) : this.search(q.trim()))),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe((list) => this.suggestions.set(list));

    afterNextRender(() => {
      loadLeaflet()
        .then((leaflet) => this.draw(leaflet))
        .catch(() => this.failed.set(true));
    });
    destroyRef.onDestroy(() => {
      this.resize?.disconnect();
      this.view?.remove();
    });

    // Toạ độ quán tải xong sau khi bản đồ đã mở thì dời về quán (khi khách chưa ghim)
    effect(() => {
      const center = this.center();
      if (center && !untracked(this.pin)) {
        this.view?.setView([center.lat, center.lng], 15);
      }
    });
  }

  protected type(value: string): void {
    this.address.set(value);
    this.open.set(true);
    this.typed.next(value);
  }

  protected choose(s: Suggestion): void {
    this.address.set(s.label);
    this.open.set(false);
    this.suggestions.set([]);
    this.place(s.pin, 17);
  }

  protected locate(): void {
    this.locateError.set(null);
    if (!('geolocation' in navigator)) {
      this.locateError.set('Trình duyệt không hỗ trợ lấy vị trí.');
      return;
    }

    this.locating.set(true);
    navigator.geolocation.getCurrentPosition(
      (p) => {
        this.locating.set(false);
        const pin = { lat: p.coords.latitude, lng: p.coords.longitude };
        this.place(pin, 17);
        if (!this.address().trim()) {
          this.reverse(pin);
        }
      },
      () => {
        this.locating.set(false);
        this.locateError.set('Không lấy được vị trí. Hãy cho phép trình duyệt truy cập vị trí hoặc chạm vào bản đồ để ghim.');
      },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  }

  private draw(leaflet: Leaflet): void {
    this.leaflet = leaflet;
    const start = this.pin() ?? this.center() ?? FALLBACK_CENTER;
    this.view = leaflet.map(this.host().nativeElement, { zoomControl: true }).setView([start.lat, start.lng], this.pin() ? 17 : 15);
    // Ô nền từ máy chủ OpenStreetMap Pháp (kiểu HOT, nhãn theo tên địa phương): một số nhà mạng trong nước chặn tên miền openstreetmap.org
    leaflet.tileLayer('https://{s}.tile.openstreetmap.fr/hot/{z}/{x}/{y}.png', { subdomains: 'abc', maxZoom: 19, attribution: '&copy; OpenStreetMap, OSM France' }).addTo(this.view);
    this.view.on('click', (e) => this.place({ lat: e.latlng.lat, lng: e.latlng.lng }));
    const pin = this.pin();
    if (pin) {
      this.place(pin);
    }

    // Khung bản đồ vừa hiện ra hoặc đổi cỡ (xoay màn hình, mở phần giao hàng) thì Leaflet đo lại để không bị ô xám
    this.resize = new ResizeObserver(() => this.view?.invalidateSize());
    this.resize.observe(this.host().nativeElement);
  }

  private place(pin: Pin, zoom?: number): void {
    this.pin.set({ lat: round(pin.lat), lng: round(pin.lng) });
    if (!this.view || !this.leaflet) {
      return;
    }

    if (this.marker) {
      this.marker.setLatLng([pin.lat, pin.lng]);
    } else {
      const icon = this.leaflet.divIcon({ className: '', html: this.pinIcon().nativeElement.firstElementChild!.cloneNode(true), iconSize: [40, 40], iconAnchor: [20, 37] });
      this.marker = this.leaflet.marker([pin.lat, pin.lng], { draggable: true, icon, keyboard: false }).addTo(this.view);
      this.marker.on('dragend', () => {
        const at = this.marker!.getLatLng();
        this.pin.set({ lat: round(at.lat), lng: round(at.lng) });
      });
    }

    this.view.setView([pin.lat, pin.lng], zoom ?? Math.max(this.view.getZoom(), 16));
  }

  // Gợi ý gần quán trước, chỉ trong Việt Nam. Bản đồ ở VN hầu như chưa có số nhà: "12 Nguyễn Huệ" thì tìm theo tên đường
  // rồi giữ số nhà khách gõ trong gợi ý; ghim đặt ở con đường, khách kéo tới đúng cửa nhà.
  private search(q: string): Observable<Suggestion[]> {
    const near = this.center() ?? FALLBACK_CENTER;
    const house = /^(?:số\s*)?(\d+[\w/-]*)\s+(.{3,})$/iu.exec(q);
    const text = house ? house[2] : q;
    const url = `${PHOTON}/api/?q=${encodeURIComponent(text)}&limit=6&lang=default&lat=${near.lat}&lon=${near.lng}&bbox=102,8,110,24`;
    // eslint-disable-next-line no-restricted-globals -- dịch vụ gợi ý địa chỉ bên ngoài (Photon), không phải API của quán
    return from(fetch(url).then((r) => (r.ok ? r.json() : { features: [] }))).pipe(
      map((body: { features?: PhotonFeature[] }) => {
        const list = (body.features ?? [])
          .filter((f) => f.properties['countrycode'] === 'VN')
          .map((f) => {
            const s = toSuggestion(f);
            return house && !f.properties['housenumber'] ? { ...s, label: `${house[1]} ${s.label}` } : s;
          });
        return list.filter((s, i) => list.findIndex((x) => x.label === s.label) === i).slice(0, 5);
      }),
      catchError(() => of([])),
    );
  }

  private reverse(pin: Pin): void {
    // eslint-disable-next-line no-restricted-globals -- dịch vụ đổi toạ độ ra địa chỉ bên ngoài (Photon)
    fetch(`${PHOTON}/reverse?lang=default&lat=${pin.lat}&lon=${pin.lng}`)
      .then((r) => (r.ok ? r.json() : { features: [] }))
      .then((body: { features?: PhotonFeature[] }) => {
        const first = body.features?.[0];
        if (first && !this.address().trim()) {
          this.address.set(toSuggestion(first).label);
        }
      })
      .catch(() => undefined);
  }
}

function round(value: number): number {
  return Math.round(value * 1e6) / 1e6;
}

function toSuggestion(f: PhotonFeature): Suggestion {
  const p = f.properties;
  const street = [p['housenumber'], p['street']].filter(Boolean).join(' ');
  // Tên địa phương tiếng Việt (lang=default); bỏ "khu phố" cho gọn
  const parts = [p['name'] !== p['street'] ? p['name'] : undefined, street, p['district'], p['city'] ?? p['state']];
  const label = parts.filter((v, i): v is string => !!v && parts.indexOf(v) === i).join(', ');
  return { label, pin: { lat: f.geometry.coordinates[1], lng: f.geometry.coordinates[0] } };
}
