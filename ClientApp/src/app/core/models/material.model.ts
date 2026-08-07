export interface MaterialListItem {
  readonly id: number;
  readonly purchaseOrderId: number;
  readonly poNumber: string;
  readonly handler: string;
  readonly purchaserName: string;
  readonly poDate: string | null;
  readonly poReceivedDate: string | null;
  readonly lineItem: string;
  readonly materialNo: string;
  readonly hsnSacCode: string | null;
  readonly description: string;
  readonly quantity: number;
  readonly unit: string;
  readonly unitPrice: number;
  readonly priceUnit: string;
  readonly totalValue?: number;
  readonly invoiceQuantity: number;
  readonly dispatchQuantity: number;
  readonly pendingQuantity: number;

  readonly liveStockQuantity: number;
  readonly allocatedStockQuantity: number;
  readonly stockMinimumQuantity: number;
  readonly stockShortageQuantity: number;
  readonly stockStatus: string;
  readonly stockRecordCount: number;
  readonly stockDataLive: boolean;
  readonly invoiceStatus: string;
  readonly processStatus: string;
  readonly dispatchStatus: string;
  readonly deliveryDate: string | null;
  readonly drawingNo: string;
  readonly assignedDashboard: string;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalRecords: number;
  readonly totalValue?: number;
  readonly totalPages: number;
  readonly hasPreviousPage: boolean;
  readonly hasNextPage: boolean;
}

export type MaterialSortColumn =
  | 'po'
  | 'line'
  | 'material'
  | 'description'
  | 'ordered'
  | 'unitPrice'
  | 'materialValue'
  | 'dispatched'
  | 'pending'
  | 'process'
  | 'dispatch'
  | 'delivery';

export type MaterialSortDirection =
  | 'asc'
  | 'desc';

export interface MaterialQuery {
  readonly page: number;
  readonly pageSize: number;

  readonly sortColumn?: MaterialSortColumn;
  readonly sortDirection?: MaterialSortDirection;

  readonly search?: string;
  readonly poNumber?: string;
  readonly handler?: string;
  readonly poDateFrom?: string;
  readonly poDateTo?: string;
  readonly poReceivedDateFrom?: string;
  readonly poReceivedDateTo?: string;
  readonly invoiceStatus?: string;
  readonly dispatchStatus?: string;
  readonly stockStatus?: string;
  readonly deliveryDateFrom?: string;
  readonly deliveryDateTo?: string;

  readonly columnPoNumber?: string;
  readonly columnPurchaser?: string;
  readonly columnHandler?: string;
  readonly lineItem?: string;
  readonly materialNo?: string;
  readonly description?: string;

  readonly minOrderedQty?: string;
  readonly maxOrderedQty?: string;

  readonly minUnitPrice?: string;
  readonly maxUnitPrice?: string;

  readonly minMaterialValue?: string;
  readonly maxMaterialValue?: string;

  readonly minDispatchQty?: string;
  readonly maxDispatchQty?: string;

  readonly minPendingQty?: string;
  readonly maxPendingQty?: string;

  readonly minAllocatedQty?: string;
  readonly maxAllocatedQty?: string;
  readonly minLiveStockQty?: string;
  readonly maxLiveStockQty?: string;

  readonly minShortageQty?: string;
  readonly maxShortageQty?: string;

  readonly columnStockStatus?: string;
  readonly processStatus?: string;
  readonly columnDispatchStatus?: string;

  readonly columnDeliveryDateFrom?: string;
  readonly columnDeliveryDateTo?: string;
}





export interface MaterialBulkDeleteRequest {
  readonly materialIds: readonly number[];
}

export interface MaterialBulkDeleteResponse {
  readonly success: boolean;
  readonly message: string;
  readonly requestedCount: number;
  readonly deletedCount: number;
  readonly skippedCount: number;
  readonly missingCount: number;
  readonly invoiceLinkedCount: number;
  readonly dispatchLinkedCount: number;
  readonly deletedMaterialIds: readonly number[];
}
