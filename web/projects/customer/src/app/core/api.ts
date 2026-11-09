import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable, InjectionToken } from '@angular/core';
import { PageMeta } from '@ot/api';
import { catchError, map, Observable, of } from 'rxjs';

// Trình duyệt gọi cùng tên miền; render phía server ghi đè bằng địa chỉ API nội bộ.
// Phải là địa chỉ đầy đủ để khớp khóa bộ nhớ đệm mà server đã ghi (HTTP_TRANSFER_CACHE_ORIGIN_MAP)
export const API_ORIGIN = new InjectionToken<string>('API_ORIGIN', { factory: () => inject(DOCUMENT).location.origin });

interface Envelope<T> {
  data: T;
}

export interface Page<T> {
  data: T[];
  meta: PageMeta;
}

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);
  private readonly origin = inject(API_ORIGIN);

  get<T>(path: string, params?: Record<string, string | number>): Observable<T> {
    return this.http
      .get<Envelope<T>>(`${this.origin}/api/v1/${path}`, { params: new HttpParams({ fromObject: params ?? {} }) })
      .pipe(map((r) => r.data));
  }

  getPage<T>(path: string, params: Record<string, string | number>): Observable<Page<T>> {
    return this.http.get<Page<T>>(`${this.origin}/api/v1/${path}`, { params: new HttpParams({ fromObject: params }) });
  }

  post<T = void>(path: string, body: unknown = {}, headers?: Record<string, string>): Observable<T> {
    return this.http.post<Envelope<T> | null>(`${this.origin}/api/v1/${path}`, body, { headers }).pipe(map((r) => r?.data as T));
  }

  put<T = void>(path: string, body: unknown): Observable<T> {
    return this.http.put<Envelope<T> | null>(`${this.origin}/api/v1/${path}`, body).pipe(map((r) => r?.data as T));
  }

  delete(path: string): Observable<void> {
    return this.http.delete<void>(`${this.origin}/api/v1/${path}`);
  }

  // Một khối lỗi không làm hỏng cả trang: trả giá trị thay thế
  getOr<T>(path: string, fallback: T, params?: Record<string, string | number>): Observable<T> {
    return this.get<T>(path, params).pipe(catchError(() => of(fallback)));
  }
}
