export interface PurchaserPurchaseOrderPerson {
  name: string;
  purchaseOrderCount: number;
}

export interface PurchaserListItem {
  id: number;
  purchaserName: string;
  contactPerson: string;
  mobileNo: string;
  email: string;
  address: string;
  purchaseOrderCount: number;
  purchaseOrderPersons: PurchaserPurchaseOrderPerson[];
}

export interface PurchaserListResponse {
  totalCount: number;
  items: PurchaserListItem[];
}

export interface PurchaserDetails {
  id: number;
  purchaserName: string;
  contactPerson: string;
  mobileNo: string;
  email: string;
  address: string;
  purchaseOrderCount: number;
}

export interface PurchaserSaveRequest {
  purchaserName: string;
  contactPerson: string | null;
  mobileNo: string | null;
  email: string | null;
  address: string | null;
}

export interface PurchaserSaveResponse {
  success: boolean;
  message: string;
  purchaserId: number;
}

export interface PurchaserDeleteResponse {
  success: boolean;
  message: string;
  purchaserId: number;
}
