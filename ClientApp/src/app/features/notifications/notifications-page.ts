import {
  DatePipe,
} from '@angular/common';
import {
  HttpErrorResponse,
} from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import {
  takeUntilDestroyed,
} from '@angular/core/rxjs-interop';
import {
  FormsModule,
} from '@angular/forms';
import {
  RouterLink,
} from '@angular/router';
import {
  finalize,
} from 'rxjs';

import {
  NotificationItem,
  NotificationsResponse,
} from '../../core/models/notification-api.model';
import {
  AuthApiService,
} from '../../core/services/auth-api.service';
import {
  NotificationApiService,
} from '../../core/services/notification-api.service';
import {
  ToastService,
} from '../../core/services/toast.service';

type NotificationStatusFilter =
  | 'all'
  | 'unread'
  | 'read';

const EMPTY_RESPONSE: NotificationsResponse = {
  summary: {
    totalAlerts: 0,
    unreadCount: 0,
    mobileQueue: 0,
    dueToday: 0,
  },
  items: [],
};

@Component({
  selector: 'app-notifications-page',
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
  ],
  templateUrl:
    './notifications-page.html',
  styleUrl:
    './notifications-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class NotificationsPage {
  private readonly api =
    inject(NotificationApiService);

  private readonly authApi =
    inject(AuthApiService);

  private readonly toastService =
    inject(ToastService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly data =
    signal<NotificationsResponse>(
      EMPTY_RESPONSE,
    );

  readonly loading =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly markingReadIds =
    signal<ReadonlySet<number>>(
      new Set<number>(),
    );

  readonly canOperate =
    this.authApi.canOperate;

  searchText = '';

  statusFilter:
    NotificationStatusFilter =
      'all';

  filteredItems():
    NotificationItem[] {
    const search =
      this.searchText
        .trim()
        .toLowerCase();

    const status =
      this.statusFilter;

    return this.data()
      .items
      .filter(item => {
        if (
          status === 'unread' &&
          item.isRead
        ) {
          return false;
        }

        if (
          status === 'read' &&
          !item.isRead
        ) {
          return false;
        }

        if (!search) {
          return true;
        }

        return (
          item.materialNo
            .toLowerCase()
            .includes(search) ||

          item.alertType
            .toLowerCase()
            .includes(search) ||

          item.message
            .toLowerCase()
            .includes(search)
        );
      });
  }

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.api
      .getNotifications()
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.loading.set(false)),
      )
      .subscribe({
        next: response =>
          this.data.set(response),

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  markRead(
    item: NotificationItem,
  ): void {
    if (
      item.isRead ||
      !this.canOperate() ||
      this.isMarkingRead(item.id)
    ) {
      return;
    }

    this.addMarkingReadId(
      item.id,
    );

    this.api
      .markRead(item.id)
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.removeMarkingReadId(
            item.id,
          )),
      )
      .subscribe({
        next: response => {
          if (!response.success) {
            return;
          }

          this.data.update(current => {
            const items =
              current.items.map(
                notification =>
                  notification.id ===
                  item.id
                    ? {
                        ...notification,
                        isRead: true,
                      }
                    : notification,
              );

            return {
              ...current,
              items,
              summary: {
                ...current.summary,
                unreadCount:
                  Math.max(
                    0,
                    current.summary
                      .unreadCount - 1,
                  ),
              },
            };
          });

          this.toastService.success(
            'Notification updated',
            response.message,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          const message =
            this.describeMarkReadError(
              error,
            );

          this.errorMessage.set(
            message,
          );

          this.toastService.error(
            'Unable to update notification',
            message,
          );
        },
      });
  }

  clearFilters(): void {
    this.searchText = '';
    this.statusFilter = 'all';
  }

  isMarkingRead(
    notificationId: number,
  ): boolean {
    return this.markingReadIds()
      .has(notificationId);
  }

  alertTypeClass(
    alertType: string,
  ): string {
    const normalized =
      alertType.toLowerCase();

    if (
      normalized.includes('overdue') ||
      normalized.includes('critical')
    ) {
      return 'type-badge type-badge--danger';
    }

    if (
      normalized.includes('dispatch')
    ) {
      return 'type-badge type-badge--orange';
    }

    if (
      normalized.includes('delivery')
    ) {
      return 'type-badge type-badge--warning';
    }

    if (
      normalized.includes('stock')
    ) {
      return 'type-badge type-badge--purple';
    }

    return 'type-badge type-badge--info';
  }

  private addMarkingReadId(
    notificationId: number,
  ): void {
    this.markingReadIds.update(
      current => {
        const updated =
          new Set(current);

        updated.add(
          notificationId,
        );

        return updated;
      },
    );
  }

  private removeMarkingReadId(
    notificationId: number,
  ): void {
    this.markingReadIds.update(
      current => {
        const updated =
          new Set(current);

        updated.delete(
          notificationId,
        );

        return updated;
      },
    );
  }

  private describeError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to view notifications.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : 'Notifications could not be loaded.';
  }

  private describeMarkReadError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 403) {
      return 'Only Admin and Production users can mark notifications as read.';
    }

    if (error.status === 404) {
      return 'The selected notification was not found.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : 'The notification could not be marked as read.';
  }
}

