import { ApplicationConfig, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideSharedCore } from '@SearchConsoleApp/shared/core';
import { provideTheme } from '@SearchConsoleApp/shared/theme';
import { routes } from './app.routes';
import { environment } from '../environments/environment';

/**
 * Audience: public
 *
 * ApiClient default'ı bu audience'a gider. Başka audience çağırmak için:
 *   apiClient.get('themes', { audience: 'public' })
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideRouter(routes),

    provideSharedCore({
      apiRootUrl: environment.apiRootUrl,
      defaultAudience: environment.defaultAudience,
      tokenStorageKey: 'SearchConsoleApp.public.token',
      appName: 'SearchConsoleApp Public',
      requiredRole: '*',
      authEnabled: true,
      loginPath: '/login',
      production: environment.production,
    }),

    provideTheme({
      defaultTheme: environment.defaultTheme,
    }),
  ]
};
