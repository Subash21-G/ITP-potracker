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

export interface AlertsResponse {
  generatedAt: string;

  summary: AlertsSummary;

  processStartAlerts: AlertMaterial[];
  overdueMaterials: AlertMaterial[];
  deliveryWithinSevenDays: AlertMaterial[];
  pendingProcess: AlertMaterial[];
  dispatchPending: AlertMaterial[];
  dispatchDateAlerts: AlertMaterial[];
  lowStock: AlertMaterial[];
}
