export interface PurchaseOrderListItem {
  readonly purchaseOrderId: number;
  readonly poNumber: string;
  readonly handler: string;
  readonly purchaserName: string;
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
  readonly totalMaterialValue?: number;
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
  readonly totalMaterialValue?: number;
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

export type PurchaseOrderSortColumn =
  | 'po'
  | 'poDate'
  | 'received'
  | 'status'
  | 'materialProgress'
  | 'materials'
  | 'quantity'
  | 'value'
  | 'dispatchBalance'
  | 'delivery'
  | 'pdf';

export type PurchaseOrderSortDirection =
  | 'asc'
  | 'desc';


export type PurchaseOrderExcelFilterColumn =
  PurchaseOrderSortColumn;

export interface PurchaseOrderExcelFilterValue {
  readonly value: string;
  readonly label: string;
  readonly count: number;
  readonly selected?: boolean;
}

export type PurchaseOrderExcelFilterSelections =
  Partial<
    Record<
      PurchaseOrderExcelFilterColumn,
      readonly string[]
    >
  >;

export interface PurchaseOrderQuery {
  readonly page: number;
  readonly pageSize: number;

  readonly sortColumn?: PurchaseOrderSortColumn;
  readonly sortDirection?: PurchaseOrderSortDirection;
  readonly excelFilters?: string;
  readonly search?: string;
  readonly handler?: string;
  readonly poDateFrom?: string;
  readonly poDateTo?: string;
  readonly poReceivedDateFrom?: string;
  readonly poReceivedDateTo?: string;
  readonly status?: string;
  readonly materialStatus?: string;
  readonly minMaterials?: string;
  readonly maxMaterials?: string;
  readonly minPoQuantity?: string;
  readonly maxPoQuantity?: string;
  readonly minPoValue?: string;
  readonly maxPoValue?: string;
  readonly minDispatchBalance?: string;
  readonly maxDispatchBalance?: string;
  readonly deliveryDateFrom?: string;
  readonly deliveryDateTo?: string;
  readonly pdfAvailability?: string;
}

export interface PurchaseOrderBulkDeleteRequest {
  readonly purchaseOrderIds: readonly number[];
}

export interface PurchaseOrderBulkDeleteResponse {
  readonly success: boolean;
  readonly message: string;
  readonly requestedCount: number;
  readonly deletedCount: number;
  readonly skippedCount: number;
  readonly missingCount: number;
  readonly linkedMaterialCount: number;
  readonly deletedPurchaseOrderIds: readonly number[];
}
