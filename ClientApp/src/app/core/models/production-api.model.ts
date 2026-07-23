export interface ProcessQueueSummary {
  totalMaterials: number;
  overdue: number;
  startToday: number;
  thisWeek: number;
  future: number;
}

export interface ProcessQueueItem {
  materialId: number;
  purchaseOrderId: number;
  poNumber: string;
  purchaserName: string;
  materialNo: string;
  description: string;
  processStartDate: string | null;
  deliveryDate: string | null;
  dispatchDate: string | null;
  assignedDashboard: string;
  processStatus: string;
  dispatchStatus: string;
}

export interface ProcessQueueResponse {
  summary: ProcessQueueSummary;
  overdue: ProcessQueueItem[];
  startToday: ProcessQueueItem[];
  thisWeek: ProcessQueueItem[];
  future: ProcessQueueItem[];
  items: ProcessQueueItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ProcessQueueQuery {
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface ProductionCalendarEvent {
  id: number;
  materialId: number;
  title: string;
  start: string;
  color: string;
  eventType: string;
  allDay: boolean;
}
