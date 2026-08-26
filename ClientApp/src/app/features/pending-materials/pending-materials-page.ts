import {
  DatePipe,
  DecimalPipe,
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
  FormsModule,
} from '@angular/forms';
import {
  RouterLink,
} from '@angular/router';
import {
  finalize,
} from 'rxjs';

import {
  PendingMaterialExcelFilterColumn,
  PendingMaterialExcelFilterSelections,
  PendingMaterialExcelFilterValue,
  PendingMaterialQuery,
  PendingMaterialSortDirection,
  PendingMaterialsResponse,
} from '../../core/models/pending-material-api.model';
import {
  PendingMaterialApiService,
} from '../../core/services/pending-material-api.service';
import {
  ServerExcelFilter,
} from '../../shared/server-excel-filter/server-excel-filter';

const EMPTY_RESPONSE: PendingMaterialsResponse = {
  items: [],
  summary: {
    totalPendingMaterials: 0,
    overdueMaterials: 0,
    dueTodayMaterials: 0,
    dueThisWeekMaterials: 0,
    withoutDeliveryDate: 0,
    totalPOQuantity: 0,
    totalInvoiceQuantity: 0,
    totalInvoiceBalanceQuantity: 0,
    totalDispatchQuantity: 0,
    totalPendingDispatchQuantity: 0,
    totalPendingValue: 0,
    assignmentSummary: [],
  },
  filters: {
    handlers: [],
    assignedDashboards: [],
    processStatuses: [],
    invoiceStatuses: [],
    dispatchStatuses: [],
  },
  page: 1,
  pageSize: 25,
  totalRecords: 0,
  totalPages: 1,
};

