export interface NotificationItem {
  id: number;
  materialId: number;
  materialNo: string;

  alertType: string;
  message: string;
  alertDate: string;

  isRead: boolean;
  isSentToMobile: boolean;
}

export interface NotificationSummary {
  totalAlerts: number;
  unreadCount: number;
  mobileQueue: number;
  dueToday: number;
}

export interface NotificationsResponse {
  summary: NotificationSummary;
  items: NotificationItem[];
}

export interface MarkNotificationReadResponse {
  success: boolean;
  message: string;
  notificationId: number;
  isRead: boolean;
}
