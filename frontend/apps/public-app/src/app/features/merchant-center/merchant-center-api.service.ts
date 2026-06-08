import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@SearchConsoleApp/shared/core';
import { MerchantCenterStatusDto } from './merchant-center.models';

@Injectable({ providedIn: 'root' })
export class MerchantCenterApiService {
  private api = inject(ApiClient);

  getStatus(): Observable<MerchantCenterStatusDto> {
    return this.api.get<MerchantCenterStatusDto>('merchant-center/status', { audience: 'web' });
  }

  getAuthorizeUrl(returnUrl: string): Observable<{ authorizeUrl: string }> {
    return this.api.get<{ authorizeUrl: string }>('merchant-center/authorize', {
      audience: 'web',
      params: { returnUrl },
    });
  }

  completeCallback(code: string, state: string): Observable<{ ok: boolean }> {
    return this.api.post<{ ok: boolean }>(
      'merchant-center/callback',
      { code, state },
      { audience: 'web' },
    );
  }

  disconnect(): Observable<{ ok: boolean }> {
    return this.api.delete<{ ok: boolean }>('merchant-center', { audience: 'web' });
  }
}
