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

  material?: string;
  description?: string;
  poNumber?: string;
  purchaser?: string;

  processStartFrom?: string;
  processStartTo?: string;

  deliveryFrom?: string;
  deliveryTo?: string;

  owner?: string;
  processStatus?: string;
  dispatchStatus?: string;

  page?: number;
  pageSize?: number;
}

export interface ProductionCalendarEvent {
  id: string;
  materialId: number;
  purchaseOrderId: number;
  poNumber: string;
  materialNo: string;
  description: string;
  unit: string;
  title: string;
  start: string;
  color: string;
  eventType: string;
  status: string;
  orderedQty: number;
  dispatchQty: number;
  pendingQty: number;
  processStartDate: string | null;
  dispatchDate: string | null;
  deliveryDate: string | null;
  handler: string;
  department: string;
  remarks: string;
  daysOverdue: number;
  allDay: boolean;
}

export interface ProductionCalendarQuery {
  start: string;
  end: string;
  search?: string;
  eventTypes?: readonly string[];
  statuses?: readonly string[];
  handler?: string;
  department?: string;
  poNumber?: string;
}

export interface ProductionCalendarDashboard {
  overdue: number;
  dueToday: number;
  nextSevenDays: number;
  completedThisMonth: number;
  totalMaterials: number;
  processStartEvents: number;
  dispatchEvents: number;
  deliveryEvents: number;
  overdueEvents: number;
  revisedEvents: number;
  completedEvents: number;
  upcoming: ProductionCalendarUpcomingItem[];
  overdueItems: ProductionCalendarOverdueItem[];
  handlerWorkload: ProductionCalendarHandlerWorkload[];
}

export interface ProductionCalendarUpcomingItem {
  materialId: number;
  purchaseOrderId: number;
  poNumber: string;
  materialNo: string;
  eventType: string;
  eventDate: string;
  pendingQty: number;
  unit: string;
}

export interface ProductionCalendarOverdueItem {
  materialId: number;
  purchaseOrderId: number;
  poNumber: string;
  materialNo: string;
  deliveryDate: string;
  daysOverdue: number;
}

export interface ProductionCalendarHandlerWorkload {
  handler: string;
  activeMaterials: number;
  level: string;
}