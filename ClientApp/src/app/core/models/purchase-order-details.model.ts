import { PagedResult } from './material.model';

export interface PurchaseOrderDetails {
  readonly purchaseOrderId: number;
  readonly poNumber: string;
  readonly poDate: string | null;
  readonly poReceivedDate: string | null;
  readonly handler: string;
  readonly buyerEmail: string | null;
  readonly vendorCode: string | null;
  readonly vendorName: string | null;
  readonly paymentTerms: string | null;
  readonly deliveryCondition: string | null;
  readonly currency: string | null;
  readonly basicValue: number;
  readonly taxValue: number;
  readonly grandTotal: number;
  readonly importFormat: string | null;
  readonly pdfFilePath: string | null;
  readonly status: string;
  readonly createdDate: string;
  readonly updatedDate: string | null;
  readonly summary: PurchaseOrderDetailsSummary;
  readonly materials: PagedResult<PurchaseOrderMaterial>;
}

export interface PurchaseOrderDetailsSummary {
  readonly totalMaterials: number;
  readonly completedMaterials: number;
  readonly pendingMaterials: number;
  readonly overdueMaterials: number;
  readonly totalPOQuantity: number;
  readonly totalInvoiceBalance: number;
  readonly totalDispatchBalance: number;
}

export interface PurchaseOrderMaterial {
  readonly id: number;
  readonly lineItem: string;
  readonly materialNo: string;
  readonly hsnSacCode: string | null;
  readonly description: string;
  readonly drawingNo: string;
  readonly quantity: number;
  readonly unit: string;
  readonly invoiceQuantity: number;
  readonly invoiceBalanceQuantity: number;
  readonly dispatchQuantity: number;
  readonly dispatchBalanceQuantity: number;
  readonly deliveryDate: string | null;
  readonly processStatus: string;
  readonly invoiceStatus: string;
  readonly dispatchStatus: string;
  readonly isOverdue: boolean;
}
