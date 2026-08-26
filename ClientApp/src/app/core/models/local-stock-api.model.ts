export interface LocalStockListItem {
  readonly id: number;
  readonly materialNo: string;
  readonly materialName: string;
  readonly rmGrade: string;
  readonly location: string;
  readonly availableQty: number;
  readonly minimumQty: number;
  readonly isLowStock: boolean;
  readonly status: string;
  readonly updatedDate: string;
}

export type LocalStockSortColumn =
  | 'material'
  | 'grade'
  | 'location'
  | 'available'
  | 'minimum'
  | 'status'
  | 'updated';

export type LocalStockSortDirection =
  | 'asc'
  | 'desc';

export type LocalStockExcelFilterColumn =
  LocalStockSortColumn;

export interface LocalStockExcelFilterValue {
  readonly value: string;
  readonly label: string;
  readonly count: number;
  readonly selected?: boolean;
}

export type LocalStockExcelFilterSelections =
  Partial<
    Record<
      LocalStockExcelFilterColumn,
      readonly string[]
    >
  >;

export interface LocalStockSummary {
  readonly totalItems: number;
  readonly lowStockItems: number;
  readonly healthyItems: number;
  readonly updatedToday: number;
}

export interface LocalStockListResponse {
  readonly items: readonly LocalStockListItem[];
  readonly summary: LocalStockSummary;
  readonly page: number;
  readonly pageSize: number;
  readonly totalRecords: number;
  readonly totalPages: number;
}

export interface LocalStockListQuery {
  readonly search: string;

  readonly material?: string;
  readonly rmGrade?: string;
  readonly location?: string;

  readonly minAvailableQty?: string;
  readonly maxAvailableQty?: string;

  readonly minMinimumQty?: string;
  readonly maxMinimumQty?: string;

  readonly status?: string;

  readonly updatedFrom?: string;
  readonly updatedTo?: string;

  readonly sortColumn?: LocalStockSortColumn;
  readonly sortDirection?: LocalStockSortDirection;
  readonly excelFilters?: string;

  readonly page: number;
  readonly pageSize: number;
}

export interface LocalStockUpsertRequest {
  readonly materialNo: string;
  readonly materialName: string | null;
  readonly rmGrade: string | null;
  readonly location: string | null;
  readonly availableQty: number;
  readonly minimumQty: number;
}

export interface LocalStockSaveResponse {
  readonly success: boolean;
  readonly message: string;
  readonly stock: LocalStockListItem | null;
}

export interface LocalStockDeleteResponse {
  readonly success: boolean;
  readonly message: string;
  readonly stockId: number;
}