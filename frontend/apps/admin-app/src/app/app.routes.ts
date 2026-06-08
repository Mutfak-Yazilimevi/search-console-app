import { Routes } from '@angular/router';
import { authGuard, roleGuard } from '@SearchConsoleApp/shared/core';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'customers',
    canActivate: [authGuard, roleGuard],
    loadComponent: () => import('./features/customers/customer-list.component').then(m => m.CustomerListComponent)
  },
  { path: '', redirectTo: 'customers', pathMatch: 'full' },
];
