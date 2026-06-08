import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth.service';
import { APP_CONFIG } from '../app-config.token';

/**
 * Token yoksa veya expired ise login'e yönlendirir.
 * Public-app'te (authEnabled=false) hep true döner — yine de takmana gerek yok.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const config = inject(APP_CONFIG);

  if (!config.authEnabled) return true;
  if (auth.isAuthenticated()) return true;

  router.navigate([config.loginPath ?? '/login'], {
    queryParams: { returnUrl: state.url }
  });
  return false;
};
