import { NumericLinePipe } from '../../shared/numeric-line.pipe';
import {
  CommonModule,
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
  linkedSignal,
  signal,
} from '@angular/core';

import {
  takeUntilDestroyed,
} from '@angular/core/rxjs-interop';

import {
  RouterLink,
} from '@angular/router';

import {
  EMPTY,
  Subject,
  catchError,
  defer,
  finalize,
  switchMap,
  tap,
} from 'rxjs';

import {
  DashboardResponse,
} from '../../core/models/dashboard.model';

import {
  DashboardApiService,
} from '../../core/services/dashboard-api.service';

@Component({
  selector: 'app-dashboard-page',
  imports: [NumericLinePipe, 
    CommonModule,
    RouterLink,
  ],
  templateUrl:
    './dashboard-page.html',
  styleUrl:
    './dashboard-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  private readonly dashboardApi =
    inject(DashboardApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  private readonly requestDashboard =
    new Subject<void>();

  readonly dashboard =
    signal<DashboardResponse | null>(
      null,
    );

  readonly isLoading =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly recentPageSize = signal(10);
  readonly recentPageSizes = [5, 10, 25, 50];
  readonly recentTotalPages = computed(() => Math.max(1, Math.ceil((this.dashboard()?.recentMaterials.length ?? 0) / this.recentPageSize())));
  readonly recentPage = linkedSignal<number, number>({
    source: this.recentTotalPages,
    computation: (total, previous) => Math.min(previous?.value ?? 1, total),
  });
  readonly pagedRecentMaterials = computed(() => {
    const rows = this.dashboard()?.recentMaterials ?? [];
    const start = (this.recentPage() - 1) * this.recentPageSize();
    return rows.slice(start, start + this.recentPageSize());
  });

  constructor() {
    this.requestDashboard
      .pipe(
        switchMap(() =>
          defer(() => {
            this.isLoading.set(
              true,
            );

            this.errorMessage.set(
              '',
            );

            return this.dashboardApi
              .getDashboard()
              .pipe(
                tap(
                  response =>
                    this.dashboard.set(
                      response,
                    ),
                ),

                catchError(
                  (
                    error:
                      HttpErrorResponse,
                  ) => {
                    this.errorMessage.set(
                      this.describeError(
                        error,
                      ),
                    );

                    return EMPTY;
                  },
                ),

                finalize(() =>
                  this.isLoading.set(
                    false,
                  ),
                ),
              );
          }),
        ),

        takeUntilDestroyed(
          this.destroyRef,
        ),
      )
      .subscribe();

    this.loadDashboard();
  }

  loadDashboard(): void {
    this.requestDashboard.next();
  }

  changeRecentPageSize(value: string): void {
    const size = Number(value);
    if (!this.recentPageSizes.includes(size)) return;
    this.recentPageSize.set(size);
    this.recentPage.set(1);
  }

  percentage(
    value: number,
    total: number,
  ): number {
    if (total <= 0) {
      return 0;
    }

    return Math.min(
      100,
      Math.max(
        0,
        Math.round(
          (
            value /
            total
          ) *
          100,
        ),
      ),
    );
  }

  processTotal(
    response: DashboardResponse,
  ): number {
    return (
      response.process.pending +
      response.process.inProgress +
      response.process.completed
    );
  }

  dispatchTotal(
    response: DashboardResponse,
  ): number {
    return (
      response.dispatch.pending +
      response.dispatch.partial +
      response.dispatch.completed
    );
  }

  invoiceTotal(
    response: DashboardResponse,
  ): number {
    return (
      response.invoice.notInvoiced +
      response.invoice.partiallyInvoiced +
      response.invoice.fullyInvoiced
    );
  }

  statusClass(
    status: string,
  ): string {
    return status
      .trim()
      .toLowerCase()
      .replace(
        /[^a-z0-9]+/g,
        '-',
      );
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
      return 'You do not have permission to view the dashboard.';
    }

    return 'Dashboard data could not be loaded. Please try again.';
  }
}
