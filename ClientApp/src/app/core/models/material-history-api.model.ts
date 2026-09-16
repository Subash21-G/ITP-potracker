export type MaterialHistoryOutcome = 'PerfectDelivery' | 'ReworkAndDelivery' | 'Rejected';

export interface MaterialHistoryItem {
  readonly id: number;
  readonly materialId: number;
  readonly materialNo: string;
  readonly lineItem: string | null;
  readonly poNumber: string;
  readonly description: string | null;
  readonly outcome: MaterialHistoryOutcome;
  readonly remark: string | null;
  readonly occurredDate: string;
  readonly createdDate: string;
  readonly updatedDate: string | null;
}

export interface MaterialHistorySummary {
  readonly totalEntries: number;
  readonly perfectDeliveries: number;
  readonly reworkDeliveries: number;
  readonly rejected: number;
}

export interface MaterialHistoryListResponse {
  readonly items: readonly MaterialHistoryItem[];
  readonly summary: MaterialHistorySummary;
  readonly page: number;
  readonly pageSize: number;
  readonly totalRecords: number;
  readonly totalPages: number;
}

export interface MaterialHistoryMaterialOption {
  readonly id: number;
  readonly materialNo: string;
  readonly lineItem: string | null;
  readonly poNumber: string;
  readonly description: string | null;
  readonly displayLabel: string;
}

export type MaterialHistoryExcelFilterColumn = 'date' | 'material' | 'poLine' | 'result' | 'remark';

export type MaterialHistorySortDirection = 'asc' | 'desc';

export interface MaterialHistoryExcelFilterValue {
  readonly value: string;
  readonly label: string;
  readonly count: number;
}

export type MaterialHistoryExcelFilterSelections = Partial<
  Record<MaterialHistoryExcelFilterColumn, readonly string[]>
>;

export interface MaterialHistoryListQuery {
  readonly search?: string;
  readonly outcome?: MaterialHistoryOutcome;
  readonly materialId?: number;
  readonly sortColumn?: MaterialHistoryExcelFilterColumn;
  readonly sortDirection?: MaterialHistorySortDirection;
  readonly excelFilters?: string;
  readonly page: number;
  readonly pageSize: number;
}

export interface MaterialHistoryUpsertRequest {
  readonly materialId: number;
  readonly outcome: MaterialHistoryOutcome;
  readonly remark: string | null;
  readonly occurredDate: string;
}

export interface MaterialHistorySaveResponse {
  readonly success: boolean;
  readonly message: string;
  readonly history: MaterialHistoryItem;
}
