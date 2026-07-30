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

import { MaterialListItem } from '../../core/models/material.model';
import { MaterialsApiService } from '../../core/services/materials-api.service';

@Component({
  selector: 'app-materials-page',
  imports: [ManageOnlyDirective, 
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './materials-page.html',
  styleUrl: './materials-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaterialsPage {
  private readonly materialsApi = inject(MaterialsApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly filters = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    poNumber: new FormControl('', { nonNullable: true }),
    handler: new FormControl('', { nonNullable: true }),
    invoiceStatus: new FormControl('', { nonNullable: true }),
    dispatchStatus: new FormControl('', { nonNullable: true }),
    deliveryDateFrom: new FormControl('', { nonNullable: true }),
    deliveryDateTo: new FormControl('', { nonNullable: true }),
  });

  readonly items = signal<readonly MaterialListItem[]>([]);
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
  readonly showingTo = computed(() =>
    Math.min(this.page() * this.pageSize(), this.totalRecords()),
  );

  constructor() {
    const initialSearch =
      this.route.snapshot.queryParamMap
        .get('search')
        ?.trim() ?? '';

    if (initialSearch) {
      this.filters.controls.search.setValue(
        initialSearch,
      );
    }

    this.loadMaterials();
  }

  applyFilters(): void {
    this.page.set(1);
    this.loadMaterials();
  }

  clearFilters(): void {
    this.filters.reset();
    this.page.set(1);
    this.loadMaterials();
  }

  changePageSize(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    if (![10, 25, 50, 100].includes(value)) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.loadMaterials();
  }

  previousPage(): void {
    if (!this.hasPreviousPage() || this.loading()) {
      return;
    }

    this.page.update((currentPage) => currentPage - 1);
    this.loadMaterials();
  }

  nextPage(): void {
    if (!this.hasNextPage() || this.loading()) {
      return;
    }

    this.page.update((currentPage) => currentPage + 1);
    this.loadMaterials();
  }

  retry(): void {
    this.loadMaterials();
  }

  statusClass(status: string): string {
    const normalizedStatus = status.toLowerCase();

    if (
      normalizedStatus.includes('partial') ||
      normalizedStatus.includes('progress') ||
      normalizedStatus.includes('process')
    ) {
      return 'status status--warning';
    }

    if (
      normalizedStatus.includes('pending') ||
      normalizedStatus.startsWith('not ')
    ) {
      return 'status status--neutral';
    }

    if (
      normalizedStatus.includes('complete') ||
      normalizedStatus.includes('dispatch') ||
      normalizedStatus.includes('invoiced')
    ) {
      return 'status status--success';
    }

    return 'status status--neutral';
  }


  stockStatusClass(status: string): string {
    const normalizedStatus =
      status.toLowerCase();

    if (
      normalizedStatus.includes('available')
    ) {
      return 'status status--success';
    }

    if (
      normalizedStatus.includes('shortage') ||
      normalizedStatus.includes('partial')
    ) {
      return 'status status--warning';
    }

    if (
      normalizedStatus.includes('out of stock') ||
      normalizedStatus.includes('offline')
    ) {
      return 'status status--danger';
    }

    return 'status status--neutral';
  }
  private loadMaterials(): void {
    if (this.loading()) {
      return;
    }

    const filterValues = this.filters.getRawValue();

    this.loading.set(true);
    this.errorMessage.set('');
    this.errorStatus.set(null);

    this.materialsApi
      .getMaterials({
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
          this.page.set(response.page);
          this.pageSize.set(response.pageSize);
          this.totalRecords.set(response.totalRecords);
          this.totalPages.set(response.totalPages);
        },
        error: (error: HttpErrorResponse) => {
          this.items.set([]);
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
      return 'Your account does not have permission to read materials.';
    }

    if (error.status === 404) {
      return 'The materials API was not found. Confirm that app.MapControllers() is enabled.';
    }

    const apiMessage = error.error?.error;

    return typeof apiMessage === 'string'
      ? apiMessage
      : 'Materials could not be loaded. Please try again.';
  }
}



