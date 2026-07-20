import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'materials',
    loadComponent: () =>
      import('./features/materials/materials-page').then((component) => component.MaterialsPage),
  },
  {
    path: 'purchase-orders/:id',
    loadComponent: () =>
      import('./features/purchase-orders/purchase-order-details-page').then(
        (component) => component.PurchaseOrderDetailsPage,
      ),
  },
  {
    path: 'purchase-orders',
    loadComponent: () =>
      import('./features/purchase-orders/purchase-orders-page').then(
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
