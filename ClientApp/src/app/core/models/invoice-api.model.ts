export interface InvoiceListItem {
  id: number;
  invoiceNumber: string;
  invoiceDate: string | null;
  customerName: string;
  uploadedFileName: string;
  createdDate: string;
  itemCount: number;
  totalQuantity: number;
  totalGstAmount: number;
  totalAmount: number;
}

export interface InvoiceListSummary {
  totalInvoices: number;
  totalItems: number;
  totalQuantity: number;
  totalGstAmount: number;
  totalInvoiceAmount: number;
  pendingItemCount: number;
}

export interface InvoiceListResponse {
  items: InvoiceListItem[];
  summary: InvoiceListSummary;
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface InvoiceListQuery {
  search?: string;
  dateFrom?: string | null;
  dateTo?: string | null;

  invoiceNumber?: string;
  columnInvoiceDateFrom?: string | null;
  columnInvoiceDateTo?: string | null;
  customerName?: string;

  minItemCount?: string;
  maxItemCount?: string;

  minTotalQuantity?: string;
  maxTotalQuantity?: string;

  minTotalGstAmount?: string;
  maxTotalGstAmount?: string;

  minTotalAmount?: string;
  maxTotalAmount?: string;

  uploadedFileName?: string;

  importedDateFrom?: string | null;
  importedDateTo?: string | null;

  page?: number;
  pageSize?: number;
}

export interface InvoiceItem {
  id: number;
  poNumber: string;
  lineItem: string;
  materialNo: string;
  qty: number;
  priceUnit: number;
  gstAmount: number;
  amount: number;
  poDate: string | null;
}

export interface InvoiceDetails {
  id: number;
  invoiceNumber: string;
  invoiceDate: string | null;
  customerName: string;
  uploadedFileName: string;
  uploadedFilePath: string;
  createdDate: string;
  totalQuantity: number;
  totalGstAmount: number;
  totalAmount: number;
  items: InvoiceItem[];
}

export interface InvoiceImportBatch {
  id: number;
  fileName: string;
  importDate: string;
  totalRows: number;
  successRows: number;
  failedRows: number;
  pendingRows: number;
  deletedRows: number;
}

export interface PendingInvoiceItem {
  id: number;
  invoiceNumber: string;
  invoiceDate: string | null;
  poNumber: string;
  lineItem: string;
  materialNo: string;
  customerName: string;
  description: string;
  hsnSacCode: string;
  qty: number;
  priceUnit: number;
  gstAmount: number;
  amount: number;
  errorMessage: string;
  createdDate: string;
}

export interface InvoiceImportResponse {
  success: boolean;
  message: string;
  batchId: number;
  totalRows: number;
  successRows: number;
  failedRows: number;
  pendingRows: number;
  }


export interface PendingInvoiceMatchCandidate {
  materialId: number;
  purchaseOrderId: number;
  poNumber: string;
  lineItem: string;
  materialNo: string;
  shortDescription: string;
  poQty: number;
  invoiceQty: number;
  balanceQty: number;
  dispatchQty: number;
  pendingQty: number;
  matchScore: number;
  isExactPO: boolean;
  isExactMaterial: boolean;
  isExactLineItem: boolean;
  hasEnoughBalance: boolean;
}

export interface PendingInvoiceMatchCandidatesResponse {
  pendingItem: PendingInvoiceItem;
  candidates: PendingInvoiceMatchCandidate[];
}

export interface PendingInvoiceManualMatchRequest {
  materialId: number;
  confirmMismatch: boolean;
}

export interface PendingInvoiceManualMatchResponse {
  success: boolean;
  message: string;
  pendingItemId: number;
  materialId: number;
  invoiceId: number;
}

export interface PendingInvoiceRetryResponse {
  success: boolean;
  message: string;
  pendingBefore: number;
  matchedRows: number;
  remainingRows: number;
}

export interface InvoiceDeleteResponse {
  success: boolean;
  message: string;
  invoiceId: number;
}

export interface InvoiceImportBatchDeleteResponse {
  success: boolean;
  message: string;
  batchId: number;
}
export interface InvoiceBulkDeleteRequest {
  invoiceIds: number[];
}

export interface InvoiceBulkDeleteResponse {
  success: boolean;
  message: string;
  requestedCount: number;
  deletedCount: number;
  missingCount: number;
  deletedInvoiceIds: number[];
}
