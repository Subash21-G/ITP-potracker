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
  RouterLink,
} from '@angular/router';
import {
  finalize,
} from 'rxjs';

import {
  AnalyticsResponse,
  AnalyticsSummary,
  PurchaserAnalyticsRow,
} from '../../core/models/insights-api.model';
import {
  InsightsApiService,
} from '../../core/services/insights-api.service';

@Component({
  selector: 'app-analytics-page',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
  ],
  templateUrl: './analytics-page.html',
  styleUrl: '../insights-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class AnalyticsPage {
  private readonly api =
    inject(InsightsApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly summary =
    signal<AnalyticsSummary>({
      purchaserCount: 0,
      totalMaterials: 0,
      overdueMaterials: 0,
      purchaseOrderCount: 0,
    });

  readonly purchasers =
    signal<
      readonly PurchaserAnalyticsRow[]
    >([]);

  readonly generatedAt =
    signal<string | null>(null);

  readonly loading = signal(false);
  readonly errorMessage = signal('');

  readonly topPurchasers =
    computed(() =>
      [...this.purchasers()]
        .sort(
          (left, right) =>
            right.materialCount -
            left.materialCount,
        )
        .slice(
          0,
          5,
        ),
    );

  readonly maximumMaterialCount =
    computed(() =>
      Math.max(
        1,
        ...this.topPurchasers().map(
          purchaser =>
            purchaser.materialCount,
        ),
      ),
    );

  constructor() {
    this.load();
  }

  refresh(): void {
    this.load();
  }

  loadPercentage(
    materialCount: number,
  ): number {
    return Math.max(
      4,
      Math.min(
        100,
        materialCount /
          this.maximumMaterialCount() *
          100,
      ),
    );
  }

  private load(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.api
      .getAnalytics()
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() =>
          this.loading.set(false),
        ),
      )
      .subscribe({
        next: (
          response: AnalyticsResponse,
        ) => {
          this.summary.set(
            response.summary,
          );

          this.purchasers.set(
            response.purchasers,
          );

          this.generatedAt.set(
            response.generatedAt,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.purchasers.set([]);
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
      return 'You do not have permission to view analytics.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message ===
      'string'
      ? message
      : 'Purchaser analytics could not be loaded.';
  }
}