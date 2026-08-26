import {
  AuditItem,
} from './insights-api.model';

export type AuditSortColumn =
  | 'date'
  | 'material'
  | 'po'
  | 'action'
  | 'process'
  | 'dispatch'
  | 'owner';

export type AuditSortDirection =
  | 'asc'
  | 'desc';

export type AuditExcelFilterColumn =
  AuditSortColumn;

export interface AuditExcelFilterValue {
  readonly value: string;
  readonly label: string;
  readonly count: number;
  readonly selected?: boolean;
}

export type AuditExcelFilterSelections =
  Partial<
    Record<
      AuditExcelFilterColumn,
      readonly string[]
    >
  >;

export interface AuditQuery {
  readonly sortColumn?: AuditSortColumn;
  readonly sortDirection?: AuditSortDirection;
  readonly excelFilters?: string;

  readonly search?: string;
  readonly owner?: string;
  readonly processStatus?: string;
  readonly dispatchStatus?: string;
  readonly dateFrom?: string;
  readonly dateTo?: string;

  readonly materialNo?: string;
  readonly poNumber?: string;
  readonly action?: string;

  readonly columnProcessStatus?: string;
  readonly columnDispatchStatus?: string;
  readonly columnOwner?: string;

  readonly createdDateFrom?: string;
  readonly createdDateTo?: string;

  readonly page: number;
  readonly pageSize: number;
}

export interface AuditPagedResponse {
  readonly items:
    readonly AuditItem[];

  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasPreviousPage: boolean;
  readonly hasNextPage: boolean;
  readonly generatedAt: string;
}