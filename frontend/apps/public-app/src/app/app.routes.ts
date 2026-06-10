import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent)
  },
  {
    path: 'merchant-center',
    loadComponent: () =>
      import('./features/merchant-center/merchant-center.component').then(m => m.MerchantCenterComponent),
  },
  {
    path: 'price-benchmark',
    loadComponent: () =>
      import('./features/price-benchmark/price-benchmark.component').then(m => m.PriceBenchmarkComponent),
  },
  {
    path: 'auth/external/google/callback',
    loadComponent: () =>
      import('./features/auth/google-auth-callback.component').then(m => m.GoogleAuthCallbackComponent),
  },
  {
    path: 'auth/search-console/callback',
    loadComponent: () =>
      import('./features/auth/search-console-callback.component').then(m => m.SearchConsoleCallbackComponent),
  },
  {
    path: 'auth/merchant-center/callback',
    loadComponent: () =>
      import('./features/merchant-center/merchant-center-callback.component').then(m => m.MerchantCenterCallbackComponent),
  },
];
