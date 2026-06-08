import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@SearchConsoleApp/shared/core';

export interface SearchConsolePropertyDto {
  siteUrl: string;
  permissionLevel: string;
}

export interface SearchConsoleStatusDto {
  connected: boolean;
  properties: SearchConsolePropertyDto[];
}

@Injectable({ providedIn: 'root' })
export class SearchConsoleApiService {
  private api = inject(ApiClient);

  getStatus(): Observable<SearchConsoleStatusDto> {
    return this.api.get<SearchConsoleStatusDto>('search-console/status', { audience: 'web' });
  }

  getAuthorizeUrl(returnUrl: string): Observable<{ authorizeUrl: string }> {
    return this.api.get<{ authorizeUrl: string }>('search-console/authorize', {
      audience: 'web',
      params: { returnUrl },
    });
  }

  completeCallback(code: string, state: string): Observable<{ ok: boolean }> {
    return this.api.post<{ ok: boolean }>(
      'search-console/callback',
      { code, state },
      { audience: 'web' },
    );
  }

  disconnect(): Observable<{ ok: boolean }> {
    return this.api.delete<{ ok: boolean }>('search-console', { audience: 'web' });
  }
}
