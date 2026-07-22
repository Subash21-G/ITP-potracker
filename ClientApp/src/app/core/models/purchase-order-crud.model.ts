export interface PurchaseOrderPurchaserOption {
  id: number;
  purchaserName: string;
}

export interface PurchaseOrderCrudDetails {
  id: number;
  purchaserId: number;
  purchaserName: string;
  poNumber: string;
  poDate: string | null;
  receivedDate: string | null;
  buyerContactPerson: string;
  pdfFilePath: string;
  createdDate: string;

  materialCount: number;
  totalQuantity: number;
  totalInvoiceQuantity: number;
  totalDispatchQuantity: number;
  totalPendingQuantity: number;
  totalValue: number;

  canDelete: boolean;
}

export interface PurchaseOrderUpsertRequest {
  purchaserId: number;
  poNumber: string;
  poDate: string | null;
  receivedDate: string | null;
  buyerContactPerson: string | null;
}

export interface PurchaseOrderSaveResponse {
  success: boolean;
  message: string;
  purchaseOrderId: number;
  purchaseOrder: PurchaseOrderCrudDetails | null;
}

export interface PurchaseOrderDeleteResponse {
  success: boolean;
  message: string;
  purchaseOrderId: number;
}
