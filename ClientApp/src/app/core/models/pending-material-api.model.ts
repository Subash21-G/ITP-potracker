export interface PendingMaterialItem {
  materialId: number;
  purchaseOrderId: number;

  poNumber: string;
  handler: string;

  poDate: string | null;
  poReceivedDate: string | null;

  lineItem: string;
  materialNo: string;

  hsnSacCode: string | null;
  shortDescription: string | null;
  briefDescription: string | null;

  unit: string | null;
  department: string | null;

  drawingNo: string | null;
  drawingVersion: string | null;

  poQuantity: number;
  invoiceQuantity: number;
  invoiceBalanceQuantity: number;
  dispatchQuantity: number;
  pendingDispatchQuantity: number;
  stockQuantity: number;

  unitCost: number;
  totalValue: number;

  deliveryDate: string | null;
  processStartDate: string | null;
  alertDate: string | null;

  processStatus: string;
  invoiceStatus: string;
  dispatchStatus: string;

  assignedDashboard: string | null;
  remarks: string | null;

  pdfFilePath: string | null;
  drawingFilePath: string | null;

  completionPercentage: number;
  overdueDays: number;

  isOverdue: boolean;
  isDueToday: boolean;
  isDueThisWeek: boolean;

  dueStatus: string;
}

export interface PendingMaterialAssignment {
  assignedDashboard: string;
  materialCount: number;
  pendingQuantity: number;
  overdueCount: number;
}

export interface PendingMaterialSummary {
  totalPendingMaterials: number;

  overdueMaterials: number;
  dueTodayMaterials: number;
  dueThisWeekMaterials: number;
  withoutDeliveryDate: number;

  totalPOQuantity: number;
  totalInvoiceQuantity: number;
  totalInvoiceBalanceQuantity: number;
  totalDispatchQuantity: number;
  totalPendingDispatchQuantity: number;
  totalPendingValue: number;

  assignmentSummary: PendingMaterialAssignment[];
}

export interface PendingMaterialFilterOption {
  value: string;
  text: string;
  count: number;
}

export interface PendingMaterialFilterData {
  handlers: PendingMaterialFilterOption[];
  assignedDashboards: PendingMaterialFilterOption[];
  processStatuses: PendingMaterialFilterOption[];
  invoiceStatuses: PendingMaterialFilterOption[];
  dispatchStatuses: PendingMaterialFilterOption[];
}

export interface PendingMaterialsResponse {
  items: PendingMaterialItem[];
  summary: PendingMaterialSummary;
  filters: PendingMaterialFilterData;

  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
}

export type PendingMaterialSortColumn =
  | 'due'
  | 'po'
  | 'material'
  | 'description'
  | 'poQty'
  | 'invoice'
  | 'invoiceBalance'
  | 'dispatch'
  | 'pendingDispatch'
  | 'progress'
  | 'process'
  | 'invoiceStatus'
  | 'dispatchStatus'
  | 'delivery'
  | 'assigned';

export type PendingMaterialSortDirection =
  | 'asc'
  | 'desc';

export type PendingMaterialExcelFilterColumn =
  PendingMaterialSortColumn;

export interface PendingMaterialExcelFilterValue {
  readonly value: string;
  readonly label: string;
  readonly count: number;
  readonly selected?: boolean;
}

export type PendingMaterialExcelFilterSelections =
  Partial<
    Record<
      PendingMaterialExcelFilterColumn,
      readonly string[]
    >
  >;

export interface PendingMaterialQuery {
  search?: string;
  poNumber?: string;
  handler?: string;
  assignedDashboard?: string;
  processStatus?: string;
  invoiceStatus?: string;
  dispatchStatus?: string;

  poDateFrom?: string;
  poDateTo?: string;
  poReceivedDateFrom?: string;
  poReceivedDateTo?: string;
  deliveryDateFrom?: string;
  deliveryDateTo?: string;

  overdueOnly?: boolean;
  dueThisWeekOnly?: boolean;

  dueStatus?: string;

  columnPoNumber?: string;
  columnHandler?: string;

  columnMaterialNo?: string;
  columnLineItem?: string;

  columnDescription?: string;

  minPoQuantity?: string;
  maxPoQuantity?: string;

  minInvoiceQuantity?: string;
  maxInvoiceQuantity?: string;

  minInvoiceBalanceQuantity?: string;
  maxInvoiceBalanceQuantity?: string;

  minDispatchQuantity?: string;
  maxDispatchQuantity?: string;

  minPendingDispatchQuantity?: string;
  maxPendingDispatchQuantity?: string;

  minCompletionPercentage?: string;
  maxCompletionPercentage?: string;

  columnProcessStatus?: string;
  columnInvoiceStatus?: string;
  columnDispatchStatus?: string;

  columnDeliveryDateFrom?: string;
  columnDeliveryDateTo?: string;

  columnAssignedDashboard?: string;

  sortColumn?: PendingMaterialSortColumn;
  sortDirection?: PendingMaterialSortDirection;
  excelFilters?: string;

  page?: number;
  pageSize?: number;
}
