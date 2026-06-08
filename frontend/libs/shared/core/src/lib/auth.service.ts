import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, map, throwError, catchError } from 'rxjs';
import {
  AuthTokens, JwtPayload, LoginRequest, RegisterRequest, RefreshRequest,
  ApiResponse, UserInfo
} from '@SearchConsoleApp/shared/models';
import { APP_CONFIG } from './app-config.token';

const REFRESH_TOKEN_KEY_SUFFIX = '.refresh';

/**
 * Login/logout/refresh state. Signal-based.
 *
 * Auth endpoint'leri her zaman /api/public/auth/* — bu sabit, config'in
 * audience'ından bağımsız (login zaten public).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private _accessToken = signal<string | null>(this.loadToken());
  private _user = signal<UserInfo | null>(this.loadUser());

  readonly accessToken = this._accessToken.asReadonly();
  readonly user = this._user.asReadonly();

  readonly isAuthenticated = computed(() => {
    const t = this._accessToken();
    if (!t) return false;
    try {
      const payload = this.decode(t);
      return payload.exp * 1000 > Date.now();
    } catch { return false; }
  });

  readonly roles = computed<string[]>(() => this._user()?.roles ?? []);

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  /** JWT `sid` claim — SignalR session revoke eşleştirmesi için */
  currentSessionId(): number | null {
    const token = this._accessToken();
    if (!token) return null;
    try {
      const payload = this.decode(token);
      const sid = (payload as JwtPayload & { sid?: string | number }).sid;
      if (sid == null) return null;
      return typeof sid === 'number' ? sid : Number(sid);
    } catch {
      return null;
    }
  }

  handleForcedLogout(_reason?: string): void {
    this.logout();
  }

  login(credentials: LoginRequest): Observable<AuthTokens> {
    if (!this.config.authEnabled) {
      return throwError(() => new Error('Auth disabled'));
    }
    return this.http
      .post<ApiResponse<AuthTokens>>(this.authUrl('login'), credentials)
      .pipe(map(r => r.data), tap(tokens => this.applyTokens(tokens)));
  }

  register(req: RegisterRequest): Observable<AuthTokens> {
    if (!this.config.authEnabled) {
      return throwError(() => new Error('Auth disabled'));
    }
    return this.http
      .post<ApiResponse<AuthTokens>>(this.authUrl('register'), req)
      .pipe(map(r => r.data), tap(tokens => this.applyTokens(tokens)));
  }

  refresh(): Observable<AuthTokens | null> {
    const refreshToken = this.loadRefreshToken();
    if (!refreshToken) {
      this.logout();
      return throwError(() => new Error('No refresh token'));
    }
    return this.http
      .post<ApiResponse<AuthTokens>>(this.authUrl('refresh'), { refreshToken } as RefreshRequest)
      .pipe(
        map(r => r.data),
        tap(tokens => this.applyTokens(tokens)),
        catchError(err => {
          this.logout();
          return throwError(() => err);
        })
      );
  }

  logout(): void {
    const refreshToken = this.loadRefreshToken();

    // Best-effort revoke
    if (refreshToken) {
      this.http.post(this.authUrl('logout'), { refreshToken }).subscribe({ error: () => {} });
    }

    localStorage.removeItem(this.config.tokenStorageKey);
    localStorage.removeItem(this.config.tokenStorageKey + REFRESH_TOKEN_KEY_SUFFIX);
    localStorage.removeItem(this.config.tokenStorageKey + '.user');
    this._accessToken.set(null);
    this._user.set(null);
  }

  // === Internals ===

  /** Auth endpoint'leri her zaman public audience'da */
  private authUrl(op: string): string {
    const base = this.config.apiRootUrl.replace(/\/$/, '');
    return `${base}/public/auth/${op}`;
  }

  private applyTokens(tokens: AuthTokens): void {
    localStorage.setItem(this.config.tokenStorageKey, tokens.accessToken);
    localStorage.setItem(this.config.tokenStorageKey + REFRESH_TOKEN_KEY_SUFFIX, tokens.refreshToken);
    localStorage.setItem(this.config.tokenStorageKey + '.user', JSON.stringify(tokens.user));
    this._accessToken.set(tokens.accessToken);
    this._user.set(tokens.user);
  }

  private loadToken(): string | null {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem(this.config.tokenStorageKey);
  }

  private loadRefreshToken(): string | null {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem(this.config.tokenStorageKey + REFRESH_TOKEN_KEY_SUFFIX);
  }

  private loadUser(): UserInfo | null {
    if (typeof localStorage === 'undefined') return null;
    const raw = localStorage.getItem(this.config.tokenStorageKey + '.user');
    if (!raw) return null;
    try { return JSON.parse(raw); } catch { return null; }
  }

  private decode(token: string): JwtPayload {
    const part = token.split('.')[1];
    return JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')));
  }
}
