import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideHttpClient, withInterceptors, HttpInterceptorFn } from '@angular/common/http';
import { AppConfig, APP_CONFIG } from './app-config.token';
import { authInterceptor } from './interceptors/auth.interceptor';
import { errorInterceptor } from './interceptors/error.interceptor';

/**
 * Tüm shared/core kurulumunu tek satıra indiren provider.
 * Her uygulama app.config.ts'inde sadece bunu çağırır:
 *
 *   provideSharedCore({ apiBaseUrl: ..., tokenStorageKey: ..., ... })
 *
 * Auth kapalıysa interceptor'lar pass-through olur (kodu paylaşmak için sorun yok).
 * Ekstra interceptor eklemek istersen `extraInterceptors` ile geçebilirsin.
 */
export function provideSharedCore(
  config: AppConfig,
  extraInterceptors: HttpInterceptorFn[] = []
): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: APP_CONFIG, useValue: config },
    provideHttpClient(
      withInterceptors([authInterceptor, errorInterceptor, ...extraInterceptors])
    )
  ]);
}
