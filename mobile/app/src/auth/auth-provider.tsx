import { createContext, useContext, useEffect, useMemo, type PropsWithChildren } from 'react';
import { useAppConfig } from '@/config/app-config';
import { defaultSecureStorage, type SecureStorage } from '@/utils/secure-storage';
import { authStore, useAuthStore } from './auth-store';
import type { LoginRequest, LoginResponse, ApiResponse } from '@/models';

const REFRESH_KEY_SUFFIX = '.refresh';

interface AuthActions {
  init: () => Promise<void>;
  login: (creds: LoginRequest) => Promise<LoginResult>;
  loginWithTwoFactor: (preAuthToken: string, code: string, useRecoveryCode?: boolean) => Promise<LoginResponse>;
  register: (req: { email: string; password: string; firstName?: string; lastName?: string }) => Promise<LoginResponse>;
  refresh: () => Promise<LoginResponse | null>;
  logout: () => Promise<void>;
  setToken: (token: string, refreshToken?: string) => Promise<void>;
}

/** Login sonucu: ya tokens ya da 2FA gerektiren preAuth bilgisi. */
export type LoginResult =
  | { type: 'success'; tokens: LoginResponse }
  | { type: 'two_factor_required'; preAuthToken: string };

const AuthActionsContext = createContext<AuthActions | null>(null);

interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: {
    entityId: string;
    email: string;
    firstName?: string;
    lastName?: string;
    roles: string[];
  };
}

export function AuthProvider({
  storage = defaultSecureStorage,
  children,
}: PropsWithChildren<{ storage?: SecureStorage }>) {
  const config = useAppConfig();

  const actions = useMemo<AuthActions>(() => {
    const refreshKey = config.tokenStorageKey + REFRESH_KEY_SUFFIX;
    const baseUrl = config.apiBaseUrl.replace(/\/$/, '');

    async function callAuth(path: string, body: unknown): Promise<AuthResponse> {
      const res = await fetch(`${baseUrl}/public/auth/${path}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        const problem = await res.json().catch(() => ({ title: 'Auth failed' }));
        throw new Error(problem.detail ?? problem.title ?? 'Auth failed');
      }
      const json = (await res.json()) as ApiResponse<AuthResponse>;
      return json.data;
    }

    async function applyTokens(r: AuthResponse): Promise<LoginResponse> {
      await storage.setItem(config.tokenStorageKey, r.accessToken);
      await storage.setItem(refreshKey, r.refreshToken);
      authStore.setToken(r.accessToken);
      return { token: r.accessToken, expiresAt: r.accessTokenExpiresAt };
    }

    return {
      async init() {
        try {
          const token = await storage.getItem(config.tokenStorageKey);
          if (token) {
            authStore.setToken(token);
          } else {
            authStore.clear();
          }
        } catch {
          authStore.clear();
        }
      },

      async login(creds) {
        // Backend ya tokens ya da {requiresTwoFactor, preAuthToken} döner
        const res = await fetch(`${baseUrl}/public/auth/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(creds),
        });
        if (!res.ok) {
          const problem = await res.json().catch(() => ({ title: 'Login failed' }));
          throw new Error(problem.detail ?? problem.title ?? 'Login failed');
        }
        const json = await res.json() as { data: any };

        if (json.data?.requiresTwoFactor) {
          return { type: 'two_factor_required', preAuthToken: json.data.preAuthToken };
        }

        const tokens = await applyTokens(json.data as AuthResponse);
        return { type: 'success', tokens };
      },

      async loginWithTwoFactor(preAuthToken, code, useRecoveryCode = false) {
        const r = await callAuth('login/2fa', {
          preAuthToken,
          code,
          useRecoveryCode,
        });
        return applyTokens(r);
      },

      async register(req) {
        const r = await callAuth('register', req);
        return applyTokens(r);
      },

      async refresh() {
        try {
          const refreshToken = await storage.getItem(refreshKey);
          if (!refreshToken) {
            await actions.logout();
            return null;
          }
          const r = await callAuth('refresh', { refreshToken });
          return applyTokens(r);
        } catch {
          await actions.logout();
          return null;
        }
      },

      async logout() {
        const refreshToken = await storage.getItem(refreshKey);

        // Best-effort revoke
        if (refreshToken) {
          fetch(`${baseUrl}/public/auth/logout`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken }),
          }).catch(() => {});
        }

        await storage.removeItem(config.tokenStorageKey);
        await storage.removeItem(refreshKey);
        authStore.clear();
      },

      async setToken(token, refreshToken) {
        await storage.setItem(config.tokenStorageKey, token);
        if (refreshToken) await storage.setItem(refreshKey, refreshToken);
        authStore.setToken(token);
      },
    };
  }, [config, storage]);

  // AuthStore'un logout fonksiyonunu bağla (ApiClient 401'de çağıracak)
  useEffect(() => {
    authStore.logout = actions.logout;
  }, [actions]);

  // Boot'ta token'ı yükle
  useEffect(() => {
    void actions.init();
  }, [actions]);

  return <AuthActionsContext.Provider value={actions}>{children}</AuthActionsContext.Provider>;
}

export function useAuthActions(): AuthActions {
  const a = useContext(AuthActionsContext);
  if (!a) throw new Error('useAuthActions: AuthProvider yok.');
  return a;
}

export function useAuth() {
  const state = useAuthStore(s => s);
  const actions = useAuthActions();
  return { ...state, ...actions };
}

export { useAuthStore };
