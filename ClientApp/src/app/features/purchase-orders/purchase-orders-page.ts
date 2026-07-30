import { ManageOnlyDirective } from '../../core/directives/manage-only.directive';
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

import {
  PurchaseOrderListItem,
  PurchaseOrderListSummary,
} from '../../core/models/purchase-order.model';
import { PurchaseOrdersApiService } from '../../core/services/purchase-orders-api.service';

const EMPTY_SUMMARY: PurchaseOrderListSummary = {
  totalPurchaseOrders: 0,
  openPurchaseOrders: 0,
  completedPurchaseOrders: 0,
  purchaseOrdersWithPendingMaterials: 0,
  purchaseOrdersWithOverdueMaterials: 0,
  purchaseOrdersWithoutHandler: 0,
  totalPOQuantity: 0,
  totalInvoiceBalanceQuantity: 0,
  totalPendingDispatchQuantity: 0,
};

@Component({
  selector: 'app-purchase-orders-page',
  imports: [ManageOnlyDirective, DatePipe, DecimalPipe, ReactiveFormsModule, RouterLink],
  templateUrl: './purchase-orders-page.html',
  styleUrl: './purchase-orders-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PurchaseOrdersPage {
  private readonly purchaseOrdersApi = inject(PurchaseOrdersApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly filters = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    handler: new FormControl('', { nonNullable: true }),
    status: new FormControl('', { nonNullable: true }),
    materialStatus: new FormControl('', { nonNullable: true }),
    poDateFrom: new FormControl('', { nonNullable: true }),
    poDateTo: new FormControl('', { nonNullable: true }),
    poReceivedDateFrom: new FormControl('', { nonNullable: true }),
    poReceivedDateTo: new FormControl('', { nonNullable: true }),
  });

  readonly items = signal<readonly PurchaseOrderListItem[]>([]);
  readonly summary = signal<PurchaseOrderListSummary>(EMPTY_SUMMARY);
  readonly handlers = signal<readonly string[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly totalRecords = signal(0);
  readonly totalPages = signal(1);
  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly errorStatus = signal<number | null>(null);
  readonly successMessage = signal('');
  readonly bulkDeleting = signal(false);

  readonly selectedIds =
    signal<ReadonlySet<number>>(
      new Set<number>(),
    );

  readonly hasPreviousPage =
    computed(
      () =>
        this.page() > 1,
    );

  readonly hasNextPage =
    computed(
      () =>
        this.page() <
        this.totalPages(),
    );

  readonly selectedCount =
    computed(
      () =>
        this.selectedIds().size,
    );

  readonly allCurrentPageSelected =
    computed(() => {
      const purchaseOrders =
        this.items();

      const selectedIds =
        this.selectedIds();

      return (
        purchaseOrders.length > 0 &&
        purchaseOrders.every(
          (purchaseOrder) =>
            selectedIds.has(
              purchaseOrder.purchaseOrderId,
            ),
        )
      );
    });
  readonly showingFrom = computed(() =>
    this.totalRecords() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );
  readonly showingTo = computed(() => Math.min(this.page() * this.pageSize(), this.totalRecords()));

  constructor() {
    this.loadPurchaseOrders();
  }

  applyFilters(): void {
    this.page.set(1);
    this.clearSelection();
    this.successMessage.set('');
    this.loadPurchaseOrders();
  }

  clearFilters(): void {
    this.filters.reset();
    this.page.set(1);
    this.clearSelection();
    this.successMessage.set('');
    this.loadPurchaseOrders();
  }

  changePageSize(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    if (![10, 25, 50, 100].includes(value)) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.clearSelection();
    this.successMessage.set('');
    this.loadPurchaseOrders();
  }

  previousPage(): void {
    if (!this.hasPreviousPage() || this.loading()) {
      return;
    }

    this.page.update(
      (currentPage) =>
        currentPage - 1,
    );

    this.clearSelection();
    this.successMessage.set('');
    this.loadPurchaseOrders();
  }

  nextPage(): void {
    if (!this.hasNextPage() || this.loading()) {
      return;
    }

    this.page.update(
      (currentPage) =>
        currentPage + 1,
    );

    this.clearSelection();
    this.successMessage.set('');
    this.loadPurchaseOrders();
  }

  togglePurchaseOrder(
    purchaseOrderId: number,
    event: Event,
  ): void {
    const checked =
      (
        event.target as
          HTMLInputElement
      ).checked;

    this.selectedIds.update(
      (selectedIds) => {
        const updated =
          new Set(selectedIds);

        if (checked) {
          updated.add(
            purchaseOrderId,
          );
        } else {
          updated.delete(
            purchaseOrderId,
          );
        }

        return updated;
      },
    );
  }

  toggleCurrentPage(
    event: Event,
  ): void {
    const checked =
      (
        event.target as
          HTMLInputElement
      ).checked;

    if (!checked) {
      this.clearSelection();
      return;
    }

    this.selectedIds.set(
      new Set(
        this.items().map(
          (purchaseOrder) =>
            purchaseOrder.purchaseOrderId,
        ),
      ),
    );
  }

  bulkDeletePurchaseOrders(): void {
    const purchaseOrderIds =
      Array.from(
        this.selectedIds(),
      );

    if (
      purchaseOrderIds.length === 0 ||
      this.bulkDeleting() ||
      this.loading()
    ) {
      return;
    }

    const confirmed =
      window.confirm(
        `Delete ${purchaseOrderIds.length} selected Purchase Order(s)?\n\n` +
          'Purchase Orders with linked materials will be safely skipped.',
      );

    if (!confirmed) {
      return;
    }

    this.bulkDeleting.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.purchaseOrdersApi
      .bulkDelete({
        purchaseOrderIds,
      })
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() =>
          this.bulkDeleting.set(
            false,
          ),
        ),
      )
      .subscribe({
        next: (response) => {
          const deletedIds =
            new Set(
              response
                .deletedPurchaseOrderIds,
            );

          if (
            response.deletedCount > 0
          ) {
            const currentPageDeleted =
              this.items().length > 0 &&
              this.items().every(
                (purchaseOrder) =>
                  deletedIds.has(
                    purchaseOrder
                      .purchaseOrderId,
                  ),
              );

            if (
              currentPageDeleted &&
              this.page() > 1
            ) {
              this.page.update(
                (currentPage) =>
                  currentPage - 1,
              );
            }

            this.clearSelection();

            this.successMessage.set(
              response.message ||
                'Selected Purchase Orders deleted successfully.',
            );

            this.loadPurchaseOrders();
            return;
          }

          this.errorMessage.set(
            response.message ||
              'No selected Purchase Orders were deleted.',
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeMutationError(
              error,
              'The selected Purchase Orders could not be deleted.',
            ),
          );
        },
      });
  }

  retry(): void {
    this.loadPurchaseOrders();
  }

  statusClass(status: string): string {
    const normalizedStatus = status.toLowerCase();

    if (normalizedStatus.includes('complete')) {
      return 'status status--success';
    }

    if (normalizedStatus.includes('progress') || normalizedStatus.includes('partial')) {
      return 'status status--info';
    }

    if (normalizedStatus.includes('open') || normalizedStatus.includes('pending')) {
      return 'status status--warning';
    }

    return 'status status--neutral';
  }

  exportUrl(): string {
    const values = this.filters.getRawValue();
    const params = new URLSearchParams();

    this.appendQueryValue(params, 'search', values.search);
    this.appendQueryValue(params, 'handler', values.handler);
    this.appendQueryValue(params, 'status', values.status);
    this.appendQueryValue(params, 'materialStatus', values.materialStatus);
    this.appendQueryValue(params, 'poDateFrom', values.poDateFrom);
    this.appendQueryValue(params, 'poDateTo', values.poDateTo);
    this.appendQueryValue(params, 'poReceivedDateFrom', values.poReceivedDateFrom);
    this.appendQueryValue(params, 'poReceivedDateTo', values.poReceivedDateTo);

    const queryString = params.toString();

    return queryString ? `/api/purchase-orders/export-excel?${queryString}` : '/api/purchase-orders/export-excel';
  }

  private clearSelection(): void {
    this.selectedIds.set(
      new Set<number>(),
    );
  }

  private loadPurchaseOrders(): void {
    if (this.loading()) {
      return;
    }

    const filterValues = this.filters.getRawValue();

    this.loading.set(true);
    this.errorMessage.set('');
    this.errorStatus.set(null);

    this.purchaseOrdersApi
      .getPurchaseOrders({
        page: this.page(),
        pageSize: this.pageSize(),
        ...filterValues,
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.items.set(response.items);
          this.summary.set(response.summary);
          this.handlers.set(response.handlers);
          this.page.set(response.page);
          this.pageSize.set(response.pageSize);
          this.totalRecords.set(response.totalRecords);
          this.totalPages.set(response.totalPages);
          this.clearSelection();
        },
        error: (error: HttpErrorResponse) => {
          this.items.set([]);
          this.summary.set(EMPTY_SUMMARY);
          this.totalRecords.set(0);
          this.totalPages.set(1);
          this.clearSelection();
          this.errorStatus.set(error.status);
          this.errorMessage.set(this.describeError(error));
        },
      });
  }

  private describeMutationError(
    error: HttpErrorResponse,
    fallbackMessage: string,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Your account does not have permission to delete Purchase Orders.';
    }

    const apiMessage =
      error.error?.message ??
      error.error?.error;

    return typeof apiMessage ===
      'string'
      ? apiMessage
      : fallbackMessage;
  }

  private describeError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. Start the ASP.NET application and check proxy.conf.json.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session is not signed in or has expired.';
    }

    if (error.status === 403) {
      return 'Your account does not have permission to read Purchase Orders.';
    }

    if (error.status === 404) {
      return 'The Purchase Orders API was not found. Confirm that Step 14 files were extracted and rebuild the ASP.NET project.';
    }

    const apiMessage = error.error?.error;

    return typeof apiMessage === 'string'
      ? apiMessage
      : 'Purchase Orders could not be loaded. Please try again.';
  }

  private appendQueryValue(params: URLSearchParams, name: string, value: string): void {
    const normalizedValue = value.trim();

    if (normalizedValue) {
      params.set(name, normalizedValue);
    }
  }
}

