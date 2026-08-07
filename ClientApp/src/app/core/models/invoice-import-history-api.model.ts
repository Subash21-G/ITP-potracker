export interface InvoiceImportHistoryBulkDeleteRequest {
  batchIds: number[];
}

export interface InvoiceImportHistoryBulkDeleteResponse {
  success: boolean;
  message: string;
  deletedBatchCount: number;
  deletedRowCount: number;
  deletedBatchIds: number[];
}

export interface InvoiceImportReportSummary {
  totalRows: number;
  successRows: number;
  pendingRows: number;
  deletedRows: number;
  totalInvoiceQuantity: number;
  successPercent: number;
  pendingPercent: number;
}

export interface InvoiceImportReportRow {
  id: number;
  poNumber: string;
  lineItem: string;
  materialNumber: string;
  invoiceNumber: string;
  invoiceQuantity: number;
  status: string;
  message: string;
}

export interface InvoiceImportReportQuery {
  readonly search?: string;
  readonly status?: string;

  readonly poNumber?: string;
  readonly lineItem?: string;
  readonly materialNumber?: string;
  readonly invoiceNumber?: string;

  readonly minInvoiceQuantity?: string;
  readonly maxInvoiceQuantity?: string;

  readonly columnStatus?: string;
  readonly message?: string;

  readonly page: number;
  readonly pageSize: number;
}

export interface InvoiceImportReportResponse {
  batchId: number;
  fileName: string;
  importDate: string;
  summary: InvoiceImportReportSummary;
  items: InvoiceImportReportRow[];
  currentPage: number;
  pageSize: number;
  totalPages: number;
}