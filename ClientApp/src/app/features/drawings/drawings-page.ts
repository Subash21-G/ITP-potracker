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
  RouterLink,
} from '@angular/router';
import {
  finalize,
} from 'rxjs';

import {
  ManageOnlyDirective,
} from '../../core/directives/manage-only.directive';
import {
  DrawingListItem,
  DrawingSummary,
} from '../../core/models/insights-api.model';
import {
  InsightsApiService,
} from '../../core/services/insights-api.service';

@Component({
  selector: 'app-drawings-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ManageOnlyDirective,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './drawings-page.html',
  styleUrl: '../insights-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class DrawingsPage {
  private readonly api =
    inject(InsightsApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly searchControl =
    new FormControl(
      '',
      {
        nonNullable: true,
      },
    );

  readonly statusControl =
    new FormControl(
      '',
      {
        nonNullable: true,
      },
    );

  readonly items =
    signal<readonly DrawingListItem[]>([]);

  readonly summary =
    signal<DrawingSummary>({
      totalMaterials: 0,
      missingDrawings: 0,
      updatedThisWeek: 0,
      withVersion: 0,
    });

  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly totalRecords = signal(0);
  readonly totalPages = signal(1);
  readonly loading = signal(false);
  readonly errorMessage = signal('');

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
    this.load();
  }

  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  clearFilters(): void {
    this.searchControl.setValue('');
    this.statusControl.setValue('');
    this.page.set(1);
    this.load();
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
    this.load();
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

    this.load();
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

    this.load();
  }

  retry(): void {
    this.load();
  }

  private load(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.api
      .getDrawings({
        search:
          this.searchControl.value,
        status:
          this.statusControl.value,
        page:
          this.page(),
        pageSize:
          this.pageSize(),
      })
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() =>
          this.loading.set(false),
        ),
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
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.items.set([]);
          this.totalRecords.set(0);
          this.totalPages.set(1);
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
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to view drawings.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message ===
      'string'
      ? message
      : 'Drawing records could not be loaded.';
  }
}