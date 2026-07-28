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
  StockListItem,
  StockSummary,
} from '../../core/models/stock-api.model';
import {
  StockApiService,
} from '../../core/services/stock-api.service';

@Component({
  selector: 'app-stocks-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
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

  private loadStocks(
    forceRefresh = false,
  ): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.stockApi
      .getStocks({
        search:
          this.searchControl.value,
        page:
          this.page(),
        pageSize:
          this.pageSize(),
        refresh:
          forceRefresh,
      })
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
