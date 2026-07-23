export interface MaterialImportRow {
  rowNumber: number;

  purchaserName: string;
  contactPerson: string | null;

  poNumber: string;
  poDate: string | null;
  receivedDate: string | null;
  buyerContactPerson: string | null;

  lineItem: string;
  materialNo: string;
  hsnSacCode: string | null;
  shortDescription: string | null;
  briefDescription: string | null;

  qty: number;
  unit: string | null;
  dep: string | null;

  valueOfGoods: number;
  priceUnit: string | null;
  totalValue: number;

  deliveryDate: string | null;
  drawingNo: string | null;

  assignedDashboard: string | null;
  remark: string | null;

  isValid: boolean;
  errors: string[];
  errorMessage: string;
}

export interface MaterialImportPreview {
  batchId: string;
  originalFileName: string;
  createdAt: string;

  rows: MaterialImportRow[];

  totalRows: number;
  validRows: number;
  invalidRows: number;

  totalQuantity: number;
  totalValue: number;
}

export interface MaterialImportCommitResponse {
  message: string;
  batchId: string;
}

export interface MaterialImportCancelResponse {
  message: string;
}
