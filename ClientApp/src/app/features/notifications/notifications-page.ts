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
  NotificationExcelFilterColumn,
  NotificationExcelFilterSelections,
  NotificationExcelFilterValue,
  NotificationItem,
  NotificationsPagedResponse,
  NotificationsQuery,
  NotificationSortDirection,
} from '../../core/models/notification-api.model';

import {
  AuthApiService,
} from '../../core/services/auth-api.service';

import {
  NotificationApiService,
} from '../../core/services/notification-api.service';

import {
  ServerExcelFilter,
} from '../../shared/server-excel-filter/server-excel-filter';

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
    ServerExcelFilter,
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

  readonly columnFilters =
    new FormGroup({
      alertType:
        new FormControl('', {
          nonNullable: true,
        }),

      material:
        new FormControl('', {
          nonNullable: true,
        }),

      message:
        new FormControl('', {
          nonNullable: true,
        }),

      alertDateFrom:
        new FormControl('', {
          nonNullable: true,
        }),

      alertDateTo:
        new FormControl('', {
          nonNullable: true,
        }),

      mobileStatus:
        new FormControl<
          'all' | 'sent' | 'queued'
        >(
          'all',
          {
            nonNullable: true,
          },
        ),

      columnStatus:
        new FormControl<
          NotificationStatusFilter
        >(
          'all',
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

  readonly sortColumn =
    signal<
      NotificationExcelFilterColumn | null
    >(null);

  readonly sortDirection =
    signal<
      NotificationSortDirection | null
    >(null);

  readonly excelFilterSelections =
    signal<NotificationExcelFilterSelections>(
      {},
    );

  readonly excelFilterOptions =
    signal<
      Partial<
        Record<
          NotificationExcelFilterColumn,
          readonly NotificationExcelFilterValue[]
        >
      >
    >({});

  readonly excelFilterLoadingColumns =
    signal<
      ReadonlySet<
        NotificationExcelFilterColumn
      >
    >(
      new Set<
        NotificationExcelFilterColumn
      >(),
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

  excelFilterOptionsFor(
    column: NotificationExcelFilterColumn,
  ): readonly NotificationExcelFilterValue[] {
    return (
      this.excelFilterOptions()[
        column
      ] ?? []
    );
  }

  excelFilterSelectedValues(
    column: NotificationExcelFilterColumn,
  ): readonly string[] {
    return (
      this.excelFilterSelections()[
        column
      ] ?? []
    );
  }

  excelFilterActive(
    column: NotificationExcelFilterColumn,
  ): boolean {
    return Object.prototype
      .hasOwnProperty.call(
        this.excelFilterSelections(),
        column,
      );
  }

  excelFilterLoading(
    column: NotificationExcelFilterColumn,
  ): boolean {
    return this
      .excelFilterLoadingColumns()
      .has(
        column,
      );
  }

  excelSortDirection(
    column: NotificationExcelFilterColumn,
  ): NotificationSortDirection | null {
    return this.sortColumn() ===
      column
      ? this.sortDirection()
      : null;
  }

  loadExcelFilterValues(
    column: NotificationExcelFilterColumn,
  ): void {
    if (
      this.excelFilterLoading(
        column,
      )
    ) {
      return;
    }

    this.excelFilterLoadingColumns
      .update(
        columns => {
          const updated =
            new Set(
              columns,
            );

          updated.add(
            column,
          );

          return updated;
        },
      );

    this.api
      .getExcelFilterValues(
        this.buildQuery(),
        column,
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(
          () =>
            this.excelFilterLoadingColumns
              .update(
                columns => {
                  const updated =
                    new Set(
                      columns,
                    );

                  updated.delete(
                    column,
                  );

                  return updated;
                },
              ),
        ),
      )
      .subscribe({
        next: options => {
          this.excelFilterOptions
            .update(
              current => ({
                ...current,
                [column]:
                  options,
              }),
            );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
            ),
          );
        },
      });
  }

  applyExcelSort(
    column: NotificationExcelFilterColumn,
    direction: NotificationSortDirection,
  ): void {
    if (this.loading()) {
      return;
    }

    this.sortColumn.set(
      column,
    );

    this.sortDirection.set(
      direction,
    );

    this.page.set(1);
    this.load();
  }

  applyExcelFilter(
    column: NotificationExcelFilterColumn,
    values: readonly string[],
  ): void {
    const availableValues =
      this.excelFilterOptions()[
        column
      ] ?? [];

    const selectedSet =
      new Set(
        values,
      );

    const selectsEverything =
      availableValues.length >
        0 &&
      availableValues.length ===
        selectedSet.size &&
      availableValues.every(
        option =>
          selectedSet.has(
            option.value,
          ),
      );

    this.excelFilterSelections
      .update(
        current => {
          const updated = {
            ...current,
          };

          if (selectsEverything) {
            delete updated[
              column
            ];
          } else {
            updated[column] =
              [...values];
          }

          return updated;
        },
      );

    this.page.set(1);
    this.load();
  }

  clearExcelFilter(
    column: NotificationExcelFilterColumn,
  ): void {
    this.excelFilterSelections
      .update(
        current => {
          const updated = {
            ...current,
          };

          delete updated[
            column
          ];

          return updated;
        },
      );

    this.page.set(1);
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

    this.columnFilters.setValue({
      alertType: '',
      material: '',
      message: '',
      alertDateFrom: '',
      alertDateTo: '',
      mobileStatus: 'all',
      columnStatus: 'all',
    });

    this.excelFilterSelections.set(
      {},
    );

    this.page.set(1);
  }

  applyColumnFilter(
    filter: HTMLDetailsElement,
  ): void {
    filter.open = false;
    this.page.set(1);
    this.load();
  }

  clearColumnFilter(
    column:
      | 'type'
      | 'material'
      | 'message'
      | 'date'
      | 'mobile'
      | 'status',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'type':
        this.columnFilters.controls
          .alertType.setValue('');
        break;

      case 'material':
        this.columnFilters.controls
          .material.setValue('');
        break;

      case 'message':
        this.columnFilters.controls
          .message.setValue('');
        break;

      case 'date':
        this.columnFilters.patchValue({
          alertDateFrom: '',
          alertDateTo: '',
        });
        break;

      case 'mobile':
        this.columnFilters.controls
          .mobileStatus.setValue('all');
        break;

      case 'status':
        this.columnFilters.controls
          .columnStatus.setValue('all');
        break;
    }

    filter.open = false;
    this.page.set(1);
    this.load();
  }

  columnFilterActive(
    column:
      | 'type'
      | 'material'
      | 'message'
      | 'date'
      | 'mobile'
      | 'status',
  ): boolean {
    const values =
      this.columnFilters.getRawValue();

    switch (column) {
      case 'type':
        return Boolean(
          values.alertType.trim(),
        );

      case 'material':
        return Boolean(
          values.material.trim(),
        );

      case 'message':
        return Boolean(
          values.message.trim(),
        );

      case 'date':
        return Boolean(
          values.alertDateFrom ||
          values.alertDateTo,
        );

      case 'mobile':
        return (
          values.mobileStatus !== 'all'
        );

      case 'status':
        return (
          values.columnStatus !== 'all'
        );
    }

    return false;
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

    const columnFilters =
      this.columnFilters.getRawValue();

    return {
      search:
        filters.search,

      status:
        filters.status,

      alertType:
        columnFilters.alertType,

      material:
        columnFilters.material,

      message:
        columnFilters.message,

      alertDateFrom:
        columnFilters.alertDateFrom,

      alertDateTo:
        columnFilters.alertDateTo,

      mobileStatus:
        columnFilters.mobileStatus,

      columnStatus:
        columnFilters.columnStatus,

      sortColumn:
        this.sortColumn() ??
        undefined,

      sortDirection:
        this.sortDirection() ??
        undefined,

      excelFilters:
        this.serializeExcelFilters(),

      page:
        this.page(),

      pageSize:
        this.pageSize(),
    };
  }

  private serializeExcelFilters():
    string | undefined {
    const selections =
      this.excelFilterSelections();

    return Object.keys(
      selections,
    ).length > 0
      ? JSON.stringify(
          selections,
        )
      : undefined;
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
