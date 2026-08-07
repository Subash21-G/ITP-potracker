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
import {
  ActivatedRoute,
  RouterLink,
} from '@angular/router';
import { finalize } from 'rxjs';

import {
  PurchaseOrderListItem,
  PurchaseOrderListSummary,
  PurchaseOrderSortColumn,
  PurchaseOrderSortDirection,
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
  totalMaterialValue: 0,
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
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly filters = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    handler: new FormControl('', { nonNullable: true }),
    status: new FormControl('', { nonNullable: true }),
    materialStatus: new FormControl('', { nonNullable: true }),
    minMaterials: new FormControl('', { nonNullable: true }),
    maxMaterials: new FormControl('', { nonNullable: true }),
    minPoQuantity: new FormControl('', { nonNullable: true }),
    maxPoQuantity: new FormControl('', { nonNullable: true }),
    minPoValue: new FormControl('', { nonNullable: true }),
    maxPoValue: new FormControl('', { nonNullable: true }),
    minDispatchBalance: new FormControl('', { nonNullable: true }),
    maxDispatchBalance: new FormControl('', { nonNullable: true }),
    deliveryDateFrom: new FormControl('', { nonNullable: true }),
    deliveryDateTo: new FormControl('', { nonNullable: true }),
    pdfAvailability: new FormControl('', { nonNullable: true }),
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

  readonly sortColumn =
    signal<PurchaseOrderSortColumn | null>(
      null,
    );

  readonly sortDirection =
    signal<PurchaseOrderSortDirection | null>(
      null,
    );

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

  readonly visiblePages =
    computed(() => {
      const current =
        this.page();

      const total =
        this.totalPages();

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
    const linkedPersonName =
      this.route.snapshot.queryParamMap
        .get('handler')
        ?.trim() ?? '';

    if (linkedPersonName) {
      this.filters.controls.handler
        .setValue(linkedPersonName);
    }

    this.loadPurchaseOrders();
  }

  toggleSort(
    column: PurchaseOrderSortColumn,
  ): void {
    if (this.loading()) {
      return;
    }

    if (
      this.sortColumn() !== column ||
      this.sortDirection() === null
    ) {
      this.sortColumn.set(column);
      this.sortDirection.set('asc');
    } else if (
      this.sortDirection() === 'asc'
    ) {
      this.sortDirection.set('desc');
    } else {
      this.sortColumn.set(null);
      this.sortDirection.set(null);
    }

    this.page.set(1);
    this.clearSelection();
    this.successMessage.set('');
    this.loadPurchaseOrders();
  }

  sortIndicator(
    column: PurchaseOrderSortColumn,
  ): string {
    if (this.sortColumn() !== column) {
      return '\u2195';
    }

    return this.sortDirection() === 'desc'
      ? '\u2193'
      : '\u2191';
  }

  sortTitle(
    column: PurchaseOrderSortColumn,
    label: string,
  ): string {
    if (this.sortColumn() !== column) {
      return `Sort ${label} ascending`;
    }

    if (this.sortDirection() === 'asc') {
      return `Sort ${label} descending`;
    }

    return `Clear ${label} sort`;
  }

  applyColumnFilter(
    filter: HTMLDetailsElement,
  ): void {
    filter.open = false;
    this.applyFilters();
  }

  clearColumnFilter(
    column:
      | 'po'
      | 'poDate'
      | 'received'
      | 'status'
      | 'material'
      | 'materials'
      | 'quantity'
      | 'value'
      | 'dispatch'
      | 'delivery'
      | 'pdf',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'po':
        this.filters.patchValue({
          search: '',
          handler: '',
        });
        break;

      case 'poDate':
        this.filters.patchValue({
          poDateFrom: '',
          poDateTo: '',
        });
        break;

      case 'received':
        this.filters.patchValue({
          poReceivedDateFrom: '',
          poReceivedDateTo: '',
        });
        break;

      case 'status':
        this.filters.controls.status.setValue('');
        break;

      case 'material':
        this.filters.controls.materialStatus.setValue('');
        break;

      case 'materials':
        this.filters.patchValue({
          minMaterials: '',
          maxMaterials: '',
        });
        break;

      case 'quantity':
        this.filters.patchValue({
          minPoQuantity: '',
          maxPoQuantity: '',
        });
        break;

      case 'value':
        this.filters.patchValue({
          minPoValue: '',
          maxPoValue: '',
        });
        break;

      case 'dispatch':
        this.filters.patchValue({
          minDispatchBalance: '',
          maxDispatchBalance: '',
        });
        break;

      case 'delivery':
        this.filters.patchValue({
          deliveryDateFrom: '',
          deliveryDateTo: '',
        });
        break;

      case 'pdf':
        this.filters.controls.pdfAvailability.setValue('');
        break;
    }

    filter.open = false;
    this.applyFilters();
  }

  columnFilterActive(
    column:
      | 'po'
      | 'poDate'
      | 'received'
      | 'status'
      | 'material'
      | 'materials'
      | 'quantity'
      | 'value'
      | 'dispatch'
      | 'delivery'
      | 'pdf',
  ): boolean {
    const values =
      this.filters.getRawValue();

    switch (column) {
      case 'po':
        return Boolean(
          values.search.trim() ||
          values.handler.trim(),
        );

      case 'poDate':
        return Boolean(
          values.poDateFrom ||
          values.poDateTo,
        );

      case 'received':
        return Boolean(
          values.poReceivedDateFrom ||
          values.poReceivedDateTo,
        );

      case 'status':
        return Boolean(values.status);

      case 'material':
        return Boolean(values.materialStatus);

      case 'materials':
        return Boolean(
          values.minMaterials ||
          values.maxMaterials,
        );

      case 'quantity':
        return Boolean(
          values.minPoQuantity ||
          values.maxPoQuantity,
        );

      case 'value':
        return Boolean(
          values.minPoValue ||
          values.maxPoValue,
        );

      case 'dispatch':
        return Boolean(
          values.minDispatchBalance ||
          values.maxDispatchBalance,
        );

      case 'delivery':
        return Boolean(
          values.deliveryDateFrom ||
          values.deliveryDateTo,
        );

      case 'pdf':
        return Boolean(values.pdfAvailability);
    }
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

    if (![25, 50, 100].includes(value)) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.clearSelection();
    this.successMessage.set('');
    this.loadPurchaseOrders();
  }

  goToPage(
    page: number,
  ): void {
    if (this.loading()) {
      return;
    }

    const normalizedPage =
      Math.min(
        Math.max(
          page,
          1,
        ),
        this.totalPages(),
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

    this.clearSelection();
    this.successMessage.set('');
    this.loadPurchaseOrders();
  }

  previousPage(): void {
    if (!this.hasPreviousPage()) {
      return;
    }

    this.goToPage(
      this.page() - 1,
    );
  }

  nextPage(): void {
    if (!this.hasNextPage()) {
      return;
    }

    this.goToPage(
      this.page() + 1,
    );
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

    const sortColumn =
      this.sortColumn();

    const sortDirection =
      this.sortDirection();

    if (
      sortColumn &&
      sortDirection
    ) {
      params.set(
        'sortColumn',
        sortColumn,
      );

      params.set(
        'sortDirection',
        sortDirection,
      );
    }

    this.appendQueryValue(params, 'search', values.search);
    this.appendQueryValue(params, 'handler', values.handler);
    this.appendQueryValue(params, 'status', values.status);
    this.appendQueryValue(params, 'materialStatus', values.materialStatus);
    this.appendQueryValue(params, 'minMaterials', values.minMaterials);
    this.appendQueryValue(params, 'maxMaterials', values.maxMaterials);
    this.appendQueryValue(params, 'minPoQuantity', values.minPoQuantity);
    this.appendQueryValue(params, 'maxPoQuantity', values.maxPoQuantity);
    this.appendQueryValue(params, 'minPoValue', values.minPoValue);
    this.appendQueryValue(params, 'maxPoValue', values.maxPoValue);
    this.appendQueryValue(params, 'minDispatchBalance', values.minDispatchBalance);
    this.appendQueryValue(params, 'maxDispatchBalance', values.maxDispatchBalance);
    this.appendQueryValue(params, 'deliveryDateFrom', values.deliveryDateFrom);
    this.appendQueryValue(params, 'deliveryDateTo', values.deliveryDateTo);
    this.appendQueryValue(params, 'pdfAvailability', values.pdfAvailability);
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

        sortColumn:
          this.sortColumn() ??
          undefined,

        sortDirection:
          this.sortDirection() ??
          undefined,

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
