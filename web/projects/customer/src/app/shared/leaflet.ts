// Leaflet (bản đồ OpenStreetMap, miễn phí) tải từ CDN khi khách mở bản đồ lần đầu; khóa đúng bản 1.9.4 kèm mã kiểm tra toàn vẹn
const BASE = 'https://unpkg.com/leaflet@1.9.4/dist';
const SCRIPT_SRI = 'sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=';
const STYLE_SRI = 'sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=';

export interface LatLng {
  lat: number;
  lng: number;
}

export interface LeafletMap {
  setView(center: [number, number], zoom: number): LeafletMap;
  on(event: 'click', handler: (e: { latlng: LatLng }) => void): LeafletMap;
  getZoom(): number;
  invalidateSize(): void;
  remove(): void;
}

export interface LeafletMarker {
  addTo(map: LeafletMap): LeafletMarker;
  setLatLng(point: [number, number]): LeafletMarker;
  getLatLng(): LatLng;
  on(event: 'dragend', handler: () => void): LeafletMarker;
}

export interface Leaflet {
  map(element: HTMLElement, options?: object): LeafletMap;
  tileLayer(url: string, options?: object): { addTo(map: LeafletMap): unknown };
  marker(point: [number, number], options?: object): LeafletMarker;
  divIcon(options: object): unknown;
}

let loading: Promise<Leaflet> | null = null;

export function loadLeaflet(): Promise<Leaflet> {
  loading ??= new Promise<Leaflet>((resolve, reject) => {
    const style = document.createElement('link');
    style.rel = 'stylesheet';
    style.href = `${BASE}/leaflet.css`;
    style.integrity = STYLE_SRI;
    style.crossOrigin = '';
    document.head.append(style);

    const script = document.createElement('script');
    script.src = `${BASE}/leaflet.js`;
    script.integrity = SCRIPT_SRI;
    script.crossOrigin = '';
    script.onload = () => resolve((window as unknown as { L: Leaflet }).L);
    script.onerror = () => {
      loading = null;
      script.remove();
      reject(new Error('leaflet'));
    };
    document.head.append(script);
  });
  return loading;
}
