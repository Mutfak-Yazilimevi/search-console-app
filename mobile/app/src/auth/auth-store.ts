import { useSyncExternalStore } from 'react';
import { jwtDecode } from 'jwt-decode';
import type { JwtPayload } from '@/models';

export interface AuthState {
  token: string | null;
  user: JwtPayload | null;
  isAuthenticated: boolean;
  roles: string[];
  /** Boot sırasında token yükleniyor mu? false olunca auth state stabilize olmuştur */
  isLoading: boolean;
}

const initialState: AuthState = {
  token: null,
  user: null,
  isAuthenticated: false,
  roles: [],
  isLoading: true,
};

/**
 * Minimal store implementasyonu (Zustand'sız).
 * useSyncExternalStore native React API'sini kullanır → tearing-safe.
 *
 * Bu store ApiClient ve AuthProvider arasındaki köprüdür.
 * Token set/clear işlemleri AuthProvider'da yapılır (storage ile birlikte).
 */
type Listener = (state: AuthState) => void;

class AuthStore {
  private state: AuthState = initialState;
  private listeners = new Set<Listener>();

  getState = (): AuthState => this.state;

  setState = (partial: Partial<AuthState>): void => {
    this.state = { ...this.state, ...partial };
    this.listeners.forEach(l => l(this.state));
  };

  setToken = (token: string): void => {
    try {
      const user = jwtDecode<JwtPayload>(token);
      const isValid = user.exp * 1000 > Date.now();
      if (!isValid) {
        this.clear();
        return;
      }
      const role = user.role;
      const roles = Array.isArray(role) ? role : role ? [role] : [];
      this.setState({
        token,
        user,
        isAuthenticated: true,
        roles,
        isLoading: false,
      });
    } catch {
      this.clear();
    }
  };

  clear = (): void => {
    this.setState({
      token: null,
      user: null,
      isAuthenticated: false,
      roles: [],
      isLoading: false,
    });
  };

  // Logout callback — AuthProvider'da SecureStore temizliği için override edilir
  logout = async (): Promise<void> => {
    this.clear();
  };

  subscribe = (listener: Listener): (() => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
}

// Tek instance — ama burada AppConfig'e ihtiyaç YOK, state'i tutuyor sadece.
// Test'lerde resetForTesting() ile temizlenebilir.
export const authStore = new AuthStore();

/**
 * Selector pattern: sadece izlenen alan değişince re-render.
 *
 *   const isAuth = useAuthStore(s => s.isAuthenticated);  // sadece bu değişince
 *   const user = useAuthStore(s => s.user);
 */
export function useAuthStore<T>(selector: (state: AuthState) => T): T {
  return useSyncExternalStore(
    authStore.subscribe,
    () => selector(authStore.getState()),
    () => selector(initialState), // SSR/server-snapshot
  );
}

/** Test helper */
export function __resetAuthStoreForTesting(): void {
  authStore.clear();
}
