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

  readonly loading =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly filteredItems =
    computed(() => {
      const items =
        this.report()?.items ?? [];

      const search =
        this.searchText()
          .trim()
          .toLowerCase();

      const status =
        this.statusFilter()
          .trim()
          .toLowerCase();

      return items.filter((row) => {
        const searchValue = [
          row.poNumber,
          row.lineItem,
          row.materialNumber,
          row.invoiceNumber,
          row.status,
          row.message,
        ]
          .join(' ')
          .toLowerCase();

        const searchMatches =
          !search ||
          searchValue.includes(search);

        const statusMatches =
          !status ||
          row.status.toLowerCase() ===
            status;

        return (
          searchMatches &&
          statusMatches
        );
      });
    });

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

  clearFilters(): void {
    this.searchText.set('');
    this.statusFilter.set('');
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
        page,
        this.pageSize(),
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

          this.searchText.set('');
          this.statusFilter.set('');
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