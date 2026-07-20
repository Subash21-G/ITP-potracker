import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login-page').then(
        (component) => component.LoginPage,
      ),
  },
  {
    path: 'materials',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/materials/materials-page').then(
        (component) => component.MaterialsPage,
      ),
  },
  {
    path: 'purchase-orders/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import(
        './features/purchase-orders/purchase-order-details-page'
      ).then(
        (component) =>
          component.PurchaseOrderDetailsPage,
      ),
  },
  {
    path: 'purchase-orders',
    canActivate: [authGuard],
    loadComponent: () =>
      import(
        './features/purchase-orders/purchase-orders-page'
      ).then(
        (component) => component.PurchaseOrdersPage,
      ),
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'materials',
  },
  {
    path: '**',
    redirectTo: 'materials',
  },
];
