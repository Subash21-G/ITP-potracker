import { Routes } from '@angular/router';

import { adminGuard, authGuard } from './core/guards/auth.guard';
import { manageAccessGuard } from './core/guards/manage-access.guard';

export const routes: Routes = [
  // ============================================================
  // AUTHENTICATION
  // ============================================================

  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login-page').then(
        (component) => component.LoginPage,
      ),
  },

  // ============================================================
  // DASHBOARD
  // ============================================================

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
  // Keep specific routes before materials/:id.
  // ============================================================

  {
    path: 'materials/new',
    canActivate: [authGuard, manageAccessGuard],
    loadComponent: () =>
      import('./features/materials/form/material-form-page').then(
        (component) => component.MaterialFormPage,
      ),
  },
  {
    path: 'materials/:id/edit',
    canActivate: [authGuard, manageAccessGuard],
    loadComponent: () =>
      import('./features/materials/form/material-form-page').then(
        (component) => component.MaterialFormPage,
      ),
  },
  {
    path: 'materials/:id/files',
    canActivate: [authGuard, manageAccessGuard],
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
    path: 'material-import',
    canActivate: [
      authGuard,
      manageAccessGuard,
    ],
    loadComponent: () =>
      import(
        './features/material-import/material-import-page'
      ).then(
        (component) =>
          component.MaterialImportPage,
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
    canActivate: [authGuard, manageAccessGuard],
    loadComponent: () =>
      import(
        './features/purchase-orders/form/purchase-order-form-page'
      ).then(
        (component) => component.PurchaseOrderFormPage,
      ),
  },
  {
    path: 'purchase-orders/:id/edit',
    canActivate: [authGuard, manageAccessGuard],
    loadComponent: () =>
      import(
        './features/purchase-orders/form/purchase-order-form-page'
      ).then(
        (component) => component.PurchaseOrderFormPage,
      ),
  },
  {
    path: 'purchase-orders/:id/manage',
    canActivate: [authGuard, manageAccessGuard],
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
    path: 'po-import',
    canActivate: [
      authGuard,
      manageAccessGuard,
    ],
    loadComponent: () =>
      import(
        './features/po-import/po-import-page'
      ).then(
        (component) =>
          component.PoImportPage,
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

  // ============================================================
  // PURCHASERS
  // ============================================================

  {
    path: 'purchasers/new',
    canActivate: [
      authGuard,
      manageAccessGuard,
    ],
    loadComponent: () =>
      import(
        './features/purchasers/form/purchaser-form-page'
      ).then(
        (component) =>
          component.PurchaserFormPage,
      ),
  },
  {
    path: 'purchasers/:id/edit',
    canActivate: [
      authGuard,
      manageAccessGuard,
    ],
    loadComponent: () =>
      import(
        './features/purchasers/form/purchaser-form-page'
      ).then(
        (component) =>
          component.PurchaserFormPage,
      ),
  },
  {
    path: 'purchasers',
    canActivate: [
      authGuard,
    ],
    loadComponent: () =>
      import(
        './features/purchasers/purchasers-page'
      ).then(
        (component) =>
          component.PurchasersPage,
      ),
  },
  // ============================================================
  // STOCK
  // ============================================================
  {
    path: 'stocks',
    canActivate: [
      authGuard,
    ],
    loadComponent: () =>
      import(
        './features/stocks/stocks-page'
      ).then(
        (component) =>
          component.StocksPage,
      ),
  },
  // ============================================================
  // INVOICES
  // Keep named routes before invoices/:id.
  // ============================================================

  {
    path: 'invoices/pending',
    canActivate: [
      authGuard,
      manageAccessGuard,
    ],
    loadComponent: () =>
      import(
        './features/invoices/pending/pending-invoices-page'
      ).then(
        (component) => component.PendingInvoicesPage,
      ),
  },
  {
    path: 'invoices/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import(
        './features/invoices/details/invoice-details-page'
      ).then(
        (component) => component.InvoiceDetailsPage,
      ),
  },
  {
    path: 'invoice-import',
    canActivate: [
      authGuard,
      manageAccessGuard,
    ],
    loadComponent: () =>
      import(
        './features/invoice-import/invoice-import-page'
      ).then(
        (component) =>
          component.InvoiceImportPage,
      ),
  },
  {
    path: 'invoices',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/invoices/invoices-page').then(
        (component) => component.InvoicesPage,
      ),
  },

  // ============================================================
  // ACCESS CONTROL
  // ============================================================

  // ============================================================
  // DISPATCH
  // ============================================================

  {
    path: 'dispatches/:materialId',
    canActivate: [authGuard],
    loadComponent: () =>
      import(
        './features/dispatches/dispatch-details-page'
      ).then(
        (component) =>
          component.DispatchDetailsPage,
      ),
  },
  {
    path: 'dispatches',
    canActivate: [authGuard],
    loadComponent: () =>
      import(
        './features/dispatches/dispatches-page'
      ).then(
        (component) =>
          component.DispatchesPage,
      ),
  },
  // ============================================================
  // PRODUCTION
  // ============================================================

  {
    path: 'pending-materials',
    canActivate: [
      authGuard,
    ],
    loadComponent: () =>
      import(
        './features/pending-materials/pending-materials-page'
      ).then(
        (component) =>
          component.PendingMaterialsPage,
      ),
  },
  {
    path: 'reports',
    canActivate: [
      authGuard,
      adminGuard,
    ],
    loadComponent: () =>
      import(
        './features/reports/reports-page'
      ).then(
        (component) =>
          component.ReportsPage,
      ),
  },
  {
    path: 'notifications',
    canActivate: [
      authGuard,
    ],
    loadComponent: () =>
      import(
        './features/notifications/notifications-page'
      ).then(
        (component) =>
          component.NotificationsPage,
      ),
  },
  {
    path: 'alerts',
    canActivate: [
      authGuard,
    ],
    loadComponent: () =>
      import(
        './features/alerts/alerts-page'
      ).then(
        (component) =>
          component.AlertsPage,
      ),
  },
  {
    path: 'process-queue',
    canActivate: [authGuard],
    loadComponent: () =>
      import(
        './features/production/process-queue-page'
      ).then(
        (component) =>
          component.ProcessQueuePage,
      ),
  },
  {
    path: 'production-calendar',
    canActivate: [authGuard],
    loadComponent: () =>
      import(
        './features/production/production-calendar-page'
      ).then(
        (component) =>
          component.ProductionCalendarPage,
      ),
  },
  {
    path: 'access-denied',
    canActivate: [authGuard],
    loadComponent: () =>
      import(
        './features/shared/access-denied/access-denied-page'
      ).then(
        (component) => component.AccessDeniedPage,
      ),
  },

  // ============================================================
  // DEFAULT ROUTES
  // ============================================================

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






