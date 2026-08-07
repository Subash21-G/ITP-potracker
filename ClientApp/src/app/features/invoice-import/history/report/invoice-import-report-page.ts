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
  ActivatedRoute,
  RouterLink,
} from '@angular/router';
import {
  finalize,
} from 'rxjs';

import {
  InvoiceImportReportQuery,
  InvoiceImportReportResponse,
  InvoiceImportReportRow,
} from '../../../../core/models/invoice-import-history-api.model';
import {
  InvoiceApiService,
} from '../../../../core/services/invoice-api.service';

@Component({
  selector: 'app-invoice-import-report-page',
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterLink,
  ],
  templateUrl:
    './invoice-import-report-page.html',
  styleUrl:
    './invoice-import-report-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class InvoiceImportReportPage {
  private readonly invoiceApi =
    inject(InvoiceApiService);

  private readonly route =
    inject(ActivatedRoute);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly batchId =
    signal(
      Number(
        this.route.snapshot.paramMap.get(
          'batchId',
        ),
      ),
    );

  readonly report =
    signal<InvoiceImportReportResponse | null>(
      null,
    );

  readonly page =
    signal(1);

  readonly pageSize =
    signal(50);

  readonly searchText =
    signal('');

  readonly statusFilter =
    signal('');

  readonly poNumberColumnFilter =
    signal('');

  readonly lineItemColumnFilter =
    signal('');

  readonly materialNumberColumnFilter =
    signal('');

  readonly invoiceNumberColumnFilter =
    signal('');

  readonly minInvoiceQuantityColumnFilter =
    signal('');

  readonly maxInvoiceQuantityColumnFilter =
    signal('');

  readonly statusColumnFilter =
    signal('');

  readonly messageColumnFilter =
    signal('');

  readonly loading =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly filteredItems =
    computed(() =>
      this.report()?.items ?? [],
    );

  readonly filteredPageQuantity =
    computed(() =>
      this.filteredItems().reduce(
        (total, row) =>
          total + row.invoiceQuantity,
        0,
      ),
    );

  readonly hasPreviousPage =
    computed(() =>
      (this.report()?.currentPage ?? 1) >
      1,
    );

  readonly hasNextPage =
    computed(() => {
      const report =
        this.report();

      return (
        report !== null &&
        report.currentPage <
          report.totalPages
      );
    });

  readonly showingFrom =
    computed(() => {
      const report =
        this.report();

      if (
        report === null ||
        report.summary.totalRows === 0
      ) {
        return 0;
      }

      return (
        (report.currentPage - 1) *
          report.pageSize +
        1
      );
    });

  readonly showingTo =
    computed(() => {
      const report =
        this.report();

      if (report === null) {
        return 0;
      }

      return Math.min(
        report.currentPage *
          report.pageSize,
        report.summary.totalRows,
      );
    });

  constructor() {
    if (
      !Number.isInteger(this.batchId()) ||
      this.batchId() <= 0
    ) {
      this.errorMessage.set(
        'The invoice import-history batch ID is invalid.',
      );

      return;
    }

    this.loadReport(1);
  }

  applyFilters(): void {
    this.loadReport(1);
  }

  applyColumnFilter(
    filter: HTMLDetailsElement,
  ): void {
    filter.open = false;
    this.loadReport(1);
  }

  clearColumnFilter(
    column:
      | 'po'
      | 'line'
      | 'material'
      | 'invoice'
      | 'quantity'
      | 'status'
      | 'message',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'po':
        this.poNumberColumnFilter
          .set('');
        break;

      case 'line':
        this.lineItemColumnFilter
          .set('');
        break;

      case 'material':
        this.materialNumberColumnFilter
          .set('');
        break;

      case 'invoice':
        this.invoiceNumberColumnFilter
          .set('');
        break;

      case 'quantity':
        this.minInvoiceQuantityColumnFilter
          .set('');

        this.maxInvoiceQuantityColumnFilter
          .set('');
        break;

      case 'status':
        this.statusColumnFilter
          .set('');
        break;

      case 'message':
        this.messageColumnFilter
          .set('');
        break;
    }

    filter.open = false;
    this.loadReport(1);
  }

  columnFilterActive(
    column:
      | 'po'
      | 'line'
      | 'material'
      | 'invoice'
      | 'quantity'
      | 'status'
      | 'message',
  ): boolean {
    switch (column) {
      case 'po':
        return Boolean(
          this.poNumberColumnFilter()
            .trim(),
        );

      case 'line':
        return Boolean(
          this.lineItemColumnFilter()
            .trim(),
        );

      case 'material':
        return Boolean(
          this.materialNumberColumnFilter()
            .trim(),
        );

      case 'invoice':
        return Boolean(
          this.invoiceNumberColumnFilter()
            .trim(),
        );

      case 'quantity':
        return Boolean(
          this.minInvoiceQuantityColumnFilter()
            .trim() ||
          this.maxInvoiceQuantityColumnFilter()
            .trim(),
        );

      case 'status':
        return Boolean(
          this.statusColumnFilter(),
        );

      case 'message':
        return Boolean(
          this.messageColumnFilter()
            .trim(),
        );
    }

    return false;
  }

  clearFilters(): void {
    this.searchText.set('');
    this.statusFilter.set('');

    this.poNumberColumnFilter.set('');
    this.lineItemColumnFilter.set('');
    this.materialNumberColumnFilter.set('');
    this.invoiceNumberColumnFilter.set('');
    this.minInvoiceQuantityColumnFilter.set('');
    this.maxInvoiceQuantityColumnFilter.set('');
    this.statusColumnFilter.set('');
    this.messageColumnFilter.set('');

    this.loadReport(1);
  }

  changePageSize(
    event: Event,
  ): void {
    const pageSize =
      Number(
        (
          event.target as
            HTMLSelectElement
        ).value,
      );

    if (
      ![25, 50, 100, 200].includes(
        pageSize,
      )
    ) {
      return;
    }

    this.pageSize.set(pageSize);
    this.loadReport(1);
  }

  previousPage(): void {
    const currentPage =
      this.report()?.currentPage ?? 1;

    if (currentPage <= 1) {
      return;
    }

    this.loadReport(
      currentPage - 1,
    );
  }

  nextPage(): void {
    const report =
      this.report();

    if (
      report === null ||
      report.currentPage >=
        report.totalPages
    ) {
      return;
    }

    this.loadReport(
      report.currentPage + 1,
    );
  }

  rowNumber(
    index: number,
  ): number {
    const report =
      this.report();

    if (report === null) {
      return index + 1;
    }

    return (
      (report.currentPage - 1) *
        report.pageSize +
      index +
      1
    );
  }

  statusClass(
    status: string,
  ): string {
    switch (
      status.toLowerCase()
    ) {
      case 'success':
        return 'status status--success';

      case 'deleted':
        return 'status status--neutral';

      default:
        return 'status status--warning';
    }
  }

  messageClass(
    status: string,
  ): string {
    switch (
      status.toLowerCase()
    ) {
      case 'success':
        return 'message message--success';

      case 'deleted':
        return 'message message--neutral';

      default:
        return 'message message--warning';
    }
  }

  referenceValue(
    row: InvoiceImportReportRow,
  ): string {
    return (
      row.materialNumber.trim() ||
      row.poNumber.trim()
    );
  }

  referenceLabel(
    row: InvoiceImportReportRow,
  ): string {
    if (row.materialNumber.trim()) {
      return 'Check Material';
    }

    if (row.poNumber.trim()) {
      return 'Check PO';
    }

    return 'No Reference';
  }

  private buildReportQuery(
    page: number,
  ): InvoiceImportReportQuery {
    return {
      search:
        this.searchText(),
      status:
        this.statusFilter(),

      poNumber:
        this.poNumberColumnFilter(),
      lineItem:
        this.lineItemColumnFilter(),
      materialNumber:
        this.materialNumberColumnFilter(),
      invoiceNumber:
        this.invoiceNumberColumnFilter(),

      minInvoiceQuantity:
        this.minInvoiceQuantityColumnFilter(),
      maxInvoiceQuantity:
        this.maxInvoiceQuantityColumnFilter(),

      columnStatus:
        this.statusColumnFilter(),
      message:
        this.messageColumnFilter(),

      page,
      pageSize:
        this.pageSize(),
    };
  }

  private loadReport(
    page: number,
  ): void {
    if (
      this.loading() ||
      this.batchId() <= 0
    ) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.invoiceApi
      .getImportHistoryReport(
        this.batchId(),
        this.buildReportQuery(
          page,
        ),
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() =>
          this.loading.set(false),
        ),
      )
      .subscribe({
        next: (report) => {
          this.report.set(report);
          this.page.set(
            report.currentPage,
          );


        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.report.set(null);

          this.errorMessage.set(
            this.describeLoadError(
              error,
            ),
          );
        },
      });
  }

  private describeLoadError(
    error: HttpErrorResponse,
  ): string {
    const apiMessage =
      error.error?.message;

    if (
      typeof apiMessage ===
        'string' &&
      apiMessage.trim()
    ) {
      return apiMessage.trim();
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again to view this import report.';
    }

    if (error.status === 403) {
      return 'Your account does not have permission to view this import report.';
    }

    if (error.status === 404) {
      return 'The requested invoice import-history report was not found.';
    }

    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. The import report could not be loaded.';
    }

    return 'The invoice import report could not be loaded.';
  }
}