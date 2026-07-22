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
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { PurchaseOrderDetails } from '../../core/models/purchase-order-details.model';
import { PurchaseOrdersApiService } from '../../core/services/purchase-orders-api.service';

@Component({
  selector: 'app-purchase-order-details-page',
  imports: [ManageOnlyDirective, DatePipe, DecimalPipe, RouterLink],
  templateUrl: './purchase-order-details-page.html',
  styleUrl: './purchase-order-details-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PurchaseOrderDetailsPage {
  private readonly purchaseOrdersApi = inject(PurchaseOrdersApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly purchaseOrderId = Number(this.route.snapshot.paramMap.get('id'));
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

    this.purchaseOrdersApi
      .getPurchaseOrderDetails(this.purchaseOrderId, this.materialPage(), this.materialPageSize())
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

