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
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule, RouterLink],
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

  readonly hasPreviousPage = computed(() => this.page() > 1);
  readonly hasNextPage = computed(() => this.page() < this.totalPages());
  readonly showingFrom = computed(() =>
    this.totalRecords() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );
  readonly showingTo = computed(() => Math.min(this.page() * this.pageSize(), this.totalRecords()));

  constructor() {
    this.loadPurchaseOrders();
  }

  applyFilters(): void {
    this.page.set(1);
    this.loadPurchaseOrders();
  }

  clearFilters(): void {
    this.filters.reset();
    this.page.set(1);
    this.loadPurchaseOrders();
  }

  changePageSize(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    if (![10, 25, 50, 100].includes(value)) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.loadPurchaseOrders();
  }

  previousPage(): void {
    if (!this.hasPreviousPage() || this.loading()) {
      return;
    }

    this.page.update((currentPage) => currentPage - 1);
    this.loadPurchaseOrders();
  }

  nextPage(): void {
    if (!this.hasNextPage() || this.loading()) {
      return;
    }

    this.page.update((currentPage) => currentPage + 1);
    this.loadPurchaseOrders();
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

    return queryString ? `/PurchaseOrder/ExportExcel?${queryString}` : '/PurchaseOrder/ExportExcel';
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
        },
        error: (error: HttpErrorResponse) => {
          this.items.set([]);
          this.summary.set(EMPTY_SUMMARY);
          this.totalRecords.set(0);
          this.totalPages.set(1);
          this.errorStatus.set(error.status);
          this.errorMessage.set(this.describeError(error));
        },
      });
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
