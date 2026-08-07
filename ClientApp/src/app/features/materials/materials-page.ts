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
  MaterialListItem,
} from '../../core/models/material.model';
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
    poDateFrom: new FormControl('', { nonNullable: true }),
    poDateTo: new FormControl('', { nonNullable: true }),
    poReceivedDateFrom: new FormControl('', { nonNullable: true }),
    poReceivedDateTo: new FormControl('', { nonNullable: true }),
    invoiceStatus: new FormControl('', { nonNullable: true }),
    dispatchStatus: new FormControl('', { nonNullable: true }),
    stockStatus: new FormControl('', { nonNullable: true }),
    deliveryDateFrom: new FormControl('', { nonNullable: true }),
    deliveryDateTo: new FormControl('', { nonNullable: true }),
  });

  readonly columnFilters =
    new FormGroup({
      columnPoNumber:
        new FormControl('', {
          nonNullable: true,
        }),

      columnPurchaser:
        new FormControl('', {
          nonNullable: true,
        }),

      columnHandler:
        new FormControl('', {
          nonNullable: true,
        }),

      lineItem:
        new FormControl('', {
          nonNullable: true,
        }),

      materialNo:
        new FormControl('', {
          nonNullable: true,
        }),

      description:
        new FormControl('', {
          nonNullable: true,
        }),

      minOrderedQty:
        new FormControl('', {
          nonNullable: true,
        }),

      maxOrderedQty:
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

      minMaterialValue:
        new FormControl('', {
          nonNullable: true,
        }),

      maxMaterialValue:
        new FormControl('', {
          nonNullable: true,
        }),

      minDispatchQty:
        new FormControl('', {
          nonNullable: true,
        }),

      maxDispatchQty:
        new FormControl('', {
          nonNullable: true,
        }),

      minPendingQty:
        new FormControl('', {
          nonNullable: true,
        }),

      maxPendingQty:
        new FormControl('', {
          nonNullable: true,
        }),

      minAllocatedQty:
        new FormControl('', {
          nonNullable: true,
        }),

      maxAllocatedQty:
        new FormControl('', {
          nonNullable: true,
        }),

      minLiveStockQty:
        new FormControl('', {
          nonNullable: true,
        }),

      maxLiveStockQty:
        new FormControl('', {
          nonNullable: true,
        }),

      minShortageQty:
        new FormControl('', {
          nonNullable: true,
        }),

      maxShortageQty:
        new FormControl('', {
          nonNullable: true,
        }),

      columnStockStatus:
        new FormControl('', {
          nonNullable: true,
        }),

      processStatus:
        new FormControl('', {
          nonNullable: true,
        }),

      columnDispatchStatus:
        new FormControl('', {
          nonNullable: true,
        }),

      columnDeliveryDateFrom:
        new FormControl('', {
          nonNullable: true,
        }),

      columnDeliveryDateTo:
        new FormControl('', {
          nonNullable: true,
        }),
    });

  readonly items = signal<readonly MaterialListItem[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly totalRecords = signal(0);
  readonly totalMaterialValue = signal(0);
  readonly totalPages = signal(1);
  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly errorStatus = signal<number | null>(null);
  readonly successMessage = signal('');
  readonly downloadingExcel = signal(false);
  readonly bulkDeleting = signal(false);

  readonly selectedIds =
    signal<ReadonlySet<number>>(
      new Set<number>(),
    );

  readonly hasPreviousPage = computed(() => this.page() > 1);
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
      const materials =
        this.items();

      const selected =
        this.selectedIds();

      return (
        materials.length > 0 &&
        materials.every(
          (material) =>
            selected.has(
              material.id,
            ),
        )
      );
    });
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
    this.clearSelection();
    this.successMessage.set('');
    this.loadMaterials();
  }

  applyColumnFilter(
    filter: HTMLDetailsElement,
  ): void {
    filter.open = false;
    this.page.set(1);
    this.clearSelection();
    this.successMessage.set('');
    this.loadMaterials();
  }

  clearColumnFilter(
    column:
      | 'po'
      | 'line'
      | 'material'
      | 'description'
      | 'ordered'
      | 'unitPrice'
      | 'materialValue'
      | 'dispatched'
      | 'pending'
      | 'allocatedLive'
      | 'shortage'
      | 'stock'
      | 'process'
      | 'dispatch'
      | 'delivery',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'po':
        this.columnFilters.patchValue({
          columnPoNumber: '',
          columnPurchaser: '',
          columnHandler: '',
        });
        break;

      case 'line':
        this.columnFilters.controls
          .lineItem.setValue('');
        break;

      case 'material':
        this.columnFilters.controls
          .materialNo.setValue('');
        break;

      case 'description':
        this.columnFilters.controls
          .description.setValue('');
        break;

      case 'ordered':
        this.columnFilters.patchValue({
          minOrderedQty: '',
          maxOrderedQty: '',
        });
        break;

      case 'unitPrice':
        this.columnFilters.patchValue({
          minUnitPrice: '',
          maxUnitPrice: '',
        });
        break;

      case 'materialValue':
        this.columnFilters.patchValue({
          minMaterialValue: '',
          maxMaterialValue: '',
        });
        break;

      case 'dispatched':
        this.columnFilters.patchValue({
          minDispatchQty: '',
          maxDispatchQty: '',
        });
        break;

      case 'pending':
        this.columnFilters.patchValue({
          minPendingQty: '',
          maxPendingQty: '',
        });
        break;

      case 'allocatedLive':
        this.columnFilters.patchValue({
          minAllocatedQty: '',
          maxAllocatedQty: '',
          minLiveStockQty: '',
          maxLiveStockQty: '',
        });
        break;

      case 'shortage':
        this.columnFilters.patchValue({
          minShortageQty: '',
          maxShortageQty: '',
        });
        break;

      case 'stock':
        this.columnFilters.controls
          .columnStockStatus.setValue('');
        break;

      case 'process':
        this.columnFilters.controls
          .processStatus.setValue('');
        break;

      case 'dispatch':
        this.columnFilters.controls
          .columnDispatchStatus.setValue('');
        break;

      case 'delivery':
        this.columnFilters.patchValue({
          columnDeliveryDateFrom: '',
          columnDeliveryDateTo: '',
        });
        break;
    }

    filter.open = false;
    this.page.set(1);
    this.clearSelection();
    this.successMessage.set('');
    this.loadMaterials();
  }

  columnFilterActive(
    column:
      | 'po'
      | 'line'
      | 'material'
      | 'description'
      | 'ordered'
      | 'unitPrice'
      | 'materialValue'
      | 'dispatched'
      | 'pending'
      | 'allocatedLive'
      | 'shortage'
      | 'stock'
      | 'process'
      | 'dispatch'
      | 'delivery',
  ): boolean {
    const values =
      this.columnFilters.getRawValue();

    switch (column) {
      case 'po':
        return Boolean(
          values.columnPoNumber.trim() ||
          values.columnPurchaser.trim() ||
          values.columnHandler.trim(),
        );

      case 'line':
        return Boolean(
          values.lineItem.trim(),
        );

      case 'material':
        return Boolean(
          values.materialNo.trim(),
        );

      case 'description':
        return Boolean(
          values.description.trim(),
        );

      case 'ordered':
        return Boolean(
          values.minOrderedQty.trim() ||
          values.maxOrderedQty.trim(),
        );

      case 'unitPrice':
        return Boolean(
          values.minUnitPrice.trim() ||
          values.maxUnitPrice.trim(),
        );

      case 'materialValue':
        return Boolean(
          values.minMaterialValue.trim() ||
          values.maxMaterialValue.trim(),
        );

      case 'dispatched':
        return Boolean(
          values.minDispatchQty.trim() ||
          values.maxDispatchQty.trim(),
        );

      case 'pending':
        return Boolean(
          values.minPendingQty.trim() ||
          values.maxPendingQty.trim(),
        );

      case 'allocatedLive':
        return Boolean(
          values.minAllocatedQty.trim() ||
          values.maxAllocatedQty.trim() ||
          values.minLiveStockQty.trim() ||
          values.maxLiveStockQty.trim(),
        );

      case 'shortage':
        return Boolean(
          values.minShortageQty.trim() ||
          values.maxShortageQty.trim(),
        );

      case 'stock':
        return Boolean(
          values.columnStockStatus,
        );

      case 'process':
        return Boolean(
          values.processStatus.trim(),
        );

      case 'dispatch':
        return Boolean(
          values.columnDispatchStatus.trim(),
        );

      case 'delivery':
        return Boolean(
          values.columnDeliveryDateFrom ||
          values.columnDeliveryDateTo,
        );
    }

    return false;
  }

  clearFilters(): void {
    this.filters.reset();
    this.columnFilters.reset();
    this.page.set(1);
    this.clearSelection();
    this.successMessage.set('');
    this.loadMaterials();
  }

  changePageSize(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    if (![25, 50, 100].includes(value)) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.clearSelection();
    this.loadMaterials();
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
    this.loadMaterials();
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

  toggleMaterial(
    materialId: number,
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
            materialId,
          );
        } else {
          updated.delete(
            materialId,
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
          (material) =>
            material.id,
        ),
      ),
    );
  }

  downloadExcel(): void {
    if (
      this.downloadingExcel() ||
      this.loading()
    ) {
      return;
    }

    const filterValues =
      this.filters.getRawValue();

    const columnValues =
      this.columnFilters.getRawValue();

    this.downloadingExcel.set(
      true,
    );

    this.errorMessage.set('');
    this.successMessage.set('');

    this.materialsApi
      .downloadExcel({
        page: 1,
        pageSize:
          this.pageSize(),
        ...filterValues,
        ...columnValues,
      })
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() =>
          this.downloadingExcel.set(
            false,
          ),
        ),
      )
      .subscribe({
        next: (file) => {
          if (
            !file ||
            file.size === 0
          ) {
            this.errorMessage.set(
              'The material Excel export was empty.',
            );

            return;
          }

          const downloadUrl =
            URL.createObjectURL(
              file,
            );

          const downloadLink =
            document.createElement(
              'a',
            );

          const timestamp =
            new Date()
              .toISOString()
              .slice(0, 19)
              .replace(
                'T',
                '_',
              )
              .replaceAll(
                ':',
                '',
              )
              .replaceAll(
                '-',
                '',
              );

          downloadLink.href =
            downloadUrl;

          downloadLink.download =
            `Materials_${timestamp}.xlsx`;

          document.body.appendChild(
            downloadLink,
          );

          downloadLink.click();
          downloadLink.remove();

          URL.revokeObjectURL(
            downloadUrl,
          );

          this.successMessage.set(
            'Material Excel export downloaded successfully.',
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeMutationError(
              error,
              'Material Excel export could not be downloaded.',
            ),
          );
        },
      });
  }

  bulkDeleteMaterials(): void {
    const materialIds =
      Array.from(
        this.selectedIds(),
      );

    if (
      materialIds.length === 0 ||
      this.bulkDeleting() ||
      this.loading()
    ) {
      return;
    }

    const confirmed =
      window.confirm(
        `Delete ${materialIds.length} selected material(s)?\n\n` +
          'Materials linked to invoices or dispatch records will be safely skipped.',
      );

    if (!confirmed) {
      return;
    }

    this.bulkDeleting.set(
      true,
    );

    this.errorMessage.set('');
    this.successMessage.set('');

    this.materialsApi
      .bulkDelete({
        materialIds,
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
                .deletedMaterialIds,
            );

          this.selectedIds.update(
            (selectedIds) =>
              new Set(
                Array.from(
                  selectedIds,
                ).filter(
                  (materialId) =>
                    !deletedIds.has(
                      materialId,
                    ),
                ),
              ),
          );

          if (
            response.deletedCount >
            0
          ) {
            this.successMessage.set(
              response.message ||
                'Selected materials deleted successfully.',
            );

            const currentPageDeleted =
              this.items().length >
                0 &&
              this.items().every(
                (material) =>
                  deletedIds.has(
                    material.id,
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

            this.loadMaterials();
            return;
          }

          this.errorMessage.set(
            response.message ||
              'No selected materials were deleted.',
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeMutationError(
              error,
              'The selected materials could not be deleted.',
            ),
          );
        },
      });
  }

  private clearSelection(): void {
    this.selectedIds.set(
      new Set<number>(),
    );
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

    const filterValues =
      this.filters.getRawValue();

    const columnValues =
      this.columnFilters.getRawValue();

    this.loading.set(true);
    this.errorMessage.set('');
    this.errorStatus.set(null);

    this.materialsApi
      .getMaterials({
        page:
          this.page(),
        pageSize:
          this.pageSize(),
        ...filterValues,
        ...columnValues,
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
          this.totalMaterialValue.set(
            response.totalValue ?? 0,
          );
          this.totalPages.set(
            response.totalPages,
          );

          const visibleIds =
            new Set(
              response.items.map(
                (material) =>
                  material.id,
              ),
            );

          this.selectedIds.update(
            (selectedIds) =>
              new Set(
                Array.from(
                  selectedIds,
                ).filter(
                  (materialId) =>
                    visibleIds.has(
                      materialId,
                    ),
                ),
              ),
          );        },
        error: (error: HttpErrorResponse) => {
          this.items.set([]);
          this.totalRecords.set(0);
          this.totalMaterialValue.set(0);
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
      return 'Your account does not have permission to perform this action.';
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
