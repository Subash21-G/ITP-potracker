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
  InvoiceImportBatch,
} from '../../../core/models/invoice-api.model';
import {
  InvoiceApiService,
} from '../../../core/services/invoice-api.service';

@Component({
  selector: 'app-invoice-import-history-page',
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
  ],
  templateUrl:
    './invoice-import-history-page.html',
  styleUrl:
    './invoice-import-history-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class InvoiceImportHistoryPage {
  private readonly invoiceApi =
    inject(InvoiceApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly history =
    signal<readonly InvoiceImportBatch[]>([]);

  readonly searchText =
    signal('');

  readonly loading =
    signal(false);

  readonly deletingId =
    signal<number | null>(null);

  readonly bulkDeleting =
    signal(false);

  readonly selectedBatchIds =
    signal<ReadonlySet<number>>(
      new Set<number>(),
    );

  readonly errorMessage =
    signal('');

  readonly successMessage =
    signal('');

  readonly totalBatches =
    computed(() =>
      this.history().length,
    );

  readonly totalRows =
    computed(() =>
      this.history().reduce(
        (total, batch) =>
          total + batch.totalRows,
        0,
      ),
    );

  readonly successRows =
    computed(() =>
      this.history().reduce(
        (total, batch) =>
          total + batch.successRows,
        0,
      ),
    );

  readonly pendingRows =
    computed(() =>
      this.history().reduce(
        (total, batch) =>
          total + batch.pendingRows,
        0,
      ),
    );

  readonly deletedRows =
    computed(() =>
      this.history().reduce(
        (total, batch) =>
          total + batch.deletedRows,
        0,
      ),
    );

  readonly successPercent =
    computed(() =>
      this.calculatePercent(
        this.successRows(),
        this.totalRows(),
      ),
    );

  readonly pendingPercent =
    computed(() =>
      this.calculatePercent(
        this.pendingRows(),
        this.totalRows(),
      ),
    );

  readonly selectedCount =
    computed(() =>
      this.selectedBatchIds().size,
    );

  readonly allVisibleSelected =
    computed(() => {
      const batches =
        this.history();

      const selectedIds =
        this.selectedBatchIds();

      return (
        batches.length > 0 &&
        batches.every((batch) =>
          selectedIds.has(batch.id),
        )
      );
    });

  constructor() {
    this.loadHistory();
  }

  applySearch(): void {
    this.clearSelection();
    this.loadHistory();
  }

  clearSearch(): void {
    this.searchText.set('');
    this.clearSelection();
    this.loadHistory();
  }

  refresh(): void {
    this.clearSelection();
    this.loadHistory();
  }

  toggleBatchSelection(
    batchId: number,
    checked: boolean,
  ): void {
    const selectedIds =
      new Set(
        this.selectedBatchIds(),
      );

    if (checked) {
      if (
        !selectedIds.has(batchId) &&
        selectedIds.size >= 200
      ) {
        this.errorMessage.set(
          'You can select a maximum of 200 import-history records.',
        );

        return;
      }

      selectedIds.add(batchId);
    } else {
      selectedIds.delete(batchId);
    }

    this.errorMessage.set('');
    this.selectedBatchIds.set(
      selectedIds,
    );
  }

  toggleVisibleSelection(
    checked: boolean,
  ): void {
    if (!checked) {
      this.clearSelection();

      return;
    }

    const visibleIds =
      this.history()
        .slice(0, 200)
        .map((batch) => batch.id);

    this.selectedBatchIds.set(
      new Set(visibleIds),
    );

    if (
      this.history().length > 200
    ) {
      this.errorMessage.set(
        'Only the first 200 import-history records were selected.',
      );
    } else {
      this.errorMessage.set('');
    }
  }

  clearSelection(): void {
    this.selectedBatchIds.set(
      new Set<number>(),
    );
  }

  isSelected(
    batchId: number,
  ): boolean {
    return this.selectedBatchIds()
      .has(batchId);
  }

  deleteHistory(
    batch: InvoiceImportBatch,
  ): void {
    if (
      this.deletingId() !== null ||
      this.bulkDeleting()
    ) {
      return;
    }

    const confirmed =
      window.confirm(
        `Delete import history for "${batch.fileName}"?\n\n` +
        'Only history records will be deleted. ' +
        'Actual invoices and material quantities will not change.',
      );

    if (!confirmed) {
      return;
    }

    this.deletingId.set(batch.id);
    this.clearMessages();

    this.invoiceApi
      .deleteImportBatch(batch.id)
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() =>
          this.deletingId.set(null),
        ),
      )
      .subscribe({
        next: (response) => {
          const selectedIds =
            new Set(
              this.selectedBatchIds(),
            );

          selectedIds.delete(batch.id);

          this.selectedBatchIds.set(
            selectedIds,
          );

          this.successMessage.set(
            response.message,
          );

          this.loadHistory(
            false,
          );
        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeDeleteError(
              error,
            ),
          );
        },
      });
  }

  bulkDeleteHistory(): void {
    const batchIds =
      Array.from(
        this.selectedBatchIds(),
      );

    if (
      batchIds.length === 0 ||
      batchIds.length > 200 ||
      this.bulkDeleting() ||
      this.deletingId() !== null
    ) {
      if (
        batchIds.length === 0
      ) {
        this.errorMessage.set(
          'Select at least one import-history record.',
        );
      } else if (
        batchIds.length > 200
      ) {
        this.errorMessage.set(
          'You can delete a maximum of 200 import-history records at one time.',
        );
      }

      return;
    }

    const confirmed =
      window.confirm(
        `Delete ${batchIds.length} selected import-history record(s)?\n\n` +
        'Actual invoices and material quantities will not change.',
      );

    if (!confirmed) {
      return;
    }

    this.bulkDeleting.set(true);
    this.clearMessages();

    this.invoiceApi
      .bulkDeleteImportHistory({
        batchIds,
      })
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() =>
          this.bulkDeleting.set(false),
        ),
      )
      .subscribe({
        next: (response) => {
          this.clearSelection();

          this.successMessage.set(
            response.message,
          );

          this.loadHistory(
            false,
          );
        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeDeleteError(
              error,
            ),
          );
        },
      });
  }

  batchSuccessPercent(
    batch: InvoiceImportBatch,
  ): number {
    return this.calculatePercent(
      batch.successRows,
      batch.totalRows,
    );
  }

  batchStatus(
    batch: InvoiceImportBatch,
  ): string {
    if (batch.totalRows <= 0) {
      return 'Empty';
    }

    if (
      batch.deletedRows ===
      batch.totalRows
    ) {
      return 'Deleted';
    }

    if (
      batch.successRows ===
      batch.totalRows
    ) {
      return 'Completed';
    }

    if (
      batch.pendingRows ===
      batch.totalRows
    ) {
      return 'Pending';
    }

    return 'Partial';
  }

  statusClass(
    status: string,
  ): string {
    switch (
      status.toLowerCase()
    ) {
      case 'completed':
        return 'status status--success';

      case 'pending':
      case 'partial':
        return 'status status--warning';

      default:
        return 'status status--neutral';
    }
  }

  private loadHistory(
    clearMessages = true,
  ): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);

    if (clearMessages) {
      this.clearMessages();
    }

    this.invoiceApi
      .getImportHistory(
        this.searchText(),
        500,
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
        next: (history) => {
          this.history.set(
            history,
          );
        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.history.set([]);

          this.errorMessage.set(
            this.describeLoadError(
              error,
            ),
          );
        },
      });
  }

  private calculatePercent(
    value: number,
    total: number,
  ): number {
    if (total <= 0) {
      return 0;
    }

    return Math.round(
      (value / total) * 100,
    );
  }

  private clearMessages(): void {
    this.errorMessage.set('');
    this.successMessage.set('');
  }

  private describeLoadError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 401) {
      return 'Your session has expired. Sign in again to view invoice import history.';
    }

    if (error.status === 403) {
      return 'Your account does not have permission to view invoice import history.';
    }

    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. Invoice import history could not be loaded.';
    }

    return 'Invoice import history could not be loaded.';
  }

  private describeDeleteError(
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
      return 'Your session has expired. Sign in again before deleting import history.';
    }

    if (error.status === 403) {
      return 'Your account does not have permission to delete import history.';
    }

    if (error.status === 404) {
      return 'The selected import-history record was not found.';
    }

    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. Import history could not be deleted.';
    }

    return 'Import history could not be deleted.';
  }
}