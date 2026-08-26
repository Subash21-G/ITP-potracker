export interface NotificationItem {
  readonly id: number;
  readonly materialId: number;
  readonly materialNo: string;

  readonly alertType: string;
  readonly message: string;
  readonly alertDate: string;

  readonly isRead: boolean;
  readonly isSentToMobile: boolean;
}

export type NotificationSortColumn =
  | 'type'
  | 'material'
  | 'message'
  | 'alertDate'
  | 'mobile'
  | 'status';

export type NotificationSortDirection =
  | 'asc'
  | 'desc';

export type NotificationExcelFilterColumn =
  NotificationSortColumn;

export interface NotificationExcelFilterValue {
  readonly value: string;
  readonly label: string;
  readonly count: number;
  readonly selected?: boolean;
}

export type NotificationExcelFilterSelections =
  Partial<
    Record<
      NotificationExcelFilterColumn,
      readonly string[]
    >
  >;

export interface NotificationSummary {
  readonly totalAlerts: number;
  readonly unreadCount: number;
  readonly mobileQueue: number;
  readonly dueToday: number;
}

export interface NotificationsQuery {
  readonly search?: string;

  readonly status?:
    | 'all'
    | 'unread'
    | 'read';

  readonly alertType?: string;
  readonly material?: string;
  readonly message?: string;

  readonly alertDateFrom?: string;
  readonly alertDateTo?: string;

  readonly mobileStatus?:
    | 'all'
    | 'sent'
    | 'queued';

  readonly columnStatus?:
    | 'all'
    | 'unread'
    | 'read';

  readonly sortColumn?: NotificationSortColumn;
  readonly sortDirection?: NotificationSortDirection;
  readonly excelFilters?: string;

  readonly page: number;
  readonly pageSize: number;
}

export interface NotificationsPagedResponse {
  readonly generatedAt: string;
  readonly summary: NotificationSummary;
  readonly items:
    readonly NotificationItem[];

  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasPreviousPage: boolean;
  readonly hasNextPage: boolean;
}

export interface MarkNotificationReadResponse {
  readonly success: boolean;
  readonly message: string;
  readonly notificationId: number;
  readonly isRead: boolean;
}
