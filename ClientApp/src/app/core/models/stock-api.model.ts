export interface StockListItem {
  readonly id: number;
  readonly materialCode: string;
  readonly materialNo: string;
  readonly description: string;
  readonly supplierName: string;
  readonly location: string;
  readonly currentStock: number;
  readonly minimumStock: number;
  readonly unitPrice: number;
  readonly totalValue: number;
  readonly isLowStock: boolean;
  readonly status: string;
  readonly lastUpdated: string | null;
}

export interface StockSummary {
  readonly totalRecords: number;
  readonly uniqueMaterials: number;
  readonly lowStockRecords: number;
  readonly availableRecords: number;
  readonly updatedToday: number;
  readonly totalCurrentStock: number;
  readonly totalInventoryValue: number;
}

export interface StockListResponse {
  readonly page: number;
  readonly pageSize: number;
  readonly totalRecords: number;
  readonly totalPages: number;
  readonly sourceOnline: boolean;
  readonly message: string;
  readonly refreshedAt: string;
  readonly summary: StockSummary;
  readonly items: readonly StockListItem[];
}

export interface StockListQuery {
  readonly search: string;
  readonly page: number;
  readonly pageSize: number;
  readonly refresh?: boolean;
}
