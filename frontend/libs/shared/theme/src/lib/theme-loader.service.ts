import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, catchError, map } from 'rxjs';
import { Theme } from './theme.model';

/**
 * Tema yükleyici. İki kaynaktan tema çekebilir:
 * 1. Static assets: /assets/themes/<name>.json (build-time temalar)
 * 2. Backend API: /api/public/themes/<name> (runtime, multi-tenant)
 *
 * Önce backend'i dener (varsa), olmadığında assets'e düşer.
 */
@Injectable({ providedIn: 'root' })
export class ThemeLoaderService {
  private http = inject(HttpClient);

  /**
   * `themeName` (ör. 'customer-acme-dark') için tema yükle.
   * `apiThemesUrl` opsiyonel — verilirse önce backend denenir.
   */
  load(themeName: string, apiThemesUrl?: string): Observable<Theme> {
    if (apiThemesUrl) {
      const url = `${apiThemesUrl.replace(/\/$/, '')}/${themeName}`;
      return this.http.get<Theme>(url).pipe(
        catchError(() => this.loadFromAssets(themeName))
      );
    }
    return this.loadFromAssets(themeName);
  }

  /** Tüm mevcut temaları listeler (assets/themes/_index.json). */
  list(): Observable<Array<{ name: string; displayName: string; mode: string }>> {
    return this.http.get<Array<{ name: string; displayName: string; mode: string }>>(
      '/assets/themes/_index.json'
    ).pipe(catchError(() => of([])));
  }

  private loadFromAssets(themeName: string): Observable<Theme> {
    return this.http.get<Theme>(`/assets/themes/${themeName}.json`);
  }
}
