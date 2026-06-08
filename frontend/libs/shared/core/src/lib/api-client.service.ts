import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ApiResponse } from '@SearchConsoleApp/shared/models';
import { APP_CONFIG, Audience } from './app-config.token';

interface RequestOptions {
  audience?: Audience;
  params?: Record<string, string | number | boolean>;
}

/**
 * Audience-aware HTTP client. Mobile tarafıyla aynı pattern.
 *
 * Default audience config'ten gelir (web-app → 'web'), method argümanıyla
 * override edilebilir. Bu sayede web-app içinden public tema endpoint'i
 * çağırmak mümkün:
 *
 *   apiClient.get('themes', { audience: 'public' })  → /api/public/themes
 *   apiClient.get('me')                               → /api/web/me
 *
 * Backend'in `/api/{audience}/*` URL yapısı korunur, ama frontend tek
 * client kullanır — URL inşası unutulmaz/yanlış yazılmaz.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  get<T>(path: string, options?: RequestOptions): Observable<T> {
    return this.http
      .get<ApiResponse<T>>(this.url(path, options?.audience), { params: this.params(options?.params) })
      .pipe(map(r => r.data));
  }

  post<T>(path: string, body: unknown, options?: RequestOptions): Observable<T> {
    return this.http
      .post<ApiResponse<T>>(this.url(path, options?.audience), body)
      .pipe(map(r => r.data));
  }

  put<T>(path: string, body: unknown, options?: RequestOptions): Observable<T> {
    return this.http
      .put<ApiResponse<T>>(this.url(path, options?.audience), body)
      .pipe(map(r => r.data));
  }

  patch<T>(path: string, body: unknown, options?: RequestOptions): Observable<T> {
    return this.http
      .patch<ApiResponse<T>>(this.url(path, options?.audience), body)
      .pipe(map(r => r.data));
  }

  delete<T = void>(path: string, options?: RequestOptions): Observable<T> {
    return this.http
      .delete<ApiResponse<T>>(this.url(path, options?.audience))
      .pipe(map(r => r.data));
  }

  private url(path: string, audience?: Audience): string {
    const base = this.config.apiRootUrl.replace(/\/$/, '');
    const aud = audience ?? this.config.defaultAudience;
    const cleanPath = path.replace(/^\//, '');
    return `${base}/${aud}/${cleanPath}`;
  }

  private params(params?: Record<string, string | number | boolean>): HttpParams {
    let hp = new HttpParams();
    if (!params) return hp;
    for (const [k, v] of Object.entries(params)) {
      if (v !== undefined && v !== null) hp = hp.set(k, String(v));
    }
    return hp;
  }
}
