import { Injectable, inject, signal, OnDestroy, effect } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel, HubConnectionState } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from './auth.service';
import { APP_CONFIG } from './app-config.token';

/**
 * SignalR notification service.
 *
 * Backend hub: /hubs/notifications
 * Auth: JWT token query string olarak (WebSocket Authorization header limit)
 *
 * Otomatik yeniden bağlanma (0s, 2s, 10s, 30s, sonra her dakika).
 * Token refresh sırasında bağlantı yeniden kurulur.
 *
 * Bu service singleton — uygulama açılınca AuthService login state'ini
 * izler ve hub'a bağlanır/çıkar.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService implements OnDestroy {
  private auth = inject(AuthService);
  private config = inject(APP_CONFIG);

  private connection?: HubConnection;

  readonly state = signal<HubConnectionState>(HubConnectionState.Disconnected);

  // Event stream'leri — komponentler subscribe olur
  readonly sessionRevoked$ = new Subject<{ sessionId: number; reason: string }>();
  readonly auditEvent$ = new Subject<AuditEventDto>();
  readonly userNotification$ = new Subject<{ title: string; message: string; severity?: string }>();

  constructor() {
    effect(() => {
      if (this.auth.isAuthenticated()) {
        void this.connect();
      } else {
        void this.disconnect();
      }
    });
  }

  async connect(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) return;

    const token = this.auth.accessToken();
    if (!token) return;  // login değilse bağlanma

    // hubBaseUrl: apiRootUrl'in /api/v1 kısmı olmadan
    const hubUrl = this.config.apiRootUrl.replace(/\/api\/v\d+$/, '') + '/hubs/notifications';

    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.auth.accessToken() ?? '',
      })
      .withAutomaticReconnect([0, 2000, 10000, 30000, 60000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('SessionRevoked', (e: { sessionId: number; reason: string }) => {
      this.sessionRevoked$.next(e);
      // Eğer mevcut session revoke olduysa → logout
      const currentSessionId = this.auth.currentSessionId();
      if (currentSessionId === e.sessionId) {
        this.auth.handleForcedLogout(`Oturum kapatıldı: ${e.reason}`);
      }
    });

    this.connection.on('AuditEvent', (e: AuditEventDto) => {
      this.auditEvent$.next(e);
    });

    this.connection.on('UserNotification', (e: { title: string; message: string; severity?: string }) => {
      this.userNotification$.next(e);
    });

    this.connection.onreconnecting(() => this.state.set(HubConnectionState.Reconnecting));
    this.connection.onreconnected(() => this.state.set(HubConnectionState.Connected));
    this.connection.onclose(() => this.state.set(HubConnectionState.Disconnected));

    try {
      await this.connection.start();
      this.state.set(HubConnectionState.Connected);
    } catch (err) {
      console.warn('SignalR connection failed', err);
      this.state.set(HubConnectionState.Disconnected);
    }
  }

  async disconnect(): Promise<void> {
    if (!this.connection) return;
    await this.connection.stop();
    this.connection = undefined;
    this.state.set(HubConnectionState.Disconnected);
  }

  ngOnDestroy(): void {
    this.disconnect();
  }
}

export interface AuditEventDto {
  id: number;
  timestamp: string;
  audience: string;
  actorCustomerId?: number;
  actorEmail?: string;
  action: string;
  targetType?: string;
  targetId?: number;
  outcome: string;
}
