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
  PendingMaterialQuery,
  PendingMaterialsResponse,
} from '../../core/models/pending-material-api.model';
import {
  PendingMaterialApiService,
} from '../../core/services/pending-material-api.service';

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

  applyFilters(): void {
    this.load(1);
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

  private buildQuery():
    PendingMaterialQuery {
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

      page: this.page,
      pageSize: this.pageSize,
    };
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
