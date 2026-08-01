import {
  DatePipe,
  DecimalPipe,
  Location,
} from '@angular/common';

import {
  HttpErrorResponse,
} from '@angular/common/http';

import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';

import {
  takeUntilDestroyed,
} from '@angular/core/rxjs-interop';

import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
} from '@angular/forms';

import {
  ActivatedRoute,
  Router,
  RouterLink,
} from '@angular/router';

import {
  EMPTY,
  Subject,
  catchError,
  debounceTime,
  defer,
  distinctUntilChanged,
  finalize,
  switchMap,
  tap,
} from 'rxjs';

import {
  NotificationItem,
  NotificationsPagedResponse,
  NotificationsQuery,
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

const EMPTY_RESPONSE:
  NotificationsPagedResponse = {
    generatedAt: '',
    summary: {
      totalAlerts: 0,
      unreadCount: 0,
      mobileQueue: 0,
      dueToday: 0,
    },
    items: [],
    page: 1,
    pageSize: 25,
    totalCount: 0,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  };

@Component({
  selector: 'app-notifications-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
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

  private readonly route =
    inject(ActivatedRoute);

  private readonly router =
    inject(Router);

  private readonly location =
    inject(Location);

  private readonly requestNotifications =
    new Subject<void>();

  private readonly initialParams =
    this.route.snapshot
      .queryParamMap;

  readonly filters =
    new FormGroup({
      search:
        new FormControl(
          this.initialParams.get(
            'search',
          ) ?? '',
          {
            nonNullable: true,
          },
        ),

      status:
        new FormControl<
          NotificationStatusFilter
        >(
          this.parseStatus(
            this.initialParams.get(
              'status',
            ),
          ),
          {
            nonNullable: true,
          },
        ),
    });

  readonly data =
    signal<NotificationsPagedResponse>(
      EMPTY_RESPONSE,
    );

  readonly page =
    signal(
      this.parsePositiveInteger(
        this.initialParams.get(
          'page',
        ),
        1,
      ),
    );

  readonly pageSize =
    signal(
      this.parsePageSize(
        this.initialParams.get(
          'pageSize',
        ),
      ),
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

  readonly startRecord =
    computed(() =>
      this.data().totalCount === 0
        ? 0
        : (
            this.data().page - 1
          ) *
            this.data().pageSize +
          1,
    );

  readonly endRecord =
    computed(() =>
      Math.min(
        this.data().page *
          this.data().pageSize,
        this.data().totalCount,
      ),
    );

  readonly visiblePages =
    computed(() => {
      const current =
        this.data().page;

      const total =
        this.data().totalPages;

      const first =
        Math.max(
          1,
          current - 2,
        );

      const last =
        Math.min(
          total,
          first + 4,
        );

      const adjustedFirst =
        Math.max(
          1,
          last - 4,
        );

      return Array.from(
        {
          length:
            last -
            adjustedFirst +
            1,
        },
        (
          _,
          index,
        ) =>
          adjustedFirst +
          index,
      );
    });

  constructor() {
    this.requestNotifications
      .pipe(
        switchMap(() =>
          defer(() => {
            this.loading.set(
              true,
            );

            this.errorMessage.set(
              '',
            );

            this.replaceUrlState();

            return this.api
              .getNotifications(
                this.buildQuery(),
              )
              .pipe(
                tap(
                  response => {
                    this.data.set(
                      response,
                    );

                    this.page.set(
                      response.page,
                    );

                    this.pageSize.set(
                      response.pageSize,
                    );
                  },
                ),

                catchError(
                  (
                    error:
                      HttpErrorResponse,
                  ) => {
                    this.errorMessage.set(
                      this.describeError(
                        error,
                      ),
                    );

                    return EMPTY;
                  },
                ),

                finalize(() =>
                  this.loading.set(
                    false,
                  ),
                ),
              );
          }),
        ),

        takeUntilDestroyed(
          this.destroyRef,
        ),
      )
      .subscribe();

    this.filters.valueChanges
      .pipe(
        debounceTime(320),

        distinctUntilChanged(
          (
            previous,
            current,
          ) =>
            JSON.stringify(
              previous,
            ) ===
            JSON.stringify(
              current,
            ),
        ),

        tap(() =>
          this.page.set(1),
        ),

        takeUntilDestroyed(
          this.destroyRef,
        ),
      )
      .subscribe(() =>
        this.load(),
      );

    this.load();
  }

  load(): void {
    this.requestNotifications.next();
  }

  clearFilters(): void {
    this.filters.setValue({
      search: '',
      status: 'all',
    });

    this.page.set(1);
  }

  changePageSize(
    value: string,
  ): void {
    const pageSize =
      this.parsePageSize(
        value,
      );

    if (
      pageSize ===
      this.pageSize()
    ) {
      return;
    }

    this.pageSize.set(
      pageSize,
    );

    this.page.set(1);
    this.load();
  }

  goToPage(
    page: number,
  ): void {
    const normalizedPage =
      Math.min(
        Math.max(
          page,
          1,
        ),
        this.data().totalPages,
      );

    if (
      normalizedPage ===
      this.page()
    ) {
      return;
    }

    this.page.set(
      normalizedPage,
    );

    this.load();
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
          ),
        ),
      )
      .subscribe({
        next: response => {
          if (!response.success) {
            return;
          }

          this.toastService.success(
            'Notification updated',
            response.message,
          );

          this.load();
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
      normalized.includes(
        'overdue',
      ) ||
      normalized.includes(
        'critical',
      )
    ) {
      return 'type-badge type-badge--danger';
    }

    if (
      normalized.includes(
        'dispatch',
      )
    ) {
      return 'type-badge type-badge--orange';
    }

    if (
      normalized.includes(
        'delivery',
      )
    ) {
      return 'type-badge type-badge--warning';
    }

    if (
      normalized.includes(
        'stock',
      )
    ) {
      return 'type-badge type-badge--purple';
    }

    return 'type-badge type-badge--info';
  }

  private buildQuery():
    NotificationsQuery {
    const filters =
      this.filters.getRawValue();

    return {
      search:
        filters.search,

      status:
        filters.status,

      page:
        this.page(),

      pageSize:
        this.pageSize(),
    };
  }

  private replaceUrlState(): void {
    const query =
      this.buildQuery();

    const urlTree =
      this.router.createUrlTree(
        [],
        {
          relativeTo:
            this.route,

          queryParams: {
            search:
              query.search?.trim() ||
              null,

            status:
              query.status !== 'all'
                ? query.status
                : null,

            page:
              query.page > 1
                ? query.page
                : null,

            pageSize:
              query.pageSize !== 25
                ? query.pageSize
                : null,
          },
        },
      );

    this.location.replaceState(
      this.router.serializeUrl(
        urlTree,
      ),
    );
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

  private parseStatus(
    value:
      | string
      | null,
  ): NotificationStatusFilter {
    return value === 'read' ||
      value === 'unread'
      ? value
      : 'all';
  }

  private parsePositiveInteger(
    value:
      | string
      | null,
    fallback: number,
  ): number {
    const parsed =
      Number(value);

    return Number.isInteger(
      parsed,
    ) &&
      parsed > 0
      ? parsed
      : fallback;
  }

  private parsePageSize(
    value:
      | string
      | null,
  ): number {
    const parsed =
      Number(value);

    return parsed === 50 ||
      parsed === 100
      ? parsed
      : 25;
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

    return typeof message ===
      'string'
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

    return typeof message ===
      'string'
      ? message
      : 'The notification could not be marked as read.';
  }
}
