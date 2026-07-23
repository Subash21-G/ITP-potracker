export interface DispatchSummary {
  totalPendingMaterials: number;
  partialMaterials: number;
  totalPOQuantity: number;
  totalDispatchedQuantity: number;
  totalPendingQuantity: number;
}

export interface DispatchMaterialListItem {
  materialId: number;
  purchaseOrderId: number;
  poNumber: string;
  purchaserName: string;
  lineItem: string;
  materialNo: string;
  description: string;
  poQuantity: number;
  dispatchQuantity: number;
  pendingQuantity: number;
  deliveryDate: string | null;
  lastDispatchDate: string | null;
  dispatchStatus: string;
}

export interface DispatchListResponse {
  items: DispatchMaterialListItem[];
  summary: DispatchSummary;
}

export interface DispatchListQuery {
  search?: string;
  status?: string;
  take?: number;
}

export interface DispatchMaterialDetails {
  materialId: number;
  purchaseOrderId: number;
  poNumber: string;
  purchaserName: string;
  lineItem: string;
  materialNo: string;
  description: string;
  poQuantity: number;
  invoiceQuantity: number;
  dispatchQuantity: number;
  pendingQuantity: number;
  deliveryDate: string | null;
  lastDispatchDate: string | null;
  dispatchStatus: string;
}

export interface DispatchHistoryItem {
  id: number;
  materialId: number;
  dispatchNo: string;
  dispatchDate: string;
  dispatchQuantity: number;
  remarks: string;
  fileName: string;
  filePath: string;
  createdDate: string;
}

export interface DispatchDetailsResponse {
  material: DispatchMaterialDetails;
  dispatches: DispatchHistoryItem[];
}

export interface CreateDispatchRequest {
  materialId: number;
  dispatchNo: string;
  dispatchDate: string;
  dispatchQuantity: number;
  remarks?: string;
  dispatchFile?: File | null;
}

export interface DispatchMutationResponse {
  success: boolean;
  message: string;
  materialId: number;
  dispatchId: number | null;
  dispatchQuantity: number;
  pendingQuantity: number;
  dispatchStatus: string;
}

export interface DispatchBulkDeleteRequest {
  materialId: number;
  dispatchIds: number[];
}
