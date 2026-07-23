export interface InvoiceImportItem {
  poDate: string | null;
  poNumber: string;
  lineItem: string;
  materialNo: string;
  description: string;
  hsnSacCode: string;
  invoiceQty: number;
  priceUnit: number;
  gstAmount: number;
  amount: number;
}

export interface InvoiceImportPreview {
  invoiceNumber: string;
  invoiceDate: string | null;
  customerName: string;
  uploadedFileName: string;
  uploadedFilePath: string;
  items: InvoiceImportItem[];
}

export interface InvoiceImportFilePreview {
  originalFileName: string;
  fileSize: number;
  isSuccessful: boolean;
  existsInDatabase: boolean;
  errorMessage: string | null;
  invoice: InvoiceImportPreview;
}

export interface InvoiceBatchImportFile {
  batchItemId: string;
  originalFileName: string;
  fileSize: number;
  isSuccessful: boolean;
  isSelected: boolean;
  isDuplicateInBatch: boolean;
  existsInDatabase: boolean;
  errorMessage: string | null;
  invoice: InvoiceImportPreview | null;
}

export interface InvoiceBatchImportPreview {
  batchId: string;
  files: InvoiceBatchImportFile[];
  totalFiles: number;
  successfulFiles: number;
  failedFiles: number;
  duplicateFiles: number;
  existingInvoiceFiles: number;
  totalInvoiceRows: number;
}

export interface InvoiceImportSaveResponse {
  message: string;
  invoiceId: number;
  invoiceNumber: string;
  totalRows: number;
  successRows: number;
  pendingRows: number;
}

export interface InvoiceBatchImportResultItem {
  originalFileName: string;
  invoiceNumber: string | null;
  isImported: boolean;
  isUpdated: boolean;
  rowsSaved: number;
  message: string;
}

export interface InvoiceBatchImportResult {
  batchId: string;
  results: InvoiceBatchImportResultItem[];
  totalProcessed: number;
  importedCount: number;
  updatedCount: number;
  failedCount: number;
  totalRowsSaved: number;
}
