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

    const textValues:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['search', query.search],
        ['alertType', query.alertType],
        ['material', query.material],
        ['message', query.message],
        ['alertDateFrom', query.alertDateFrom],
        ['alertDateTo', query.alertDateTo],
      ];

    for (
      const [
        name,
        value,
      ] of textValues
    ) {
      const normalized =
        value?.trim();

      if (normalized) {
        params =
          params.set(
            name,
            normalized,
          );
      }
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

    if (
      query.mobileStatus &&
      query.mobileStatus !== 'all'
    ) {
      params =
        params.set(
          'mobileStatus',
          query.mobileStatus,
        );
    }

    if (
      query.columnStatus &&
      query.columnStatus !== 'all'
    ) {
      params =
        params.set(
          'columnStatus',
          query.columnStatus,
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
