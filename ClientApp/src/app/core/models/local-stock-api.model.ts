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