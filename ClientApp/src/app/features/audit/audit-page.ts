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
  AuditItem,
} from '../../core/models/insights-api.model';
import {
  InsightsApiService,
} from '../../core/services/insights-api.service';

@Component({
  selector: 'app-audit-page',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
  ],
  templateUrl: './audit-page.html',
  styleUrl: '../insights-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class AuditPage {
  private readonly api =
    inject(InsightsApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly items =
    signal<readonly AuditItem[]>([]);

  readonly generatedAt =
    signal<string | null>(null);

  readonly loading = signal(false);
  readonly errorMessage = signal('');

  constructor() {
    this.load();
  }

  refresh(): void {
    this.load();
  }

  private load(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.api
      .getAudit(25)
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

          this.generatedAt.set(
            response.generatedAt,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.items.set([]);
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
      return 'You do not have permission to view the audit page.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message ===
      'string'
      ? message
      : 'Recent material activity could not be loaded.';
  }
}