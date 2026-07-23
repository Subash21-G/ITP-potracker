import {
  DatePipe,
  DecimalPipe,
} from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
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
  DispatchMaterialListItem,
  DispatchSummary,
} from '../../core/models/dispatch-api.model';
import {
  DispatchApiService,
} from '../../core/services/dispatch-api.service';

const EMPTY_SUMMARY: DispatchSummary = {
  totalPendingMaterials: 0,
  partialMaterials: 0,
  totalPOQuantity: 0,
  totalDispatchedQuantity: 0,
  totalPendingQuantity: 0,
};

@Component({
  selector: 'app-dispatches-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './dispatches-page.html',
  styleUrls: [
    '../invoices/invoices-page.css',
    './dispatches-page.css',
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DispatchesPage {
  private readonly dispatchApi =
    inject(DispatchApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly filters = new FormGroup({
    search: new FormControl(
      '',
      {
        nonNullable: true,
      },
    ),

    status: new FormControl(
      'All',
      {
        nonNullable: true,
      },
    ),
  });

  readonly items =
    signal<readonly DispatchMaterialListItem[]>([]);

  readonly summary =
    signal<DispatchSummary>(EMPTY_SUMMARY);

  readonly loading = signal(false);
  readonly errorMessage = signal('');

  constructor() {
    this.loadDispatches();
  }

  applyFilters(): void {
    this.loadDispatches();
  }

  clearFilters(): void {
    this.filters.reset({
      search: '',
      status: 'All',
    });

    this.loadDispatches();
  }

  retry(): void {
    this.loadDispatches();
  }

  private loadDispatches(): void {
    if (this.loading()) {
      return;
    }

    const filters =
      this.filters.getRawValue();

    this.loading.set(true);
    this.errorMessage.set('');

    this.dispatchApi
      .getDispatches({
        search: filters.search,
        status: filters.status,
        take: 1000,
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),

        finalize(() =>
          this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.items.set(response.items);
          this.summary.set(response.summary);
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.items.set([]);
          this.summary.set(EMPTY_SUMMARY);

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
      return 'Your account cannot view dispatch information.';
    }

    if (error.status === 404) {
      return 'The Dispatch API was not found. Rebuild the ASP.NET project.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : 'Dispatch materials could not be loaded.';
  }
}
