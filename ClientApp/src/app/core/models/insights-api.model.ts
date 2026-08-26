export interface DrawingSummary {
  readonly totalMaterials: number;
  readonly missingDrawings: number;
  readonly updatedThisWeek: number;
  readonly withVersion: number;
}

export interface DrawingListItem {
  readonly materialId: number;
  readonly materialNo: string;
  readonly description: string;
  readonly poNumber: string;
  readonly drawingVersion: string;
  readonly drawingUpdatedDate: string | null;
  readonly drawingFilePath: string | null;
  readonly pdfFilePath: string | null;
  readonly hasDrawing: boolean;
}

export interface DrawingListResponse {
  readonly summary: DrawingSummary;
  readonly items: readonly DrawingListItem[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalRecords: number;
  readonly totalPages: number;
}

export type DrawingSortColumn =
  | 'material'
  | 'po'
  | 'version'
  | 'updated'
  | 'drawing'
  | 'pdf'
  | 'status';

export type DrawingSortDirection =
  | 'asc'
  | 'desc';

export type DrawingExcelFilterColumn =
  DrawingSortColumn;

export interface DrawingExcelFilterValue {
  readonly value: string;
  readonly label: string;
  readonly count: number;
  readonly selected?: boolean;
}

export type DrawingExcelFilterSelections =
  Partial<
    Record<
      DrawingExcelFilterColumn,
      readonly string[]
    >
  >;

export interface DrawingListQuery {
  readonly search: string;
  readonly status: string;

  readonly material?: string;
  readonly poNumber?: string;
  readonly drawingVersion?: string;

  readonly updatedDateFrom?: string;
  readonly updatedDateTo?: string;

  readonly drawingAvailability?: string;
  readonly pdfAvailability?: string;
  readonly columnStatus?: string;

  readonly sortColumn?: DrawingSortColumn;
  readonly sortDirection?: DrawingSortDirection;
  readonly excelFilters?: string;

  readonly page: number;
  readonly pageSize: number;
}

export interface AuditItem {
  readonly materialId: number;
  readonly createdDate: string;
  readonly materialNo: string;
  readonly poNumber: string;
  readonly action: string;
  readonly processStatus: string;
  readonly dispatchStatus: string;
  readonly owner: string;
}

export interface AuditResponse {
  readonly items: readonly AuditItem[];
  readonly recordCount: number;
  readonly generatedAt: string;
}

export interface AnalyticsSummary {
  readonly purchaserCount: number;
  readonly totalMaterials: number;
  readonly overdueMaterials: number;
  readonly purchaseOrderCount: number;
}

export interface PurchaserAnalyticsRow {
  readonly purchaserId: number;
  readonly purchaserName: string;
  readonly purchaseOrderCount: number;
  readonly materialCount: number;
  readonly pendingMaterials: number;
  readonly completedMaterials: number;
}

export interface AnalyticsResponse {
  readonly summary: AnalyticsSummary;
  readonly purchasers: readonly PurchaserAnalyticsRow[];
  readonly generatedAt: string;
}