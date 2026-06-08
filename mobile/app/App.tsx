import { useEffect, useState } from 'react';
import { StatusBar } from 'expo-status-bar';
import * as SplashScreen from 'expo-splash-screen';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { QueryClientProvider } from '@tanstack/react-query';
import Constants from 'expo-constants';

import { AppConfigProvider, type AppConfig } from '@/config/app-config';
import { AuthProvider } from '@/auth/auth-provider';
import { ApiClientProvider } from '@/api/api-client-provider';
import { ThemeProvider } from '@/theme/theme-provider';
import { AppErrorBoundary } from '@/components/error-boundary';
import { RootNavigator } from '@/navigation/root-navigator';
import { createQueryClient } from '@/api/query-client';
import { initI18n } from '@/i18n';
import { initSentry } from '@/utils/sentry';

void SplashScreen.preventAutoHideAsync();

// Config — Expo Constants veya env'den okunabilir
const config: AppConfig = {
  apiBaseUrl: Constants.expoConfig?.extra?.apiBaseUrl ?? 'http://localhost:5000/api/v1',
  tokenStorageKey: 'SearchConsoleApp.mobile.token',
  sentryDsn: Constants.expoConfig?.extra?.sentryDsn ?? null,
  production: !__DEV__,
  appName: 'SearchConsoleApp',
  defaultLocale: 'en',
  defaultTheme: 'default-light',
};

// Boot-time setup
initSentry(config.sentryDsn, config.production);
initI18n(config.defaultLocale);

const queryClient = createQueryClient();

/**
 * Provider hiyerarşisi (dıştan içe):
 * - GestureHandler         → reanimated/navigation animasyonları
 * - ErrorBoundary          → tüm app çökmesin
 * - AppConfig              → diğer provider'lar config'i okur
 * - QueryClient            → react-query
 * - Theme                  → ApiClient/Auth tema'ya muhtaç değil ama UI muhtaç
 * - Auth                   → ApiClient onUnauthorized → authStore.logout
 * - ApiClient              → Auth'a bağlı
 * - RootNavigator          → auth state'ine göre stack seçer
 */
export default function App() {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    // Bootstrap tamamlandığında splash'i kaldır.
    // AuthProvider.init() arka planda çalışıyor; navigation isLoading'i gösterir.
    const id = setTimeout(() => {
      void SplashScreen.hideAsync();
      setReady(true);
    }, 100);
    return () => clearTimeout(id);
  }, []);

  if (!ready) return null;

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <AppErrorBoundary>
        <AppConfigProvider config={config}>
          <QueryClientProvider client={queryClient}>
            <ThemeProvider defaultThemeName={config.defaultTheme}>
              <AuthProvider>
                <ApiClientProvider>
                  <StatusBar style="auto" />
                  <RootNavigator />
                </ApiClientProvider>
              </AuthProvider>
            </ThemeProvider>
          </QueryClientProvider>
        </AppConfigProvider>
      </AppErrorBoundary>
    </GestureHandlerRootView>
  );
}
