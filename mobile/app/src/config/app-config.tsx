import { createContext, useContext, type PropsWithChildren } from 'react';

/**
 * Uygulama yapılandırması. Tek mobile app içinde üç audience (public/web/admin)
 * vardır — config tüm audience'lara aynı backend kökü ile ulaşır.
 */
export interface AppConfig {
  /**
   * Backend API kök URL'i (audience suffix YOK).
   * Örn: 'http://localhost:5000/api/v1' veya 'https://api.SearchConsoleApp.com/api'.
   * apiClient kendisi /public, /web, /admin suffix'lerini ekler.
   */
  apiBaseUrl: string;

  /** Token'ın SecureStore'da saklanacağı key */
  tokenStorageKey: string;

  /** Sentry DSN — null ise crash reporting devre dışı */
  sentryDsn: string | null;

  /** Production mode */
  production: boolean;

  /** Uygulama display name */
  appName: string;

  /** Default dil (kullanıcı henüz seçim yapmadıysa) */
  defaultLocale: string;

  /** Default tema adı */
  defaultTheme: string;
}

const AppConfigContext = createContext<AppConfig | null>(null);

/**
 * App.tsx en üst seviyede bir kez sarmalama yapılır.
 * Test'lerde mock config ile sarmalanır → singleton sıfırlama derdi yok.
 */
export function AppConfigProvider({
  config,
  children,
}: PropsWithChildren<{ config: AppConfig }>) {
  return (
    <AppConfigContext.Provider value={config}>
      {children}
    </AppConfigContext.Provider>
  );
}

export function useAppConfig(): AppConfig {
  const cfg = useContext(AppConfigContext);
  if (!cfg) {
    throw new Error(
      'useAppConfig: AppConfigProvider yok. Root layout\'ta AppConfigProvider ile sarmala.'
    );
  }
  return cfg;
}
