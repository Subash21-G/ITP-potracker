import {
  HttpClient,
  HttpParams,
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
  NotificationsPagedResponse,
  NotificationsQuery,
} from '../models/notification-api.model';

@Injectable({
  providedIn: 'root',
})
export class NotificationApiService {
  private readonly http =
    inject(HttpClient);

  getNotifications(
    query: NotificationsQuery,
  ): Observable<NotificationsPagedResponse> {
    let params =
      new HttpParams()
        .set(
          'page',
          query.page.toString(),
        )
        .set(
          'pageSize',
          query.pageSize.toString(),
        );

    const search =
      query.search?.trim();

    if (search) {
      params =
        params.set(
          'search',
          search,
        );
    }

    if (
      query.status &&
      query.status !== 'all'
    ) {
      params =
        params.set(
          'status',
          query.status,
        );
    }

    return this.http.get<
      NotificationsPagedResponse
    >(
      '/api/notifications',
      {
        params,
        withCredentials: true,
      },
    );
  }

  markRead(
    notificationId: number,
  ): Observable<MarkNotificationReadResponse> {
    return this.http.post<
      MarkNotificationReadResponse
    >(
      `/api/notifications/${notificationId}/read`,
      {},
      {
        withCredentials: true,
      },
    );
  }
}
