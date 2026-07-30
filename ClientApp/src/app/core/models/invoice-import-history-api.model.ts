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