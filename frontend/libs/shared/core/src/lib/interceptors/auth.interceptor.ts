import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../auth.service';
import { APP_CONFIG } from '../app-config.token';

/**
 * Her giden isteğe Authorization: Bearer <token> ekler.
 * Auth kapalı app'lerde (public-app) bile import edilebilir; token yoksa pass-through.
 *
 * Kullanım: provideHttpClient(withInterceptors([authInterceptor])) — sadece auth gereken app'lerde.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(APP_CONFIG);
  if (!config.authEnabled) return next(req);

  const auth = inject(AuthService);
  const token = auth.accessToken();
  if (!token) return next(req);

  return next(req.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  }));
};
