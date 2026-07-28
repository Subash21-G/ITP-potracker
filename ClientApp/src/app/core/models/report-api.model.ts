export interface ReportPurchaserOption {
  id: number;
  purchaserName: string;
}

export interface ReportOptions {
  purchasers: ReportPurchaserOption[];
  processStatuses: string[];
  dispatchStatuses: string[];
}

export interface MaterialReportFilter {
  fromDate: string;
  toDate: string;
  processStatus: string;
  dispatchStatus: string;
  purchaserId: number | null;
}

export interface MaterialReportItem {
  materialId: number;
  purchaseOrderId: number | null;

  poNumber: string;
  purchaserName: string;

  materialNo: string;
  description: string;

  qty: number;
  dispatchQty: number;
  pendingQty: number;
  liveStockQuantity: number;
  allocatedStockQuantity: number;
  stockMinimumQuantity: number;
  stockShortageQuantity: number;
  stockStatus: string;
  stockRecordCount: number;
  stockDataLive: boolean;

  deliveryDate: string | null;

  processStatus: string;
  dispatchStatus: string;

  rmGrade: string;
  rmSource: string;
  surfaceFinish: string;

  cost: number;
  remark: string;
}

export interface MaterialReportSummary {
  totalRecords: number;
  totalQty: number;
  totalDispatchQty: number;
  totalPendingQty: number;
  totalCost: number;
}

export interface MaterialReportResponse {
  summary: MaterialReportSummary;
  items: MaterialReportItem[];
}

