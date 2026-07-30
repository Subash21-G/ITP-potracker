import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { ManageOnlyDirective } from '../../core/directives/manage-only.directive';
import {
  InvoiceListItem,
  InvoiceListSummary,
} from '../../core/models/invoice-api.model';
import { InvoiceApiService } from '../../core/services/invoice-api.service';

const EMPTY_SUMMARY: InvoiceListSummary = {
  totalInvoices: 0,
  totalItems: 0,
  totalQuantity: 0,
  totalGstAmount: 0,
  totalInvoiceAmount: 0,
  pendingItemCount: 0,
};

@Component({
  selector: 'app-invoices-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ManageOnlyDirective,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './invoices-page.html',
  styleUrl: './invoices-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoicesPage {
  private readonly invoiceApi = inject(InvoiceApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly filters = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    dateFrom: new FormControl('', { nonNullable: true }),
    dateTo: new FormControl('', { nonNullable: true }),
  });

  readonly items = signal<readonly InvoiceListItem[]>([]);
  readonly summary = signal<InvoiceListSummary>(EMPTY_SUMMARY);
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);
  readonly loading = signal(false);
  readonly deletingId = signal<number | null>(null);
  readonly bulkDeleting = signal(false);
  readonly downloadingExcel = signal(false);
  readonly selectedInvoiceIds =
    signal<ReadonlySet<number>>(
      new Set<number>(),
    );
  readonly errorMessage = signal('');
  readonly successMessage = signal('');

  readonly hasPreviousPage = computed(() => this.page() > 1);
  readonly hasNextPage = computed(() => this.page() < this.totalPages());

  readonly selectedCount = computed(
    () => this.selectedInvoiceIds().size,
  );

  readonly allVisibleSelected = computed(() => {
    const invoices = this.items();
    const selectedIds = this.selectedInvoiceIds();

    return (
      invoices.length > 0 &&
      invoices.every((invoice) =>
        selectedIds.has(invoice.id),
      )
    );
  });
  readonly showingFrom = computed(() =>
    this.totalCount() === 0
      ? 0
      : (this.page() - 1) * this.pageSize() + 1,
  );
  readonly showingTo = computed(() =>
    Math.min(this.page() * this.pageSize(), this.totalCount()),
  );

  constructor() {
    this.loadInvoices();
  }

  applyFilters(): void {
    this.page.set(1);
    this.loadInvoices();
  }

  clearFilters(): void {
    this.filters.reset();
    this.page.set(1);
    this.loadInvoices();
  }

  changePageSize(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    if (![10, 25, 50, 100].includes(value)) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.loadInvoices();
  }

  previousPage(): void {
    if (!this.hasPreviousPage() || this.loading()) {
      return;
    }

    this.page.update((currentPage) => currentPage - 1);
    this.loadInvoices();
  }

  nextPage(): void {
    if (!this.hasNextPage() || this.loading()) {
      return;
    }

    this.page.update((currentPage) => currentPage + 1);
    this.loadInvoices();
  }

  retry(): void {
    this.loadInvoices();
  }

  isInvoiceSelected(invoiceId: number): boolean {
    return this.selectedInvoiceIds().has(
      invoiceId,
    );
  }

  toggleInvoiceSelection(
    invoiceId: number,
    selected: boolean,
  ): void {
    const updatedIds =
      new Set(this.selectedInvoiceIds());

    if (selected) {
      updatedIds.add(invoiceId);
    } else {
      updatedIds.delete(invoiceId);
    }

    this.selectedInvoiceIds.set(
      updatedIds,
    );
  }

  toggleVisibleSelection(
    selected: boolean,
  ): void {
    const updatedIds =
      new Set(this.selectedInvoiceIds());

    for (const invoice of this.items()) {
      if (selected) {
        updatedIds.add(invoice.id);
      } else {
        updatedIds.delete(invoice.id);
      }
    }

    this.selectedInvoiceIds.set(
      updatedIds,
    );
  }

  clearSelection(): void {
    this.selectedInvoiceIds.set(
      new Set<number>(),
    );
  }

  downloadInvoicesExcel(): void {
    if (this.downloadingExcel()) {
      return;
    }

    this.downloadingExcel.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.invoiceApi
      .downloadInvoicesExcel()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() =>
          this.downloadingExcel.set(false),
        ),
      )
      .subscribe({
        next: (file) => {
          if (!file || file.size === 0) {
            this.errorMessage.set(
              'The invoice Excel export was empty.',
            );

            return;
          }

          const downloadUrl =
            URL.createObjectURL(file);

          const downloadLink =
            document.createElement('a');

          const timestamp =
            new Date()
              .toISOString()
              .slice(0, 19)
              .replace('T', '_')
              .replaceAll(':', '')
              .replaceAll('-', '');

          downloadLink.href =
            downloadUrl;

          downloadLink.download =
            `Merged_All_Invoices_${timestamp}.xlsx`;

          document.body.appendChild(
            downloadLink,
          );

          downloadLink.click();
          downloadLink.remove();

          setTimeout(
            () =>
              URL.revokeObjectURL(
                downloadUrl,
              ),
            0,
          );

          this.successMessage.set(
            'Invoice Excel export downloaded successfully.',
          );
        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeExportError(
              error,
            ),
          );
        },
      });
  }

  bulkDeleteInvoices(): void {
    const invoiceIds =
      Array.from(
        this.selectedInvoiceIds(),
      );

    if (
      invoiceIds.length === 0 ||
      this.bulkDeleting() ||
      this.deletingId() !== null
    ) {
      return;
    }

    const confirmed = window.confirm(
      `Delete ${invoiceIds.length} selected invoice(s)?\n\n` +
        'Linked material invoice and dispatch quantities will be restored.',
    );

    if (!confirmed) {
      return;
    }

    this.bulkDeleting.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.invoiceApi
      .bulkDeleteInvoices({
        invoiceIds,
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() =>
          this.bulkDeleting.set(false),
        ),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(
            response.message ||
              'Selected invoices deleted successfully.',
          );

          const deletedIds =
            new Set(
              response.deletedInvoiceIds,
            );

          const remainingSelectedIds =
            new Set(
              Array.from(
                this.selectedInvoiceIds(),
              ).filter(
                (invoiceId) =>
                  !deletedIds.has(invoiceId),
              ),
            );

          this.selectedInvoiceIds.set(
            remainingSelectedIds,
          );

          const currentPageWasDeleted =
            this.items().length > 0 &&
            this.items().every((invoice) =>
              deletedIds.has(invoice.id),
            );

          if (
            currentPageWasDeleted &&
            this.page() > 1
          ) {
            this.page.update(
              (currentPage) =>
                currentPage - 1,
            );
          }

          this.loadInvoices(false);
        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeBulkDeleteError(
              error,
            ),
          );
        },
      });
  }

  deleteInvoice(invoice: InvoiceListItem): void {
    if (this.deletingId() !== null || this.bulkDeleting()) {
      return;
    }

    const confirmed = window.confirm(
      `Delete invoice ${invoice.invoiceNumber}?\n\n` +
        'The linked material invoice and dispatch quantities will be restored.',
    );

    if (!confirmed) {
      return;
    }

    this.deletingId.set(invoice.id);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.invoiceApi
      .deleteInvoice(invoice.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.deletingId.set(null)),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(response.message || 'Invoice deleted successfully.');

          if (this.items().length === 1 && this.page() > 1) {
            this.page.update((currentPage) => currentPage - 1);
          }

          this.loadInvoices(false);
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(this.describeDeleteError(error));
        },
      });
  }

  private loadInvoices(clearMessages = true): void {
    if (this.loading()) {
      return;
    }

    const filterValues = this.filters.getRawValue();

    this.loading.set(true);

    if (clearMessages) {
      this.errorMessage.set('');
      this.successMessage.set('');
    }

    this.invoiceApi
      .getInvoices({
        search: filterValues.search,
        dateFrom: filterValues.dateFrom || null,
        dateTo: filterValues.dateTo || null,
        page: this.page(),
        pageSize: this.pageSize(),
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.items.set(response.items);
          this.summary.set(response.summary);
          this.page.set(response.page);
          this.pageSize.set(response.pageSize);
          this.totalCount.set(response.totalCount);
          this.totalPages.set(Math.max(1, response.totalPages));
        },
        error: (error: HttpErrorResponse) => {
          this.items.set([]);
          this.summary.set(EMPTY_SUMMARY);
          this.totalCount.set(0);
          this.totalPages.set(1);
          this.errorMessage.set(this.describeLoadError(error));
        },
      });
  }

  private describeLoadError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. Start the ASP.NET application and verify the Angular proxy configuration.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Your account does not have permission to read invoices.';
    }

    if (error.status === 404) {
      return 'The Invoice API was not found. Confirm that InvoicesApiController.cs is in Controllers/Api and rebuild the ASP.NET project.';
    }

    return this.readApiMessage(error) || 'Invoices could not be loaded. Please try again.';
  }

  private describeDeleteError(error: HttpErrorResponse): string {
    if (error.status === 401) {
      return 'Your session has expired. Sign in again before deleting the invoice.';
    }

    if (error.status === 403) {
      return 'Only Admin and Production users can delete invoices.';
    }

    if (error.status === 404) {
      return 'The selected invoice no longer exists.';
    }

    return this.readApiMessage(error) || 'The invoice could not be deleted.';
  }

  private describeExportError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 401) {
      return 'Your session has expired. Sign in again before exporting invoices.';
    }

    if (error.status === 403) {
      return 'Your account does not have permission to export invoices.';
    }

    if (error.status === 404) {
      return 'No invoices are available to export.';
    }

    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. The invoice Excel export could not be downloaded.';
    }

    return 'The invoice Excel export could not be downloaded.';
  }

  private describeBulkDeleteError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 401) {
      return 'Your session has expired. Sign in again before deleting invoices.';
    }

    if (error.status === 403) {
      return 'Only Admin and Production users can delete invoices.';
    }

    if (error.status === 404) {
      return 'None of the selected invoices could be found.';
    }

    return (
      this.readApiMessage(error) ||
      'The selected invoices could not be deleted.'
    );
  }

  private readApiMessage(error: HttpErrorResponse): string {
    const message = error.error?.message ?? error.error?.error;

    return typeof message === 'string' ? message : '';
  }
}
