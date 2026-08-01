export type AlertCategory =
  | 'process-start'
  | 'overdue'
  | 'delivery-seven-days'
  | 'pending-process'
  | 'dispatch-pending'
  | 'dispatch-date'
  | 'low-stock';

export interface AlertMaterial {
  materialId: number;
  purchaseOrderId: number;

  lineItem: string;
  materialNo: string;
  description: string;

  deliveryDate: string | null;
  alertDate: string | null;
  dispatchDate: string | null;

  processStatus: string;
  dispatchStatus: string;

  pendingQuantity: number;
  liveStockQuantity: number;
  allocatedStockQuantity: number;
  stockMinimumQuantity: number;
  stockShortageQuantity: number;
  stockStatus: string;
  stockRecordCount: number;
  stockDataLive: boolean;
}

export interface AlertsSummary {
  processStartAlerts: number;
  overdueMaterials: number;
  deliveryWithinSevenDays: number;
  pendingProcess: number;
  dispatchPending: number;
  dispatchDateAlerts: number;
  lowStock: number;
  totalAlerts: number;
}

export interface AlertsQuery {
  readonly category:
    AlertCategory;

  readonly search?: string;
  readonly page: number;
  readonly pageSize: number;
}

export interface AlertsPagedResponse {
  readonly generatedAt: string;
  readonly summary: AlertsSummary;
  readonly category: AlertCategory;
  readonly items:
    readonly AlertMaterial[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasPreviousPage: boolean;
  readonly hasNextPage: boolean;
}
