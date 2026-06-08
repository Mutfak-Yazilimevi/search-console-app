import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api-client.service';
import { SessionDto, DeviceDto } from '@SearchConsoleApp/shared/models';

/**
 * Kullanıcının oturumları ve cihazları için API wrapper.
 * Her zaman 'web' audience'ına gider — admin için ayrı service var.
 */
@Injectable({ providedIn: 'root' })
export class SessionsService {
  private api = inject(ApiClient);

  /** Aktif oturumlar — `isCurrent` JWT'deki session ID ile match'leşir. */
  listActive(): Observable<SessionDto[]> {
    return this.api.get<SessionDto[]>('sessions', { audience: 'web' });
  }

  /** Geçmiş dahil son 100 oturum. */
  history(): Observable<SessionDto[]> {
    return this.api.get<SessionDto[]>('sessions/history', { audience: 'web' });
  }

  /** Belirli oturumu kapat ("uzaktan logout"). */
  revoke(sessionId: number): Observable<void> {
    return this.api.post<void>(`sessions/${sessionId}/revoke`, {}, { audience: 'web' });
  }

  /** Mevcut oturum hariç tüm diğer oturumları kapat. */
  revokeOthers(): Observable<void> {
    return this.api.post<void>('sessions/revoke-others', {}, { audience: 'web' });
  }

  /** Kullanıcının cihazları. */
  listDevices(): Observable<DeviceDto[]> {
    return this.api.get<DeviceDto[]>('devices', { audience: 'web' });
  }

  renameDevice(entityId: string, name: string): Observable<void> {
    return this.api.patch<void>(`devices/${entityId}/rename`, { name }, { audience: 'web' });
  }

  trustDevice(entityId: string, trusted: boolean): Observable<void> {
    return this.api.patch<void>(`devices/${entityId}/trust`, { trusted }, { audience: 'web' });
  }
}
