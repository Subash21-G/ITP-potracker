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
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/dashboard/dashboard-page').then(
        (component) => component.DashboardPage,
      ),
  },

  // ============================================================
  // MATERIALS
  // ============================================================

  {
    path: 'materials/new',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/materials/form/material-form-page').then(
        (component) => component.MaterialFormPage,
      ),
  },
  {
    path: 'materials/:id/edit',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/materials/form/material-form-page').then(
        (component) => component.MaterialFormPage,
      ),
  },
  {
    path: 'materials/:id/files',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/materials/files/material-files-page').then(
        (component) => component.MaterialFilesPage,
      ),
  },
  {
    path: 'materials/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/materials/details/material-details-page').then(
        (component) => component.MaterialDetailsPage,
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

  // ============================================================
  // PURCHASE ORDERS
  // Keep specific routes before purchase-orders/:id.
  // ============================================================

  {
    path: 'purchase-orders/new',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/purchase-orders/form/purchase-order-form-page').then(
        (component) => component.PurchaseOrderFormPage,
      ),
  },
  {
    path: 'purchase-orders/:id/edit',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/purchase-orders/form/purchase-order-form-page').then(
        (component) => component.PurchaseOrderFormPage,
      ),
  },
  {
    path: 'purchase-orders/:id/manage',
    canActivate: [authGuard],
    loadComponent: () =>
      import(
        './features/purchase-orders/manage/purchase-order-manage-page'
      ).then(
        (component) => component.PurchaseOrderManagePage,
      ),
  },
  {
    path: 'purchase-orders/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import(
        './features/purchase-orders/purchase-order-details-page'
      ).then(
        (component) => component.PurchaseOrderDetailsPage,
      ),
  },
  {
    path: 'purchase-orders',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/purchase-orders/purchase-orders-page').then(
        (component) => component.PurchaseOrdersPage,
      ),
  },

  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'dashboard',
  },
  {
    path: '**',
    redirectTo: 'dashboard',
  },
];
