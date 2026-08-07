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
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
} from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { PurchaseOrderDetails } from '../../core/models/purchase-order-details.model';
import { PurchaseOrdersApiService } from '../../core/services/purchase-orders-api.service';

@Component({
  selector: 'app-purchase-order-details-page',
  imports: [
    ManageOnlyDirective,
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './purchase-order-details-page.html',
  styleUrl: './purchase-order-details-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PurchaseOrderDetailsPage {
  private readonly purchaseOrdersApi = inject(PurchaseOrdersApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly purchaseOrderId = Number(this.route.snapshot.paramMap.get('id'));
  readonly materialFilters =
    new FormGroup({
      lineItem:
        new FormControl('', {
          nonNullable: true,
        }),

      material:
        new FormControl('', {
          nonNullable: true,
        }),

      description:
        new FormControl('', {
          nonNullable: true,
        }),

      minQuantity:
        new FormControl('', {
          nonNullable: true,
        }),

      maxQuantity:
        new FormControl('', {
          nonNullable: true,
        }),

      minUnitPrice:
        new FormControl('', {
          nonNullable: true,
        }),

      maxUnitPrice:
        new FormControl('', {
          nonNullable: true,
        }),

      minInvoiceBalance:
        new FormControl('', {
          nonNullable: true,
        }),

      maxInvoiceBalance:
        new FormControl('', {
          nonNullable: true,
        }),

      minDispatchBalance:
        new FormControl('', {
          nonNullable: true,
        }),

      maxDispatchBalance:
        new FormControl('', {
          nonNullable: true,
        }),

      deliveryDateFrom:
        new FormControl('', {
          nonNullable: true,
        }),

      deliveryDateTo:
        new FormControl('', {
          nonNullable: true,
        }),

      overdueStatus:
        new FormControl<
          'all' | 'overdue' | 'not-overdue'
        >(
          'all',
          {
            nonNullable: true,
          },
        ),

      processStatus:
        new FormControl('', {
          nonNullable: true,
        }),

      invoiceStatus:
        new FormControl('', {
          nonNullable: true,
        }),

      dispatchStatus:
        new FormControl('', {
          nonNullable: true,
        }),
    });

  readonly details = signal<PurchaseOrderDetails | null>(null);
  readonly materialPage = signal(1);
  readonly materialPageSize = signal(25);
  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly errorStatus = signal<number | null>(null);

  readonly materials = computed(() => this.details()?.materials.items ?? []);
  readonly materialTotalRecords = computed(() => this.details()?.materials.totalRecords ?? 0);
  readonly materialTotalPages = computed(() => this.details()?.materials.totalPages ?? 1);
  readonly hasPreviousPage = computed(() => this.materialPage() > 1);
  readonly hasNextPage = computed(() => this.materialPage() < this.materialTotalPages());
  readonly showingFrom = computed(() =>
    this.materialTotalRecords() === 0 ? 0 : (this.materialPage() - 1) * this.materialPageSize() + 1,
  );
  readonly showingTo = computed(() =>
    Math.min(this.materialPage() * this.materialPageSize(), this.materialTotalRecords()),
  );

  constructor() {
    if (!Number.isInteger(this.purchaseOrderId) || this.purchaseOrderId < 1) {
      this.errorStatus.set(400);
      this.errorMessage.set('The Purchase Order address contains an invalid ID.');
      return;
    }

    this.loadDetails();
  }

  applyMaterialFilter(
    filter: HTMLDetailsElement,
  ): void {
    filter.open = false;
    this.materialPage.set(1);
    this.loadDetails();
  }

  clearMaterialFilter(
    column:
      | 'line'
      | 'material'
      | 'description'
      | 'quantity'
      | 'unitPrice'
      | 'invoiceBalance'
      | 'dispatchBalance'
      | 'delivery'
      | 'process'
      | 'invoice'
      | 'dispatch',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'line':
        this.materialFilters.controls
          .lineItem.setValue('');
        break;

      case 'material':
        this.materialFilters.controls
          .material.setValue('');
        break;

      case 'description':
        this.materialFilters.controls
          .description.setValue('');
        break;

      case 'quantity':
        this.materialFilters.patchValue({
          minQuantity: '',
          maxQuantity: '',
        });
        break;

      case 'unitPrice':
        this.materialFilters.patchValue({
          minUnitPrice: '',
          maxUnitPrice: '',
        });
        break;

      case 'invoiceBalance':
        this.materialFilters.patchValue({
          minInvoiceBalance: '',
          maxInvoiceBalance: '',
        });
        break;

      case 'dispatchBalance':
        this.materialFilters.patchValue({
          minDispatchBalance: '',
          maxDispatchBalance: '',
        });
        break;

      case 'delivery':
        this.materialFilters.patchValue({
          deliveryDateFrom: '',
          deliveryDateTo: '',
          overdueStatus: 'all',
        });
        break;

      case 'process':
        this.materialFilters.controls
          .processStatus.setValue('');
        break;

      case 'invoice':
        this.materialFilters.controls
          .invoiceStatus.setValue('');
        break;

      case 'dispatch':
        this.materialFilters.controls
          .dispatchStatus.setValue('');
        break;
    }

    filter.open = false;
    this.materialPage.set(1);
    this.loadDetails();
  }

  materialFilterActive(
    column:
      | 'line'
      | 'material'
      | 'description'
      | 'quantity'
      | 'unitPrice'
      | 'invoiceBalance'
      | 'dispatchBalance'
      | 'delivery'
      | 'process'
      | 'invoice'
      | 'dispatch',
  ): boolean {
    const values =
      this.materialFilters.getRawValue();

    switch (column) {
      case 'line':
        return Boolean(
          values.lineItem.trim(),
        );

      case 'material':
        return Boolean(
          values.material.trim(),
        );

      case 'description':
        return Boolean(
          values.description.trim(),
        );

      case 'quantity':
        return Boolean(
          values.minQuantity.trim() ||
          values.maxQuantity.trim(),
        );

      case 'unitPrice':
        return Boolean(
          values.minUnitPrice.trim() ||
          values.maxUnitPrice.trim(),
        );

      case 'invoiceBalance':
        return Boolean(
          values.minInvoiceBalance.trim() ||
          values.maxInvoiceBalance.trim(),
        );

      case 'dispatchBalance':
        return Boolean(
          values.minDispatchBalance.trim() ||
          values.maxDispatchBalance.trim(),
        );

      case 'delivery':
        return Boolean(
          values.deliveryDateFrom ||
          values.deliveryDateTo ||
          values.overdueStatus !== 'all',
        );

      case 'process':
        return Boolean(
          values.processStatus.trim(),
        );

      case 'invoice':
        return Boolean(
          values.invoiceStatus.trim(),
        );

      case 'dispatch':
        return Boolean(
          values.dispatchStatus.trim(),
        );
    }

    return false;
  }

  changePageSize(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    if (![10, 25, 50, 100].includes(value)) {
      return;
    }

    this.materialPageSize.set(value);
    this.materialPage.set(1);
    this.loadDetails();
  }

  previousPage(): void {
    if (!this.hasPreviousPage() || this.loading()) {
      return;
    }

    this.materialPage.update((currentPage) => currentPage - 1);
    this.loadDetails();
  }

  nextPage(): void {
    if (!this.hasNextPage() || this.loading()) {
      return;
    }

    this.materialPage.update((currentPage) => currentPage + 1);
    this.loadDetails();
  }

  retry(): void {
    this.loadDetails();
  }

  statusClass(status: string): string {
    const normalizedStatus = status.toLowerCase();

    if (
      normalizedStatus.includes('complete') ||
      normalizedStatus.includes('fully') ||
      normalizedStatus === 'dispatched'
    ) {
      return 'status status--success';
    }

    if (
      normalizedStatus.includes('progress') ||
      normalizedStatus.includes('partial') ||
      normalizedStatus.includes('running')
    ) {
      return 'status status--info';
    }

    if (normalizedStatus.includes('pending') || normalizedStatus.startsWith('not ')) {
      return 'status status--warning';
    }

    return 'status status--neutral';
  }

  private loadDetails(): void {
    if (this.loading() || !Number.isInteger(this.purchaseOrderId) || this.purchaseOrderId < 1) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');
    this.errorStatus.set(null);

    const materialFilters =
      this.materialFilters.getRawValue();

    this.purchaseOrdersApi
      .getPurchaseOrderDetailsFiltered(
        this.purchaseOrderId,
        {
          materialPage:
            this.materialPage(),

          materialPageSize:
            this.materialPageSize(),

          ...materialFilters,
        },
      )
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.details.set(response);
          this.materialPage.set(response.materials.page);
          this.materialPageSize.set(response.materials.pageSize);
        },
        error: (error: HttpErrorResponse) => {
          this.details.set(null);
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
      return 'Your account does not have permission to view this Purchase Order.';
    }

    if (error.status === 404) {
      return `Purchase Order ${this.purchaseOrderId} was not found.`;
    }

    const apiMessage = error.error?.error;

    return typeof apiMessage === 'string'
      ? apiMessage
      : 'Purchase Order details could not be loaded. Please try again.';
  }
}

