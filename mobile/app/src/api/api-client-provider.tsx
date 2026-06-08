import { createContext, useContext, useMemo, type PropsWithChildren } from 'react';
import { ApiClient } from './api-client';
import { useAppConfig } from '@/config/app-config';
import { defaultSecureStorage, type SecureStorage } from '@/utils/secure-storage';
import { useAuthStore } from '@/auth/auth-store';

const ApiClientContext = createContext<ApiClient | null>(null);

/**
 * ApiClient'i ContextProvider içinde örnekler.
 * 401 olduğunda authStore.logout() tetikler — circular dependency yok.
 *
 * Test'lerde mock storage ve mock config ile sarmalanabilir.
 */
export function ApiClientProvider({
  storage = defaultSecureStorage,
  children,
}: PropsWithChildren<{ storage?: SecureStorage }>) {
  const config = useAppConfig();
  const logout = useAuthStore(s => s.logout);

  const client = useMemo(
    () =>
      new ApiClient({
        config,
        storage,
        onUnauthorized: () => {
          // Token expired/invalid — auth state'ini temizle
          void logout();
        },
      }),
    [config, storage, logout],
  );

  return <ApiClientContext.Provider value={client}>{children}</ApiClientContext.Provider>;
}

export function useApiClient(): ApiClient {
  const c = useContext(ApiClientContext);
  if (!c) throw new Error('useApiClient: ApiClientProvider yok.');
  return c;
}
