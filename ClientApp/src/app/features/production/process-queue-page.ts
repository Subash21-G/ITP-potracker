import { DatePipe } from '@angular/common';
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
  ProcessQueueExcelFilterColumn,
  ProcessQueueExcelFilterSelections,
  ProcessQueueExcelFilterValue,
  ProcessQueueResponse,
  ProcessQueueSortDirection,
} from '../../core/models/production-api.model';
import { ProductionApiService } from '../../core/services/production-api.service';
import { ServerExcelFilter } from '../../shared/server-excel-filter/server-excel-filter';

const EMPTY_QUEUE: ProcessQueueResponse = {
  summary: {
    totalMaterials: 0,
    overdue: 0,
    startToday: 0,
    thisWeek: 0,
    future: 0,
  },
  overdue: [],
  startToday: [],
  thisWeek: [],
  future: [],
  items: [],
  page: 1,
  pageSize: 50,
  totalCount: 0,
  totalPages: 1,
};

@Component({
  selector: 'app-process-queue-page',
  imports: [DatePipe, ReactiveFormsModule, RouterLink, ServerExcelFilter],
  templateUrl: './process-queue-page.html',
  styleUrls: ['../invoices/invoices-page.css', './process-queue-page.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProcessQueuePage {
  private readonly productionApi = inject(ProductionApiService);

  private readonly destroyRef = inject(DestroyRef);

  readonly filters = new FormGroup({
    search: new FormControl('', {
      nonNullable: true,
    }),
  });

  readonly columnFilters = new FormGroup({
    material: new FormControl('', {
      nonNullable: true,
    }),

    description: new FormControl('', {
      nonNullable: true,
    }),

    poNumber: new FormControl('', {
      nonNullable: true,
    }),

    purchaser: new FormControl('', {
      nonNullable: true,
    }),

    processStartFrom: new FormControl('', {
      nonNullable: true,
    }),

    processStartTo: new FormControl('', {
      nonNullable: true,
    }),

    deliveryFrom: new FormControl('', {
      nonNullable: true,
    }),

    deliveryTo: new FormControl('', {
      nonNullable: true,
    }),

    owner: new FormControl('', {
      nonNullable: true,
    }),

    processStatus: new FormControl('', {
      nonNullable: true,
    }),

    dispatchStatus: new FormControl('', {
      nonNullable: true,
    }),
  });

  readonly queue = signal<ProcessQueueResponse>(EMPTY_QUEUE);

  readonly page = signal(1);
  readonly pageSize = signal(50);

  readonly sortColumn = signal<ProcessQueueExcelFilterColumn | null>(null);

  readonly sortDirection = signal<ProcessQueueSortDirection | null>(null);

  readonly excelFilterSelections = signal<ProcessQueueExcelFilterSelections>({});

  readonly excelFilterOptions = signal<
    Partial<Record<ProcessQueueExcelFilterColumn, readonly ProcessQueueExcelFilterValue[]>>
  >({});

  readonly excelFilterLoadingColumns = signal<ReadonlySet<ProcessQueueExcelFilterColumn>>(
    new Set<ProcessQueueExcelFilterColumn>(),
  );

  readonly loading = signal(false);
  readonly errorMessage = signal('');

  readonly hasPreviousPage = computed(() => this.page() > 1);

  readonly hasNextPage = computed(() => this.page() < this.queue().totalPages);

  constructor() {
    this.loadQueue();
  }

  applyFilters(): void {
    this.page.set(1);
    this.loadQueue();
  }

  clearFilters(): void {
    this.filters.reset({
      search: '',
    });

    this.excelFilterSelections.set({});

    this.columnFilters.reset({
      material: '',
      description: '',
      poNumber: '',
      purchaser: '',
      processStartFrom: '',
      processStartTo: '',
      deliveryFrom: '',
      deliveryTo: '',
      owner: '',
      processStatus: '',
      dispatchStatus: '',
    });

    this.page.set(1);
    this.loadQueue();
  }

  applyColumnFilter(filter: HTMLDetailsElement): void {
    filter.open = false;
    this.page.set(1);
    this.loadQueue();
  }

  clearColumnFilter(
    column:
      | 'material'
      | 'po'
      | 'purchaser'
      | 'processStart'
      | 'delivery'
      | 'owner'
      | 'process'
      | 'dispatch',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'material':
        this.columnFilters.patchValue({
          material: '',
          description: '',
        });
        break;

      case 'po':
        this.columnFilters.controls.poNumber.setValue('');
        break;

      case 'purchaser':
        this.columnFilters.controls.purchaser.setValue('');
        break;

      case 'processStart':
        this.columnFilters.patchValue({
          processStartFrom: '',
          processStartTo: '',
        });
        break;

      case 'delivery':
        this.columnFilters.patchValue({
          deliveryFrom: '',
          deliveryTo: '',
        });
        break;

      case 'owner':
        this.columnFilters.controls.owner.setValue('');
        break;

      case 'process':
        this.columnFilters.controls.processStatus.setValue('');
        break;

      case 'dispatch':
        this.columnFilters.controls.dispatchStatus.setValue('');
        break;
    }

    filter.open = false;
    this.page.set(1);
    this.loadQueue();
  }

  columnFilterActive(
    column:
      | 'material'
      | 'po'
      | 'purchaser'
      | 'processStart'
      | 'delivery'
      | 'owner'
      | 'process'
      | 'dispatch',
  ): boolean {
    const values = this.columnFilters.getRawValue();

    switch (column) {
      case 'material':
        return Boolean(values.material.trim() || values.description.trim());

      case 'po':
        return Boolean(values.poNumber.trim());

      case 'purchaser':
        return Boolean(values.purchaser.trim());

      case 'processStart':
        return Boolean(values.processStartFrom || values.processStartTo);

      case 'delivery':
        return Boolean(values.deliveryFrom || values.deliveryTo);

      case 'owner':
        return Boolean(values.owner.trim());

      case 'process':
        return Boolean(values.processStatus.trim());

      case 'dispatch':
        return Boolean(values.dispatchStatus.trim());
    }

    return false;
  }

  excelFilterOptionsFor(
    column: ProcessQueueExcelFilterColumn,
  ): readonly ProcessQueueExcelFilterValue[] {
    return this.excelFilterOptions()[column] ?? [];
  }

  excelFilterSelectedValues(column: ProcessQueueExcelFilterColumn): readonly string[] {
    return this.excelFilterSelections()[column] ?? [];
  }

  excelFilterActive(column: ProcessQueueExcelFilterColumn): boolean {
    return Object.prototype.hasOwnProperty.call(this.excelFilterSelections(), column);
  }

  excelFilterLoading(column: ProcessQueueExcelFilterColumn): boolean {
    return this.excelFilterLoadingColumns().has(column);
  }

  excelSortDirection(column: ProcessQueueExcelFilterColumn): ProcessQueueSortDirection | null {
    return this.sortColumn() === column ? this.sortDirection() : null;
  }

  loadExcelFilterValues(column: ProcessQueueExcelFilterColumn): void {
    if (this.excelFilterLoading(column)) {
      return;
    }

    this.excelFilterLoadingColumns.update((columns) => {
      const updated = new Set(columns);

      updated.add(column);

      return updated;
    });

    this.productionApi
      .getProcessQueueExcelFilterValues(this.buildQueueQuery(false), column)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() =>
          this.excelFilterLoadingColumns.update((columns) => {
            const updated = new Set(columns);

            updated.delete(column);

            return updated;
          }),
        ),
      )
      .subscribe({
        next: (options) => {
          this.excelFilterOptions.update((current) => ({
            ...current,
            [column]: options,
          }));
        },

        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(this.describeError(error));
        },
      });
  }

  applyExcelSort(
    column: ProcessQueueExcelFilterColumn,
    direction: ProcessQueueSortDirection | null,
  ): void {
    if (this.loading()) {
      return;
    }

    this.sortColumn.set(direction === null ? null : column);

    this.sortDirection.set(direction);

    this.page.set(1);
    this.loadQueue();
  }

  applyExcelFilter(column: ProcessQueueExcelFilterColumn, values: readonly string[]): void {
    const availableValues = this.excelFilterOptions()[column] ?? [];

    const selectedSet = new Set(values);

    const selectsEverything =
      availableValues.length > 0 &&
      availableValues.length === selectedSet.size &&
      availableValues.every((option) => selectedSet.has(option.value));

    this.excelFilterSelections.update((current) => {
      const updated = {
        ...current,
      };

      if (selectsEverything) {
        delete updated[column];
      } else {
        updated[column] = [...values];
      }

      return updated;
    });

    this.page.set(1);
    this.loadQueue();
  }

  clearExcelFilter(column: ProcessQueueExcelFilterColumn): void {
    this.excelFilterSelections.update((current) => {
      const updated = {
        ...current,
      };

      delete updated[column];

      return updated;
    });

    this.page.set(1);
    this.loadQueue();
  }

  changePageSize(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    if (![10, 25, 50, 100, 200].includes(value)) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.loadQueue();
  }

  previousPage(): void {
    if (!this.hasPreviousPage() || this.loading()) {
      return;
    }

    this.page.update((current) => current - 1);

    this.loadQueue();
  }

  nextPage(): void {
    if (!this.hasNextPage() || this.loading()) {
      return;
    }

    this.page.update((current) => current + 1);

    this.loadQueue();
  }

  retry(): void {
    this.loadQueue();
  }

  private buildQueueQuery(includePaging: boolean) {
    const filters = this.filters.getRawValue();

    const columnFilters = this.columnFilters.getRawValue();

    return {
      search: filters.search,

      material: columnFilters.material,

      description: columnFilters.description,

      poNumber: columnFilters.poNumber,

      purchaser: columnFilters.purchaser,

      processStartFrom: columnFilters.processStartFrom,

      processStartTo: columnFilters.processStartTo,

      deliveryFrom: columnFilters.deliveryFrom,

      deliveryTo: columnFilters.deliveryTo,

      owner: columnFilters.owner,

      processStatus: columnFilters.processStatus,

      dispatchStatus: columnFilters.dispatchStatus,

      sortColumn: this.sortColumn() ?? undefined,

      sortDirection: this.sortDirection() ?? undefined,

      excelFilters: this.serializeExcelFilters(),

      page: includePaging ? this.page() : 1,

      pageSize: this.pageSize(),
    };
  }

  private serializeExcelFilters(): string | undefined {
    const selections = this.excelFilterSelections();

    return Object.keys(selections).length > 0 ? JSON.stringify(selections) : undefined;
  }

  private loadQueue(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.productionApi
      .getProcessQueue(this.buildQueueQuery(true))
      .pipe(
        takeUntilDestroyed(this.destroyRef),

        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.queue.set(response);
          this.page.set(response.page);
          this.pageSize.set(response.pageSize);
        },

        error: (error: HttpErrorResponse) => {
          this.queue.set(EMPTY_QUEUE);

          this.errorMessage.set(this.describeError(error));
        },
      });
  }

  private describeError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Your account cannot view the process queue.';
    }

    const message = error.error?.message ?? error.error?.error;

    return typeof message === 'string' ? message : 'The process queue could not be loaded.';
  }
}
