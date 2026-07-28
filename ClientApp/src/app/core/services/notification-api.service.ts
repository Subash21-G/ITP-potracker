import {
  HttpClient,
} from '@angular/common/http';
import {
  Injectable,
  inject,
} from '@angular/core';
import {
  Observable,
} from 'rxjs';

import {
  MarkNotificationReadResponse,
  NotificationsResponse,
} from '../models/notification-api.model';

@Injectable({
  providedIn: 'root',
})
export class NotificationApiService {
  private readonly http =
    inject(HttpClient);

  getNotifications():
    Observable<NotificationsResponse> {
    return this.http.get<NotificationsResponse>(
      '/api/notifications',
      {
        withCredentials: true,
      },
    );
  }

  markRead(
    notificationId: number,
  ): Observable<MarkNotificationReadResponse> {
    return this.http.post<MarkNotificationReadResponse>(
      `/api/notifications/${notificationId}/read`,
      {},
      {
        withCredentials: true,
      },
    );
  }
}
