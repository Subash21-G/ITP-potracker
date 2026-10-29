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
  DrawingExcelFilterColumn,
  DrawingExcelFilterSelections,
  DrawingExcelFilterValue,
  DrawingListItem,
  DrawingSortDirection,
  DrawingSummary,
} from '../../core/models/insights-api.model';
import { InsightsApiService } from '../../core/services/insights-api.service';
import { ServerExcelFilter } from '../../shared/server-excel-filter/server-excel-filter';

@Component({
  selector: 'app-drawings-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ManageOnlyDirective,
    ReactiveFormsModule,
    RouterLink,
    ServerExcelFilter,
  ],
  templateUrl: './drawings-page.html',
  styleUrl: '../insights-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DrawingsPage {
  private readonly api = inject(InsightsApiService);

  private readonly destroyRef = inject(DestroyRef);

  readonly searchControl = new FormControl('', {
    nonNullable: true,
  });

  readonly statusControl = new FormControl('', {
    nonNullable: true,
  });

  readonly columnFilters = new FormGroup({
    material: new FormControl('', {
      nonNullable: true,
    }),

    poNumber: new FormControl('', {
      nonNullable: true,
    }),

    drawingVersion: new FormControl('', {
      nonNullable: true,
    }),

    updatedDateFrom: new FormControl('', {
      nonNullable: true,
    }),

    updatedDateTo: new FormControl('', {
      nonNullable: true,
    }),

    drawingAvailability: new FormControl('', {
      nonNullable: true,
    }),

    pdfAvailability: new FormControl('', {
      nonNullable: true,
    }),

    columnStatus: new FormControl('', {
      nonNullable: true,
    }),
  });

  readonly items = signal<readonly DrawingListItem[]>([]);

  readonly summary = signal<DrawingSummary>({
    totalMaterials: 0,
    missingDrawings: 0,
    updatedThisWeek: 0,
    withVersion: 0,
  });

  readonly page = signal(1);
  readonly pageSize = signal(25);

  readonly sortColumn = signal<DrawingExcelFilterColumn | null>(null);

  readonly sortDirection = signal<DrawingSortDirection | null>(null);

  readonly excelFilterSelections = signal<DrawingExcelFilterSelections>({});

  readonly excelFilterOptions = signal<
    Partial<Record<DrawingExcelFilterColumn, readonly DrawingExcelFilterValue[]>>
  >({});

  readonly excelFilterLoadingColumns = signal<ReadonlySet<DrawingExcelFilterColumn>>(
    new Set<DrawingExcelFilterColumn>(),
  );

  readonly totalRecords = signal(0);
  readonly totalPages = signal(1);
  readonly loading = signal(false);
  readonly exporting = signal(false);
  readonly errorMessage = signal('');

  readonly hasPreviousPage = computed(() => this.page() > 1);

  readonly hasNextPage = computed(() => this.page() < this.totalPages());

  readonly showingFrom = computed(() =>
    this.totalRecords() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );

  readonly showingTo = computed(() => Math.min(this.page() * this.pageSize(), this.totalRecords()));

  constructor() {
    this.load();
  }

  excelFilterOptionsFor(column: DrawingExcelFilterColumn): readonly DrawingExcelFilterValue[] {
    return this.excelFilterOptions()[column] ?? [];
  }

  excelFilterSelectedValues(column: DrawingExcelFilterColumn): readonly string[] {
    return this.excelFilterSelections()[column] ?? [];
  }

  excelFilterActive(column: DrawingExcelFilterColumn): boolean {
    return Object.prototype.hasOwnProperty.call(this.excelFilterSelections(), column);
  }

  excelFilterLoading(column: DrawingExcelFilterColumn): boolean {
    return this.excelFilterLoadingColumns().has(column);
  }

  excelSortDirection(column: DrawingExcelFilterColumn): DrawingSortDirection | null {
    return this.sortColumn() === column ? this.sortDirection() : null;
  }

  loadExcelFilterValues(column: DrawingExcelFilterColumn): void {
    if (this.excelFilterLoading(column)) {
      return;
    }

    this.excelFilterLoadingColumns.update((columns) => {
      const updated = new Set(columns);

      updated.add(column);

      return updated;
    });

    this.api
      .getDrawingExcelFilterValues(this.buildDrawingQuery(false), column)
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

  applyExcelSort(column: DrawingExcelFilterColumn, direction: DrawingSortDirection | null): void {
    if (this.loading()) {
      return;
    }

    this.sortColumn.set(direction === null ? null : column);

    this.sortDirection.set(direction);

    this.page.set(1);
    this.load();
  }

  applyExcelFilter(column: DrawingExcelFilterColumn, values: readonly string[]): void {
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
    this.load();
  }

  clearExcelFilter(column: DrawingExcelFilterColumn): void {
    this.excelFilterSelections.update((current) => {
      const updated = {
        ...current,
      };

      delete updated[column];

      return updated;
    });

    this.page.set(1);
    this.load();
  }

  applyColumnFilter(filter: HTMLDetailsElement): void {
    filter.open = false;
    this.page.set(1);
    this.load();
  }

  clearColumnFilter(
    column: 'material' | 'po' | 'version' | 'updated' | 'drawing' | 'pdf' | 'status',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'material':
        this.columnFilters.controls.material.setValue('');
        break;

      case 'po':
        this.columnFilters.controls.poNumber.setValue('');
        break;

      case 'version':
        this.columnFilters.controls.drawingVersion.setValue('');
        break;

      case 'updated':
        this.columnFilters.patchValue({
          updatedDateFrom: '',
          updatedDateTo: '',
        });
        break;

      case 'drawing':
        this.columnFilters.controls.drawingAvailability.setValue('');
        break;

      case 'pdf':
        this.columnFilters.controls.pdfAvailability.setValue('');
        break;

      case 'status':
        this.columnFilters.controls.columnStatus.setValue('');
        break;
    }

    filter.open = false;
    this.page.set(1);
    this.load();
  }

  columnFilterActive(
    column: 'material' | 'po' | 'version' | 'updated' | 'drawing' | 'pdf' | 'status',
  ): boolean {
    const value = this.columnFilters.getRawValue();

    switch (column) {
      case 'material':
        return Boolean(value.material.trim());

      case 'po':
        return Boolean(value.poNumber.trim());

      case 'version':
        return Boolean(value.drawingVersion.trim());

      case 'updated':
        return Boolean(value.updatedDateFrom || value.updatedDateTo);

      case 'drawing':
        return Boolean(value.drawingAvailability);

      case 'pdf':
        return Boolean(value.pdfAvailability);

      case 'status':
        return Boolean(value.columnStatus);
    }

    return false;
  }

  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  clearFilters(): void {
    this.searchControl.setValue('', {
      emitEvent: false,
    });

    this.statusControl.setValue('', {
      emitEvent: false,
    });

    this.columnFilters.reset(undefined, {
      emitEvent: false,
    });

    this.excelFilterSelections.set({});

    this.page.set(1);
    this.load();
  }

  changePageSize(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    if (![10, 25, 50, 100].includes(value)) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.load();
  }

  previousPage(): void {
    if (!this.hasPreviousPage() || this.loading()) {
      return;
    }

    this.page.update((current) => current - 1);

    this.load();
  }

  nextPage(): void {
    if (!this.hasNextPage() || this.loading()) {
      return;
    }

    this.page.update((current) => current + 1);

    this.load();
  }

  retry(): void {
    this.load();
  }

  downloadSource(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const url = select.value;
    select.value = '';
    if (!url.startsWith('/api/private-documents/')) {
      return;
    }

    const link = document.createElement('a');
    link.href = url;
    document.body.appendChild(link);
    link.click();
    link.remove();
  }

  exportExcel(): void {
    if (this.exporting() || this.loading() || this.totalRecords() === 0) {
      return;
    }
    this.exporting.set(true);
    this.errorMessage.set('');
    this.api.exportDrawingsExcel(this.buildDrawingQuery(false))
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.exporting.set(false)),
      )
      .subscribe({
        next: (file) => {
          if (file.size === 0) {
            this.errorMessage.set('The drawing Excel export was empty.');
            return;
          }
          const url = URL.createObjectURL(file);
          const link = document.createElement('a');
          link.href = url;
          link.download = `Drawing_Revisions_${new Date().toISOString().slice(0, 10)}.xlsx`;
          document.body.appendChild(link);
          link.click();
          link.remove();
          URL.revokeObjectURL(url);
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(this.describeError(error));
        },
      });
  }

  private buildDrawingQuery(includePaging: boolean) {
    return {
      search: this.searchControl.value,

      status: this.statusControl.value,

      ...this.columnFilters.getRawValue(),

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

  private load(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.api
      .getDrawings(this.buildDrawingQuery(true))
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

          this.totalRecords.set(response.totalRecords);

          this.totalPages.set(response.totalPages);
        },

        error: (error: HttpErrorResponse) => {
          this.items.set([]);
          this.totalRecords.set(0);
          this.totalPages.set(1);
          this.errorMessage.set(this.describeError(error));
        },
      });
  }

  private describeError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to view drawings.';
    }

    const message = error.error?.message ?? error.error?.error;

    return typeof message === 'string' ? message : 'Drawing records could not be loaded.';
  }
}
