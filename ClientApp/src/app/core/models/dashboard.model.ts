export interface DashboardResponse {
  generatedAt: string;
  overview: DashboardOverview;
  quantities: DashboardQuantitySummary;
  process: DashboardProcessSummary;
  dispatch: DashboardDispatchSummary;
  invoice: DashboardInvoiceSummary;
  recentMaterials: DashboardRecentMaterial[];
}

export interface DashboardOverview {
  totalPurchasers: number;
  totalPurchaseOrders: number;
  totalMaterials: number;
  pendingMaterials: number;
  lowStock: number;
  todayAlerts: number;
  overdueMaterials: number;
  deliveryWithin7Days: number;
  processStartAlerts: number;
  dispatchPending: number;
}

export interface DashboardQuantitySummary {
  totalPoQty: number;
  totalInvoiceQty: number;
  totalDispatchQty: number;
  totalPendingQty: number;
}

export interface DashboardProcessSummary {
  pending: number;
  inProgress: number;
  completed: number;
}

export interface DashboardDispatchSummary {
  completed: number;
  partial: number;
  pending: number;
}

export interface DashboardInvoiceSummary {
  fullyInvoiced: number;
  partiallyInvoiced: number;
  notInvoiced: number;
}

export interface DashboardRecentMaterial {
  id: number;
  purchaseOrderId: number;
  poNumber: string;
  purchaserName: string;
  lineItem: string;
  materialNo: string;
  description: string;
  poQty: number;
  invoiceQty: number;
  dispatchQty: number;
  pendingQty: number;
  deliveryDate: string | null;
  invoiceStatus: string;
  dispatchStatus: string;
  processStatus: string;
}
