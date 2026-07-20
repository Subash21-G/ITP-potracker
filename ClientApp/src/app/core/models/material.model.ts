export interface MaterialListItem {
  readonly id: number;
  readonly purchaseOrderId: number;
  readonly poNumber: string;
  readonly handler: string;
  readonly poDate: string | null;
  readonly poReceivedDate: string | null;
  readonly lineItem: string;
  readonly materialNo: string;
  readonly hsnSacCode: string | null;
  readonly description: string;
  readonly quantity: number;
  readonly unit: string;
  readonly invoiceQuantity: number;
  readonly dispatchQuantity: number;
  readonly pendingQuantity: number;
  readonly recordedStockQuantity: number;
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
  readonly totalPages: number;
  readonly hasPreviousPage: boolean;
  readonly hasNextPage: boolean;
}

export interface MaterialQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly search?: string;
  readonly poNumber?: string;
  readonly handler?: string;
  readonly invoiceStatus?: string;
  readonly dispatchStatus?: string;
  readonly deliveryDateFrom?: string;
  readonly deliveryDateTo?: string;
}
