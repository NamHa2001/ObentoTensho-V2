import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

export type Params = Record<string, string | number | boolean | null | undefined>;

function query(params?: Params): HttpParams {
  let result = new HttpParams();
  for (const [k, v] of Object.entries(params ?? {})) {
    if (v !== null && v !== undefined && v !== '') {
      result = result.set(k, String(v));
    }
  }

  return result;
}

function headers(version?: string | null): HttpHeaders {
  return version ? new HttpHeaders({ 'If-Match': `"${version}"` }) : new HttpHeaders();
}

const BASE = '/api/v1/admin/';

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  // Trả nguyên { data, meta } để bảng biết tổng số dòng
  raw<T>(path: string, params?: Params): Observable<{ data: T; meta?: { page: number; pageSize: number; total: number } }> {
    return this.http.get<{ data: T; meta?: { page: number; pageSize: number; total: number } }>(BASE + path, { params: query(params) });
  }

  get<T>(path: string, params?: Params): Observable<T> {
    return this.raw<T>(path, params).pipe(map((r) => r.data));
  }

  post<T = unknown>(path: string, body: unknown = {}, version?: string | null): Observable<T> {
    return this.http.post<{ data: T } | null>(BASE + path, body, { headers: headers(version) }).pipe(map((r) => r?.data as T));
  }

  postWith<T = unknown>(path: string, body: unknown, extra?: Record<string, string>): Observable<T> {
    return this.http.post<{ data: T } | null>(BASE + path, body, { headers: extra }).pipe(map((r) => r?.data as T));
  }

  postBlob(path: string, body: unknown): Observable<HttpResponse<Blob>> {
    return this.http.post(BASE + path, body, { responseType: 'blob', observe: 'response' });
  }

  put<T = unknown>(path: string, body: unknown, version?: string | null): Observable<T> {
    return this.http.put<{ data: T } | null>(BASE + path, body, { headers: headers(version) }).pipe(map((r) => r?.data as T));
  }

  patch<T = unknown>(path: string, body: unknown = {}, version?: string | null): Observable<T> {
    return this.http.patch<{ data: T } | null>(BASE + path, body, { headers: headers(version) }).pipe(map((r) => r?.data as T));
  }

  // Cài đặt công khai (tên, logo quán) nằm ngoài nhánh admin
  publicSettings(): Observable<Record<string, string>> {
    return this.http.get<{ data: Record<string, string> }>('/api/v1/public/settings').pipe(map((r) => r.data));
  }

  delete(path: string, version?: string | null): Observable<void> {
    return this.http.delete<void>(BASE + path, { headers: headers(version) });
  }

  download(path: string, params?: Params): Observable<HttpResponse<Blob>> {
    return this.http.get(BASE + path, { params: query(params), responseType: 'blob', observe: 'response' });
  }
}

export function saveBlob(response: HttpResponse<Blob>, fallback: string): void {
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)/i.exec(response.headers.get('content-disposition') ?? '');
  const link = document.createElement('a');
  link.href = URL.createObjectURL(response.body as Blob);
  link.download = match ? decodeURIComponent(match[1]) : fallback;
  link.click();
  URL.revokeObjectURL(link.href);
}