@Component({
  selector: 'app-pending-materials-page',
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterLink,
    ServerExcelFilter,
  ],
  templateUrl:
    './pending-materials-page.html',
  styleUrl:
    './pending-materials-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class PendingMaterialsPage {
  private readonly api =
    inject(PendingMaterialApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly data =
    signal<PendingMaterialsResponse>(
      EMPTY_RESPONSE,
    );

  readonly loading =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly showAdvancedFilters =
    signal(false);

  search = '';
  poNumber = '';
  handler = '';
  assignedDashboard = '';
  processStatus = '';
  invoiceStatus = '';
  dispatchStatus = '';

  poDateFrom = '';
  poDateTo = '';
  poReceivedDateFrom = '';
  poReceivedDateTo = '';
  deliveryDateFrom = '';
  deliveryDateTo = '';

  overdueOnly = false;
  dueThisWeekOnly = false;

  dueStatus = '';

  columnPoNumber = '';
  columnHandler = '';

  columnMaterialNo = '';
  columnLineItem = '';

  columnDescription = '';

  minPoQuantity = '';
  maxPoQuantity = '';

  minInvoiceQuantity = '';
  maxInvoiceQuantity = '';

  minInvoiceBalanceQuantity = '';
  maxInvoiceBalanceQuantity = '';

  minDispatchQuantity = '';
  maxDispatchQuantity = '';

  minPendingDispatchQuantity = '';
  maxPendingDispatchQuantity = '';

  minCompletionPercentage = '';
  maxCompletionPercentage = '';

  columnProcessStatus = '';
  columnInvoiceStatus = '';
  columnDispatchStatus = '';

  columnDeliveryDateFrom = '';
  columnDeliveryDateTo = '';

  columnAssignedDashboard = '';

  readonly sortColumn =
    signal<
      PendingMaterialExcelFilterColumn | null
    >(
      null,
    );

  readonly sortDirection =
    signal<
      PendingMaterialSortDirection | null
    >(
      null,
    );

  readonly excelFilterSelections =
    signal<
      PendingMaterialExcelFilterSelections
    >(
      {},
    );

  readonly excelFilterOptions =
    signal<
      Partial<
        Record<
          PendingMaterialExcelFilterColumn,
          readonly PendingMaterialExcelFilterValue[]
        >
      >
    >({});

  readonly excelFilterLoadingColumns =
    signal<
      ReadonlySet<
        PendingMaterialExcelFilterColumn
      >
    >(
      new Set<
        PendingMaterialExcelFilterColumn
      >(),
    );

  page = 1;
  pageSize = 25;

  readonly firstRecord =
    computed(() => {
      const response =
        this.data();

      if (
        response.totalRecords === 0
      ) {
        return 0;
      }

      return (
        (response.page - 1) *
        response.pageSize
      ) + 1;
    });

  readonly lastRecord =
    computed(() => {
      const response =
        this.data();

      return Math.min(
        response.page *
          response.pageSize,
        response.totalRecords,
      );
    });

  constructor() {
    this.load();
  }

  load(
    requestedPage = this.page,
  ): void {
    this.page =
      Math.max(1, requestedPage);

    this.loading.set(true);
    this.errorMessage.set('');

    this.api
      .getPendingMaterials(
        this.buildQuery(),
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.data.set(response);
          this.page = response.page;
          this.pageSize =
            response.pageSize;
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  excelFilterOptionsFor(
    column: PendingMaterialExcelFilterColumn,
  ): readonly PendingMaterialExcelFilterValue[] {
    return (
      this.excelFilterOptions()[
        column
      ] ?? []
    );
  }

  excelFilterSelectedValues(
    column: PendingMaterialExcelFilterColumn,
  ): readonly string[] {
    return (
      this.excelFilterSelections()[
        column
      ] ?? []
    );
  }

  excelFilterActive(
    column: PendingMaterialExcelFilterColumn,
  ): boolean {
    return Object.prototype
      .hasOwnProperty.call(
        this.excelFilterSelections(),
        column,
      );
  }

  excelFilterLoading(
    column: PendingMaterialExcelFilterColumn,
  ): boolean {
    return this
      .excelFilterLoadingColumns()
      .has(
        column,
      );
  }

  excelSortDirection(
    column: PendingMaterialExcelFilterColumn,
  ): PendingMaterialSortDirection | null {
    return this.sortColumn() ===
      column
      ? this.sortDirection()
      : null;
  }

  loadExcelFilterValues(
    column: PendingMaterialExcelFilterColumn,
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
        this.buildQuery(
          false,
        ),
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
    column: PendingMaterialExcelFilterColumn,
    direction:
      PendingMaterialSortDirection,
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

    this.load(1);
  }

  applyExcelFilter(
    column: PendingMaterialExcelFilterColumn,
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

    this.load(1);
  }

  clearExcelFilter(
    column: PendingMaterialExcelFilterColumn,
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

    this.load(1);
  }

  applyFilters(): void {
    this.load(1);
  }

  applyColumnFilter(
    filter: HTMLDetailsElement,
  ): void {
    filter.open = false;
    this.load(1);
  }

  clearColumnFilter(
    column:
      | 'due'
      | 'po'
      | 'material'
      | 'description'
      | 'poQty'
      | 'invoice'
      | 'invoiceBalance'
      | 'dispatch'
      | 'pendingDispatch'
      | 'progress'
      | 'process'
      | 'invoiceStatus'
      | 'dispatchStatus'
      | 'delivery'
      | 'assigned',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'due':
        this.dueStatus = '';
        break;

      case 'po':
        this.columnPoNumber = '';
        this.columnHandler = '';
        break;

      case 'material':
        this.columnMaterialNo = '';
        this.columnLineItem = '';
        break;

      case 'description':
        this.columnDescription = '';
        break;

      case 'poQty':
        this.minPoQuantity = '';
        this.maxPoQuantity = '';
        break;

      case 'invoice':
        this.minInvoiceQuantity = '';
        this.maxInvoiceQuantity = '';
        break;

      case 'invoiceBalance':
        this.minInvoiceBalanceQuantity = '';
        this.maxInvoiceBalanceQuantity = '';
        break;

      case 'dispatch':
        this.minDispatchQuantity = '';
        this.maxDispatchQuantity = '';
        break;

      case 'pendingDispatch':
        this.minPendingDispatchQuantity = '';
        this.maxPendingDispatchQuantity = '';
        break;

      case 'progress':
        this.minCompletionPercentage = '';
        this.maxCompletionPercentage = '';
        break;

      case 'process':
        this.columnProcessStatus = '';
        break;

      case 'invoiceStatus':
        this.columnInvoiceStatus = '';
        break;

      case 'dispatchStatus':
        this.columnDispatchStatus = '';
        break;

      case 'delivery':
        this.columnDeliveryDateFrom = '';
        this.columnDeliveryDateTo = '';
        break;

      case 'assigned':
        this.columnAssignedDashboard = '';
        break;
    }

    filter.open = false;
    this.load(1);
  }

  columnFilterActive(
    column:
      | 'due'
      | 'po'
      | 'material'
      | 'description'
      | 'poQty'
      | 'invoice'
      | 'invoiceBalance'
      | 'dispatch'
      | 'pendingDispatch'
      | 'progress'
      | 'process'
      | 'invoiceStatus'
      | 'dispatchStatus'
      | 'delivery'
      | 'assigned',
  ): boolean {
    switch (column) {
      case 'due':
        return Boolean(
          this.dueStatus,
        );

      case 'po':
        return Boolean(
          this.columnPoNumber.trim() ||
          this.columnHandler.trim(),
        );

      case 'material':
        return Boolean(
          this.columnMaterialNo.trim() ||
          this.columnLineItem.trim(),
        );

      case 'description':
        return Boolean(
          this.columnDescription.trim(),
        );

      case 'poQty':
        return Boolean(
          this.minPoQuantity.trim() ||
          this.maxPoQuantity.trim(),
        );

      case 'invoice':
        return Boolean(
          this.minInvoiceQuantity.trim() ||
          this.maxInvoiceQuantity.trim(),
        );

      case 'invoiceBalance':
        return Boolean(
          this.minInvoiceBalanceQuantity.trim() ||
          this.maxInvoiceBalanceQuantity.trim(),
        );

      case 'dispatch':
        return Boolean(
          this.minDispatchQuantity.trim() ||
          this.maxDispatchQuantity.trim(),
        );

      case 'pendingDispatch':
        return Boolean(
          this.minPendingDispatchQuantity.trim() ||
          this.maxPendingDispatchQuantity.trim(),
        );

      case 'progress':
        return Boolean(
          this.minCompletionPercentage.trim() ||
          this.maxCompletionPercentage.trim(),
        );

      case 'process':
        return Boolean(
          this.columnProcessStatus,
        );

      case 'invoiceStatus':
        return Boolean(
          this.columnInvoiceStatus,
        );

      case 'dispatchStatus':
        return Boolean(
          this.columnDispatchStatus,
        );

      case 'delivery':
        return Boolean(
          this.columnDeliveryDateFrom ||
          this.columnDeliveryDateTo,
        );

      case 'assigned':
        return Boolean(
          this.columnAssignedDashboard,
        );
    }

    return false;
  }

  clearFilters(): void {
    this.search = '';
    this.poNumber = '';
    this.handler = '';
    this.assignedDashboard = '';
    this.processStatus = '';
    this.invoiceStatus = '';
    this.dispatchStatus = '';

    this.poDateFrom = '';
    this.poDateTo = '';
    this.poReceivedDateFrom = '';
    this.poReceivedDateTo = '';
    this.deliveryDateFrom = '';
    this.deliveryDateTo = '';

    this.overdueOnly = false;
    this.dueThisWeekOnly = false;

    this.dueStatus = '';
    this.columnPoNumber = '';
    this.columnHandler = '';
    this.columnMaterialNo = '';
    this.columnLineItem = '';
    this.columnDescription = '';
    this.minPoQuantity = '';
    this.maxPoQuantity = '';
    this.minInvoiceQuantity = '';
    this.maxInvoiceQuantity = '';
    this.minInvoiceBalanceQuantity = '';
    this.maxInvoiceBalanceQuantity = '';
    this.minDispatchQuantity = '';
    this.maxDispatchQuantity = '';
    this.minPendingDispatchQuantity = '';
    this.maxPendingDispatchQuantity = '';
    this.minCompletionPercentage = '';
    this.maxCompletionPercentage = '';
    this.columnProcessStatus = '';
    this.columnInvoiceStatus = '';
    this.columnDispatchStatus = '';
    this.columnDeliveryDateFrom = '';
    this.columnDeliveryDateTo = '';
    this.columnAssignedDashboard = '';

    this.excelFilterSelections.set(
      {},
    );

    this.sortColumn.set(
      null,
    );

    this.sortDirection.set(
      null,
    );

    this.load(1);
  }

  toggleAdvancedFilters(): void {
    this.showAdvancedFilters.update(
      value => !value,
    );
  }

  setPageSize(): void {
    this.load(1);
  }

  previousPage(): void {
    if (this.data().page > 1) {
      this.load(
        this.data().page - 1,
      );
    }
  }

  nextPage(): void {
    const response =
      this.data();

    if (
      response.page <
      response.totalPages
    ) {
      this.load(
        response.page + 1,
      );
    }
  }

  exportExcel(): void {
    window.location.href =
      this.api.buildExcelExportUrl(
        this.buildQuery(),
      );
  }

  dueClass(
    dueStatus: string,
  ): string {
    switch (dueStatus) {
      case 'Overdue':
        return 'status status--danger';

      case 'Due Today':
        return 'status status--warning';

      case 'Due This Week':
        return 'status status--info';

      case 'No Delivery Date':
        return 'status status--neutral';

      default:
        return 'status status--success';
    }
  }

  statusClass(
    status: string,
  ): string {
    const normalized =
      status.toLowerCase();

    if (
      normalized.includes('completed') ||
      normalized.includes('fully') ||
      normalized.includes('dispatched')
    ) {
      return 'status status--success';
    }

    if (
      normalized.includes('partial') ||
      normalized.includes('progress')
    ) {
      return 'status status--info';
    }

    if (
      normalized.includes('overdue') ||
      normalized.includes('failed')
    ) {
      return 'status status--danger';
    }

    return 'status status--warning';
  }

  private buildQuery(
    includePaging = true,
  ): PendingMaterialQuery {
    return {
      search: this.search,
      poNumber: this.poNumber,
      handler: this.handler,
      assignedDashboard:
        this.assignedDashboard,
      processStatus:
        this.processStatus,
      invoiceStatus:
        this.invoiceStatus,
      dispatchStatus:
        this.dispatchStatus,

      poDateFrom:
        this.poDateFrom,
      poDateTo:
        this.poDateTo,

      poReceivedDateFrom:
        this.poReceivedDateFrom,
      poReceivedDateTo:
        this.poReceivedDateTo,

      deliveryDateFrom:
        this.deliveryDateFrom,
      deliveryDateTo:
        this.deliveryDateTo,

      overdueOnly:
        this.overdueOnly,
      dueThisWeekOnly:
        this.dueThisWeekOnly,

      dueStatus:
        this.dueStatus,

      columnPoNumber:
        this.columnPoNumber,
      columnHandler:
        this.columnHandler,

      columnMaterialNo:
        this.columnMaterialNo,
      columnLineItem:
        this.columnLineItem,

      columnDescription:
        this.columnDescription,

      minPoQuantity:
        this.minPoQuantity,
      maxPoQuantity:
        this.maxPoQuantity,

      minInvoiceQuantity:
        this.minInvoiceQuantity,
      maxInvoiceQuantity:
        this.maxInvoiceQuantity,

      minInvoiceBalanceQuantity:
        this.minInvoiceBalanceQuantity,
      maxInvoiceBalanceQuantity:
        this.maxInvoiceBalanceQuantity,

      minDispatchQuantity:
        this.minDispatchQuantity,
      maxDispatchQuantity:
        this.maxDispatchQuantity,

      minPendingDispatchQuantity:
        this.minPendingDispatchQuantity,
      maxPendingDispatchQuantity:
        this.maxPendingDispatchQuantity,

      minCompletionPercentage:
        this.minCompletionPercentage,
      maxCompletionPercentage:
        this.maxCompletionPercentage,

      columnProcessStatus:
        this.columnProcessStatus,
      columnInvoiceStatus:
        this.columnInvoiceStatus,
      columnDispatchStatus:
        this.columnDispatchStatus,

      columnDeliveryDateFrom:
        this.columnDeliveryDateFrom,
      columnDeliveryDateTo:
        this.columnDeliveryDateTo,

      columnAssignedDashboard:
        this.columnAssignedDashboard,

      sortColumn:
        this.sortColumn() ??
        undefined,

      sortDirection:
        this.sortDirection() ??
        undefined,

      excelFilters:
        this.serializeExcelFilters(),

      page:
        includePaging
          ? this.page
          : undefined,

      pageSize:
        includePaging
          ? this.pageSize
          : undefined,
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

  private describeError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : 'Pending materials could not be loaded.';
  }
}
