import { InjectionToken } from '@angular/core';

export type Audience = 'public' | 'web' | 'admin';

/**
 * Her Angular uygulaması bu config'i kendi environment.ts'inden sağlar.
 *
 * apiRootUrl artık AUDIENCE'sız kök URL — örn. http://localhost:5000/api
 * Default audience: bu app'in birincil hedef audience'ı. Login/me gibi
 * audience'sız çağrılar (sub bölgelerde) bu default'u kullanır.
 *
 * ApiClient method'ları opsiyonel audience argümanı alır:
 *   apiClient.get('me')                  → defaultAudience'a gider
 *   apiClient.get('themes', { audience: 'public' })  → /api/public/themes
 *
 * Bu sayede web-app'in içinden public endpoint'i (tema listesi) çağırmak
 * mümkün — manuel URL inşa etmek yerine.
 */
export interface AppConfig {
  /** Backend kök URL — audience suffix'siz. ör. http://localhost:5000/api */
  apiRootUrl: string;

  /** Bu app'in birincil audience'ı */
  defaultAudience: Audience;

  /** LocalStorage'da JWT için key */
  tokenStorageKey: string;

  /** App display name */
  appName: string;

  /** Required role: '*' = role check yok, 'user'/'admin' */
  requiredRole: '*' | 'user' | 'admin';

  /** Login path (UI), default '/login' */
  loginPath?: string;

  /** Auth açık mı? public-app için false */
  authEnabled: boolean;

  /** Login endpoint suffix — audience prefix'siz, örn. 'auth/login' */
  loginEndpoint?: string;

  /** Default tema adı */
  defaultTheme?: string;

  /** Production? */
  production: boolean;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');
