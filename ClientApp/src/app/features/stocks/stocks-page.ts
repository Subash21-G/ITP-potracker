import {
  DatePipe,
  DecimalPipe,
} from '@angular/common';
import {
  HttpErrorResponse,
} from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  takeUntilDestroyed,
} from '@angular/core/rxjs-interop';
import {
  FormControl,
  ReactiveFormsModule,
} from '@angular/forms';
import {
  finalize,
} from 'rxjs';

import {
  StockExcelFilterColumn,
  StockExcelFilterSelections,
  StockExcelFilterValue,
  StockListItem,
  StockSortDirection,
  StockSummary,
} from '../../core/models/stock-api.model';
import {
  StockApiService,
} from '../../core/services/stock-api.service';
import {
  ServerExcelFilter,
} from '../../shared/server-excel-filter/server-excel-filter';

@Component({
  selector: 'app-stocks-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    ServerExcelFilter,
  ],
  templateUrl: './stocks-page.html',
  styleUrl: './stocks-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class StocksPage {
  private readonly stockApi =
    inject(StockApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly searchControl =
    new FormControl(
      '',
      {
        nonNullable: true,
      },
    );

  readonly materialControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly descriptionControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly supplierControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly locationControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly minCurrentStockControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly maxCurrentStockControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly minMinimumStockControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly maxMinimumStockControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly minUnitPriceControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly maxUnitPriceControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly minTotalValueControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly maxTotalValueControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly statusControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly updatedFromControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly updatedToControl =
    new FormControl('', {
      nonNullable: true,
    });

  readonly items =
    signal<readonly StockListItem[]>([]);

  readonly summary =
    signal<StockSummary>({
      totalRecords: 0,
      uniqueMaterials: 0,
      lowStockRecords: 0,
      availableRecords: 0,
      updatedToday: 0,
      totalCurrentStock: 0,
      totalInventoryValue: 0,
    });

  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly totalRecords = signal(0);
  readonly totalPages = signal(1);

  readonly sortColumn =
    signal<
      StockExcelFilterColumn | null
    >(null);

  readonly sortDirection =
    signal<
      StockSortDirection | null
    >(null);

  readonly excelFilterSelections =
    signal<StockExcelFilterSelections>(
      {},
    );

  readonly excelFilterOptions =
    signal<
      Partial<
        Record<
          StockExcelFilterColumn,
          readonly StockExcelFilterValue[]
        >
      >
    >({});

  readonly excelFilterLoadingColumns =
    signal<
      ReadonlySet<
        StockExcelFilterColumn
      >
    >(
      new Set<
        StockExcelFilterColumn
      >(),
    );

  readonly loading = signal(false);
  readonly errorMessage = signal('');

  readonly sourceOnline = signal(false);
  readonly sourceMessage = signal('');
  readonly refreshedAt =
    signal<string | null>(null);

  readonly hasPreviousPage =
    computed(() =>
      this.page() > 1,
    );

  readonly hasNextPage =
    computed(() =>
      this.page() <
      this.totalPages(),
    );

  readonly showingFrom =
    computed(() =>
      this.totalRecords() === 0
        ? 0
        : (
            this.page() - 1
          ) *
            this.pageSize() +
          1,
    );

  readonly showingTo =
    computed(() =>
      Math.min(
        this.page() *
          this.pageSize(),
        this.totalRecords(),
      ),
    );

  constructor() {
    this.loadStocks();
  }

  search(): void {
    this.page.set(1);
    this.loadStocks();
  }

  clearSearch(): void {
    this.searchControl.setValue('');
    this.page.set(1);
    this.loadStocks();
  }

  applyColumnFilter(
    filter: HTMLDetailsElement,
  ): void {
    filter.open = false;
    this.page.set(1);
    this.loadStocks();
  }

  clearColumnFilter(
    column:
      | 'material'
      | 'description'
      | 'supplier'
      | 'location'
      | 'current'
      | 'minimum'
      | 'unitPrice'
      | 'totalValue'
      | 'status'
      | 'updated',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'material':
        this.materialControl.setValue('');
        break;

      case 'description':
        this.descriptionControl.setValue('');
        break;

      case 'supplier':
        this.supplierControl.setValue('');
        break;

      case 'location':
        this.locationControl.setValue('');
        break;

      case 'current':
        this.minCurrentStockControl.setValue('');
        this.maxCurrentStockControl.setValue('');
        break;

      case 'minimum':
        this.minMinimumStockControl.setValue('');
        this.maxMinimumStockControl.setValue('');
        break;

      case 'unitPrice':
        this.minUnitPriceControl.setValue('');
        this.maxUnitPriceControl.setValue('');
        break;

      case 'totalValue':
        this.minTotalValueControl.setValue('');
        this.maxTotalValueControl.setValue('');
        break;

      case 'status':
        this.statusControl.setValue('');
        break;

      case 'updated':
        this.updatedFromControl.setValue('');
        this.updatedToControl.setValue('');
        break;
    }

    filter.open = false;
    this.page.set(1);
    this.loadStocks();
  }

  columnFilterActive(
    column:
      | 'material'
      | 'description'
      | 'supplier'
      | 'location'
      | 'current'
      | 'minimum'
      | 'unitPrice'
      | 'totalValue'
      | 'status'
      | 'updated',
  ): boolean {
    switch (column) {
      case 'material':
        return Boolean(
          this.materialControl.value.trim(),
        );

      case 'description':
        return Boolean(
          this.descriptionControl.value.trim(),
        );

      case 'supplier':
        return Boolean(
          this.supplierControl.value.trim(),
        );

      case 'location':
        return Boolean(
          this.locationControl.value.trim(),
        );

      case 'current':
        return Boolean(
          this.minCurrentStockControl.value.trim() ||
          this.maxCurrentStockControl.value.trim(),
        );

      case 'minimum':
        return Boolean(
          this.minMinimumStockControl.value.trim() ||
          this.maxMinimumStockControl.value.trim(),
        );

      case 'unitPrice':
        return Boolean(
          this.minUnitPriceControl.value.trim() ||
          this.maxUnitPriceControl.value.trim(),
        );

      case 'totalValue':
        return Boolean(
          this.minTotalValueControl.value.trim() ||
          this.maxTotalValueControl.value.trim(),
        );

      case 'status':
        return Boolean(
          this.statusControl.value,
        );

      case 'updated':
        return Boolean(
          this.updatedFromControl.value ||
          this.updatedToControl.value,
        );
    }

    return false;
  }

  excelFilterOptionsFor(
    column: StockExcelFilterColumn,
  ): readonly StockExcelFilterValue[] {
    return (
      this.excelFilterOptions()[
        column
      ] ?? []
    );
  }

  excelFilterSelectedValues(
    column: StockExcelFilterColumn,
  ): readonly string[] {
    return (
      this.excelFilterSelections()[
        column
      ] ?? []
    );
  }

  excelFilterActive(
    column: StockExcelFilterColumn,
  ): boolean {
    return Object.prototype
      .hasOwnProperty.call(
        this.excelFilterSelections(),
        column,
      );
  }

  excelFilterLoading(
    column: StockExcelFilterColumn,
  ): boolean {
    return this
      .excelFilterLoadingColumns()
      .has(
        column,
      );
  }

  excelSortDirection(
    column: StockExcelFilterColumn,
  ): StockSortDirection | null {
    return this.sortColumn() ===
      column
      ? this.sortDirection()
      : null;
  }

  loadExcelFilterValues(
    column: StockExcelFilterColumn,
  ): void {
    if (
      this.excelFilterLoading(
        column,
      )
    ) {
      return;
    }

    this.excelFilterLoadingColumns
      .update(
        columns => {
          const updated =
            new Set(
              columns,
            );

          updated.add(
            column,
          );

          return updated;
        },
      );

    this.stockApi
      .getExcelFilterValues(
        this.buildStockQuery(
          false,
        ),
        column,
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(
          () =>
            this.excelFilterLoadingColumns
              .update(
                columns => {
                  const updated =
                    new Set(
                      columns,
                    );

                  updated.delete(
                    column,
                  );

                  return updated;
                },
              ),
        ),
      )
      .subscribe({
        next: options => {
          this.excelFilterOptions
            .update(
              current => ({
                ...current,
                [column]:
                  options,
              }),
            );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
            ),
          );
        },
      });
  }

  applyExcelSort(
    column: StockExcelFilterColumn,
    direction: StockSortDirection,
  ): void {
    if (this.loading()) {
      return;
    }

    this.sortColumn.set(
      column,
    );

    this.sortDirection.set(
      direction,
    );

    this.page.set(1);
    this.loadStocks();
  }

  applyExcelFilter(
    column: StockExcelFilterColumn,
    values: readonly string[],
  ): void {
    const availableValues =
      this.excelFilterOptions()[
        column
      ] ?? [];

    const selectedSet =
      new Set(
        values,
      );

    const selectsEverything =
      availableValues.length >
        0 &&
      availableValues.length ===
        selectedSet.size &&
      availableValues.every(
        option =>
          selectedSet.has(
            option.value,
          ),
      );

    this.excelFilterSelections
      .update(
        current => {
          const updated = {
            ...current,
          };

          if (selectsEverything) {
            delete updated[
              column
            ];
          } else {
            updated[column] =
              [...values];
          }

          return updated;
        },
      );

    this.page.set(1);
    this.loadStocks();
  }

  clearExcelFilter(
    column: StockExcelFilterColumn,
  ): void {
    this.excelFilterSelections
      .update(
        current => {
          const updated = {
            ...current,
          };

          delete updated[
            column
          ];

          return updated;
        },
      );

    this.page.set(1);
    this.loadStocks();
  }

  refresh(): void {
    this.loadStocks(true);
  }

  changePageSize(
    event: Event,
  ): void {
    const value =
      Number(
        (
          event.target as
            HTMLSelectElement
        ).value,
      );

    if (
      ![
        10,
        25,
        50,
        100,
      ].includes(value)
    ) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.loadStocks();
  }

  previousPage(): void {
    if (
      !this.hasPreviousPage() ||
      this.loading()
    ) {
      return;
    }

    this.page.update(
      current =>
        current - 1,
    );

    this.loadStocks();
  }

  nextPage(): void {
    if (
      !this.hasNextPage() ||
      this.loading()
    ) {
      return;
    }

    this.page.update(
      current =>
        current + 1,
    );

    this.loadStocks();
  }

  private buildStockQuery(
    includePaging: boolean,
    forceRefresh = false,
  ) {
    return {
      search:
        this.searchControl.value,

      material:
        this.materialControl.value,

      description:
        this.descriptionControl.value,

      supplier:
        this.supplierControl.value,

      location:
        this.locationControl.value,

      minCurrentStock:
        this.minCurrentStockControl.value,

      maxCurrentStock:
        this.maxCurrentStockControl.value,

      minMinimumStock:
        this.minMinimumStockControl.value,

      maxMinimumStock:
        this.maxMinimumStockControl.value,

      minUnitPrice:
        this.minUnitPriceControl.value,

      maxUnitPrice:
        this.maxUnitPriceControl.value,

      minTotalValue:
        this.minTotalValueControl.value,

      maxTotalValue:
        this.maxTotalValueControl.value,

      status:
        this.statusControl.value,

      updatedFrom:
        this.updatedFromControl.value,

      updatedTo:
        this.updatedToControl.value,

      sortColumn:
        this.sortColumn() ??
        undefined,

      sortDirection:
        this.sortDirection() ??
        undefined,

      excelFilters:
        this.serializeExcelFilters(),

      page:
        includePaging
          ? this.page()
          : 1,

      pageSize:
        this.pageSize(),

      refresh:
        forceRefresh,
    };
  }

  private serializeExcelFilters():
    string | undefined {
    const selections =
      this.excelFilterSelections();

    return Object.keys(
      selections,
    ).length > 0
      ? JSON.stringify(
          selections,
        )
      : undefined;
  }

  private loadStocks(
    forceRefresh = false,
  ): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.stockApi
      .getStocks(
        this.buildStockQuery(
          true,
          forceRefresh,
        ),
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() => {
          this.loading.set(false);
        }),
      )
      .subscribe({
        next: response => {
          this.items.set(
            response.items,
          );

          this.summary.set(
            response.summary,
          );

          this.page.set(
            response.page,
          );

          this.pageSize.set(
            response.pageSize,
          );

          this.totalRecords.set(
            response.totalRecords,
          );

          this.totalPages.set(
            response.totalPages,
          );

          this.sourceOnline.set(
            response.sourceOnline,
          );

          this.sourceMessage.set(
            response.message,
          );

          this.refreshedAt.set(
            response.refreshedAt,
          );
        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.items.set([]);
          this.totalRecords.set(0);
          this.totalPages.set(1);
          this.sourceOnline.set(false);

          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  private describeError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Please sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to view stock.';
    }

    const apiMessage =
      error.error?.error ??
      error.error?.message;

    return typeof apiMessage ===
      'string'
      ? apiMessage
      : 'Stock Maintenance data could not be loaded.';
  }
}
