export interface MaterialPurchaseOrderOption {
  id: number;
  poNumber: string;
  handler: string;
  poDate: string | null;
  receivedDate: string | null;
}

export interface RelatedMaterialPurchaseOrder {
  purchaseOrderId: number;
  poNumber: string;
  handler: string;
  purchaserName: string;
  materialCount: number;
}

export interface MaterialDetails {
  id: number;
  purchaseOrderId: number;
  poNumber: string;
  handler: string;
  purchaserName: string;

  lineItem: string;
  itemType: string;
  materialNo: string;
  hsnSacCode: string;
  shortDescription: string;
  briefDescription: string;
  receiver: string;
  unloadingPoint: string;

  qty: number;
  unit: string;
  dep: string;
  dispatchQty: number;
  pendingQty: number;
  requiredQty: number;
  stockQty: number;
  liveStockQuantity: number;
  allocatedStockQuantity: number;
  stockMinimumQuantity: number;
  stockShortageQuantity: number;
  stockStatus: string;
  stockRecordCount: number;
  stockDataLive: boolean;

  invoiceQty: number;
  balanceQty: number;
  invoiceStatus: string;

  cost: number;
  valueOfGoods: number;
  priceUnit: string;
  totalValue: number;
  cgstRate: number;
  cgstAmount: number;
  sgstRate: number;
  sgstAmount: number;
  igstRate: number;
  igstAmount: number;
  totalValueWithTax: number;

  rmGrade: string;
  rmSize: string;
  rmSource: string;
  rmProcess: string;

  surfaceFinish: string;
  machiningType: string;

  processTime: string;
  materialProcessTime: string;

  poDate: string | null;
  receivingDate: string | null;
  deliveryDate: string | null;
  alertDate: string | null;
  dispatchDate: string | null;
  drawingUpdatedDate: string | null;
  processStartDate: string | null;

  drawingNo: string;
  drawingVersion: string;
  drawingFilePath: string;
  handSketchFilePath: string;
  outsourceDrawingFilePath: string;
  webDrawingFilePath: string;
  pdfFilePath: string;

  processStatus: string;
  dispatchStatus: string;
  assignedDashboard: string;
  sourcingType?: 'Inhouse' | 'Outsourcing';
  outsourcedTo?: string;
  hasCostVariation?: boolean;
  variationCost?: number | null;
  priorityLevel?: 'Normal' | 'Priority' | 'Urgent';
  remark: string;

  createdDate: string;
  updatedDate: string | null;

  invoiceItemCount: number;
  dispatchRecordCount: number;
  canDelete: boolean;
}

export interface MaterialUpsertRequest {
  purchaseOrderId: number;

  lineItem: string | null;
  itemType: string | null;
  materialNo: string | null;
  hsnSacCode: string | null;
  shortDescription: string | null;
  briefDescription: string | null;
  receiver: string | null;
  unloadingPoint: string | null;

  qty: number;
  unit: string | null;
  dep: string | null;

  cost: number;
  valueOfGoods: number;
  priceUnit: string | null;
  totalValue: number;

  rmGrade: string | null;
  rmSource: string | null;
  rmProcess: string | null;

  surfaceFinish: string | null;
  machiningType: string | null;

  processTime: string | null;
  materialProcessTime: string | null;

  poDate: string | null;
  receivingDate: string | null;
  deliveryDate: string | null;
  dispatchDate: string | null;
  drawingUpdatedDate: string | null;

  drawingNo: string | null;
  drawingVersion: string | null;

  processStatus: string | null;
  assignedDashboard: string | null;
  sourcingType?: 'Inhouse' | 'Outsourcing';
  outsourcedTo?: string | null;
  hasCostVariation?: boolean;
  variationCost?: number | null;
  priorityLevel?: 'Normal' | 'Priority' | 'Urgent';
  remark: string | null;
}

export interface MaterialSaveResponse {
  success: boolean;
  message: string;
  materialId: number;
  matchedPendingInvoiceRows: number;
  material: MaterialDetails | null;
}

export interface MaterialDeleteResponse {
  success: boolean;
  message: string;
  materialId: number;
}

export interface MaterialFileUpload {
  pdfFile?: File | null;
  drawingFile?: File | null;
  handSketchFile?: File | null;
  outsourceDrawingFile?: File | null;
  webDrawingFile?: File | null;
}



