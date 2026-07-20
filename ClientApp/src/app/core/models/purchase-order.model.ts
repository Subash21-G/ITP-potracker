export interface PurchaseOrderListItem {
  readonly purchaseOrderId: number;
  readonly poNumber: string;
  readonly handler: string;
  readonly poDate: string | null;
  readonly poReceivedDate: string | null;
  readonly status: string;
  readonly materialStatus: string;
  readonly pdfFilePath: string | null;
  readonly createdDate: string;
  readonly totalMaterials: number;
  readonly pendingMaterials: number;
  readonly completedMaterials: number;
  readonly notInvoicedMaterials: number;
  readonly pendingDispatchMaterials: number;
  readonly overdueMaterials: number;
  readonly totalPOQuantity: number;
  readonly totalInvoiceQuantity: number;
  readonly totalInvoiceBalanceQuantity: number;
  readonly totalDispatchQuantity: number;
  readonly totalPendingDispatchQuantity: number;
  readonly earliestDeliveryDate: string | null;
  readonly latestDeliveryDate: string | null;
  readonly completionPercentage: number;
}

export interface PurchaseOrderListSummary {
  readonly totalPurchaseOrders: number;
  readonly openPurchaseOrders: number;
  readonly completedPurchaseOrders: number;
  readonly purchaseOrdersWithPendingMaterials: number;
  readonly purchaseOrdersWithOverdueMaterials: number;
  readonly purchaseOrdersWithoutHandler: number;
  readonly totalPOQuantity: number;
  readonly totalInvoiceBalanceQuantity: number;
  readonly totalPendingDispatchQuantity: number;
}

export interface PurchaseOrderListResponse {
  readonly items: readonly PurchaseOrderListItem[];
  readonly summary: PurchaseOrderListSummary;
  readonly handlers: readonly string[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalRecords: number;
  readonly totalPages: number;
  readonly hasPreviousPage: boolean;
  readonly hasNextPage: boolean;
}

export interface PurchaseOrderQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly search?: string;
  readonly handler?: string;
  readonly poDateFrom?: string;
  readonly poDateTo?: string;
  readonly poReceivedDateFrom?: string;
  readonly poReceivedDateTo?: string;
  readonly status?: string;
  readonly materialStatus?: string;
}
