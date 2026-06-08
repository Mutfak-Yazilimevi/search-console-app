import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth.service';
import { APP_CONFIG } from '../app-config.token';

/**
 * AppConfig'teki `requiredRole` ile karşılaştırır.
 * `*` ise role check yok → her authenticated kullanıcı geçer.
 * Admin-app `requiredRole: 'admin'` set eder, web-app `'user'` set eder.
 *
 * Login değilse authGuard önce yakalar — bu guard zincirin ikinci halkası olmalı.
 */
export const roleGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const config = inject(APP_CONFIG);

  if (config.requiredRole === '*') return true;
  if (auth.hasRole(config.requiredRole)) return true;

  router.navigateByUrl('/access-denied');
  return false;
};
