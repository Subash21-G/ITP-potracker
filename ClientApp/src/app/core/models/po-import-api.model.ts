export interface PoImportItem {
  lineItem: string | null;
  itemType: string | null;
  materialNo: string | null;
  hsnSacCode: string | null;
  shortDescription: string | null;
  fullDescription: string | null;
  drawingNo: string | null;
  drawingVersion: string | null;
  grade: string | null;
  size: string | null;
  process: string | null;
  source: string | null;
  receiver: string | null;
  unloadingPoint: string | null;
  quantity: number;
  unit: string | null;
  dep: string | null;
  valueOfGoods: number;
  priceUnit: string | null;
  totalValue: number;
  cgstRate: number;
  cgstAmount: number;
  sgstRate: number;
  sgstAmount: number;
  igstRate: number;
  igstAmount: number;
  totalValueWithTax: number;
  deliveryDate: string | null;
}

export interface PoImportPreview {
  purchaseOrderNo: string | null;
  poDate: string | null;
  receivedDate: string | null;
  purchaserName: string | null;
  buyerContactPerson: string | null;
  buyerEmail: string | null;
  vendorCode: string | null;
  vendorName: string | null;
  paymentTerms: string | null;
  deliveryCondition: string | null;
  currency: string | null;
  basicValue: number;
  taxValue: number;
  grandTotal: number;
  importFormat: string | null;
  uploadedPdfPath: string | null;
  warnings: string[];
  items: PoImportItem[];
}

export interface PoImportFilePreview {
  batchItemId: string;
  originalFileName: string;
  fileSize: number;
  isSuccessful: boolean;
  isSelected: boolean;
  isDuplicateInBatch: boolean;
  existsInDatabase: boolean;
  errorMessage: string | null;
  purchaseOrder: PoImportPreview | null;
}

export interface PoBatchImportPreview {
  batchId: string;
  files: PoImportFilePreview[];
  totalFiles: number;
  successfulFiles: number;
  failedFiles: number;
  duplicateFiles: number;
  existingPOFiles: number;
  totalMaterialRows: number;
}

export interface PoImportSaveResponse {
  message: string;
  wasCreated: boolean;
  purchaseOrderId: number;
  materialRowsSaved: number;
  matchedInvoiceRows: number;
}

export interface PoBatchImportResultItem {
  originalFileName: string;
  purchaseOrderNo: string | null;
  isImported: boolean;
  isUpdated: boolean;
  materialRowsSaved: number;
  message: string;
}

export interface PoBatchImportResult {
  batchId: string;
  results: PoBatchImportResultItem[];
  totalProcessed: number;
  importedCount: number;
  updatedCount: number;
  failedCount: number;
  totalMaterialRowsSaved: number;
}
