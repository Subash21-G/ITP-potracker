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
import {
  takeUntilDestroyed,
} from '@angular/core/rxjs-interop';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import {
  ProcessQueueResponse,
} from '../../core/models/production-api.model';
import {
  ProductionApiService,
} from '../../core/services/production-api.service';

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
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './process-queue-page.html',
  styleUrls: [
    '../invoices/invoices-page.css',
    './process-queue-page.css',
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProcessQueuePage {
  private readonly productionApi =
    inject(ProductionApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly filters = new FormGroup({
    search: new FormControl(
      '',
      {
        nonNullable: true,
      },
    ),
  });

  readonly queue =
    signal<ProcessQueueResponse>(EMPTY_QUEUE);

  readonly page = signal(1);
  readonly pageSize = signal(50);
  readonly loading = signal(false);
  readonly errorMessage = signal('');

  readonly hasPreviousPage =
    computed(() => this.page() > 1);

  readonly hasNextPage =
    computed(() =>
      this.page() <
      this.queue().totalPages);

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

    this.page.set(1);
    this.loadQueue();
  }

  changePageSize(event: Event): void {
    const value = Number(
      (event.target as HTMLSelectElement)
        .value,
    );

    if (
      ![10, 25, 50, 100, 200]
        .includes(value)
    ) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.loadQueue();
  }

  previousPage(): void {
    if (
      !this.hasPreviousPage() ||
      this.loading()
    ) {
      return;
    }

    this.page.update(
      (current) => current - 1,
    );

    this.loadQueue();
  }

  nextPage(): void {
    if (
      !this.hasNextPage() ||
      this.loading()
    ) {
      return;
    }

    this.page.update(
      (current) => current + 1,
    );

    this.loadQueue();
  }

  retry(): void {
    this.loadQueue();
  }

  private loadQueue(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    const filters =
      this.filters.getRawValue();

    this.productionApi
      .getProcessQueue({
        search: filters.search,
        page: this.page(),
        pageSize: this.pageSize(),
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),

        finalize(() =>
          this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.queue.set(response);
          this.page.set(response.page);
          this.pageSize.set(
            response.pageSize,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.queue.set(EMPTY_QUEUE);

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
      return 'Your session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Your account cannot view the process queue.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : 'The process queue could not be loaded.';
  }
}
