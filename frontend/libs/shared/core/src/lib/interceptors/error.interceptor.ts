import { HttpInterceptorFn, HttpErrorResponse, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError, Observable, of } from 'rxjs';
import { AuthService } from '../auth.service';
import { APP_CONFIG } from '../app-config.token';
import { HttpEvent } from '@angular/common/http';

// Refresh isteği zaten gidiyor mu? Aynı anda 10 paralel istek 401 alırsa
// 10 ayrı refresh çağırmasın diye flag tutuyoruz.
let isRefreshing = false;

/**
 * 401 → refresh token ile yeni access dene → orijinal isteği tekrar gönder.
 * Refresh başarısız → logout + /login.
 * 403 → access-denied.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const config = inject(APP_CONFIG);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && config.authEnabled) {
        return handle401(req, next, auth, router, config.loginPath ?? '/login');
      }
      if (err.status === 403) {
        router.navigateByUrl('/access-denied');
      }
      return throwError(() => err);
    })
  );
};

function handle401(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  auth: AuthService,
  router: Router,
  loginPath: string,
): Observable<HttpEvent<unknown>> {
  // /auth/login veya /auth/refresh'in 401'ini retry'lama — infinite loop
  if (req.url.includes('/auth/')) {
    auth.logout();
    router.navigateByUrl(loginPath);
    return throwError(() => new Error('Auth endpoint failed'));
  }

  if (isRefreshing) {
    // Başka bir refresh çağrısı sürüyor — bu isteği iptal et
    return throwError(() => new Error('Refresh in progress'));
  }

  isRefreshing = true;

  return auth.refresh().pipe(
    switchMap(tokens => {
      isRefreshing = false;
      if (!tokens) {
        router.navigateByUrl(loginPath);
        return throwError(() => new Error('Refresh failed'));
      }
      // Yeni token ile orijinal isteği tekrarla
      const retryReq = req.clone({
        setHeaders: { Authorization: `Bearer ${tokens.accessToken}` }
      });
      return next(retryReq);
    }),
    catchError(err => {
      isRefreshing = false;
      auth.logout();
      router.navigateByUrl(loginPath);
      return throwError(() => err);
    })
  );
}
