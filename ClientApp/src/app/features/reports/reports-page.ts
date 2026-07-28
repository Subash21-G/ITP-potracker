import {
  CurrencyPipe,
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
  FormsModule,
} from '@angular/forms';
import {
  RouterLink,
} from '@angular/router';
import {
  finalize,
} from 'rxjs';

import {
  MaterialReportFilter,
  MaterialReportResponse,
  ReportOptions,
} from '../../core/models/report-api.model';
import {
  ReportApiService,
} from '../../core/services/report-api.service';
import {
  ToastService,
} from '../../core/services/toast.service';

const EMPTY_OPTIONS: ReportOptions = {
  purchasers: [],
  processStatuses: [],
  dispatchStatuses: [],
};

const EMPTY_REPORT: MaterialReportResponse = {
  summary: {
    totalRecords: 0,
    totalQty: 0,
    totalDispatchQty: 0,
    totalPendingQty: 0,
    totalCost: 0,
  },
  items: [],
};

type DownloadType =
  | 'excel'
  | 'pdf'
  | null;

@Component({
  selector: 'app-reports-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterLink,
  ],
  templateUrl:
    './reports-page.html',
  styleUrl:
    './reports-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class ReportsPage {
  private readonly api =
    inject(ReportApiService);

  private readonly toastService =
    inject(ToastService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly options =
    signal<ReportOptions>(
      EMPTY_OPTIONS,
    );

  readonly report =
    signal<MaterialReportResponse>(
      EMPTY_REPORT,
    );

  readonly loadingOptions =
    signal(false);

  readonly loadingPreview =
    signal(false);

  readonly downloadType =
    signal<DownloadType>(null);

  readonly hasPreview =
    signal(false);

  readonly errorMessage =
    signal('');

  fromDate = '';

  toDate = '';

  processStatus = '';

  dispatchStatus = '';

  purchaserId:
    number | null =
      null;

  constructor() {
    this.loadOptions();
  }

  preview(): void {
    if (!this.validateDates()) {
      return;
    }

    this.loadingPreview.set(true);
    this.errorMessage.set('');

    this.api
      .preview(
        this.createFilter(),
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.loadingPreview.set(false)),
      )
      .subscribe({
        next: response => {
          this.report.set(response);
          this.hasPreview.set(true);

          this.toastService.success(
            'Report generated',
            `${response.summary.totalRecords} material records loaded.`,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.report.set(
            EMPTY_REPORT,
          );

          this.hasPreview.set(false);

          this.errorMessage.set(
            this.describeError(
              error,
              'The report preview could not be generated.',
            ),
          );
        },
      });
  }

  downloadExcel(): void {
    if (!this.validateDates()) {
      return;
    }

    this.downloadType.set(
      'excel',
    );

    this.errorMessage.set('');

    this.api
      .downloadExcel(
        this.createFilter(),
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.downloadType.set(null)),
      )
      .subscribe({
        next: blob => {
          this.saveBlob(
            blob,
            'Materials_Report.xlsx',
          );

          this.toastService.success(
            'Excel exported',
            'The materials report was downloaded successfully.',
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          const message =
            this.describeError(
              error,
              'The Excel report could not be downloaded.',
            );

          this.errorMessage.set(
            message,
          );

          this.toastService.error(
            'Excel export failed',
            message,
          );
        },
      });
  }

  downloadPdf(): void {
    if (!this.validateDates()) {
      return;
    }

    this.downloadType.set(
      'pdf',
    );

    this.errorMessage.set('');

    this.api
      .downloadPdf(
        this.createFilter(),
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.downloadType.set(null)),
      )
      .subscribe({
        next: blob => {
          this.saveBlob(
            blob,
            'Materials_Report.pdf',
          );

          this.toastService.success(
            'PDF exported',
            'The materials report was downloaded successfully.',
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          const message =
            this.describeError(
              error,
              'The PDF report could not be downloaded.',
            );

          this.errorMessage.set(
            message,
          );

          this.toastService.error(
            'PDF export failed',
            message,
          );
        },
      });
  }

  clearFilters(): void {
    this.fromDate = '';
    this.toDate = '';
    this.processStatus = '';
    this.dispatchStatus = '';
    this.purchaserId = null;

    this.report.set(
      EMPTY_REPORT,
    );

    this.hasPreview.set(false);
    this.errorMessage.set('');
  }

  private loadOptions(): void {
    this.loadingOptions.set(true);
    this.errorMessage.set('');

    this.api
      .getOptions()
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.loadingOptions.set(false)),
      )
      .subscribe({
        next: options =>
          this.options.set(options),

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
              'Report filter options could not be loaded.',
            ),
          );
        },
      });
  }

  private createFilter():
    MaterialReportFilter {
    return {
      fromDate:
        this.fromDate,

      toDate:
        this.toDate,

      processStatus:
        this.processStatus,

      dispatchStatus:
        this.dispatchStatus,

      purchaserId:
        this.purchaserId,
    };
  }

  private validateDates(): boolean {
    if (
      this.fromDate &&
      this.toDate &&
      this.fromDate >
        this.toDate
    ) {
      const message =
        'From Delivery Date cannot be later than To Delivery Date.';

      this.errorMessage.set(
        message,
      );

      this.toastService.warning(
        'Invalid date range',
        message,
      );

      return false;
    }

    return true;
  }

  private saveBlob(
    blob: Blob,
    filename: string,
  ): void {
    const objectUrl =
      URL.createObjectURL(blob);

    const anchor =
      document.createElement('a');

    anchor.href =
      objectUrl;

    anchor.download =
      filename;

    document.body.appendChild(
      anchor,
    );

    anchor.click();
    anchor.remove();

    URL.revokeObjectURL(
      objectUrl,
    );
  }

  private describeError(
    error: HttpErrorResponse,
    fallback: string,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Only Admin users can access Reports.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : fallback;
  }
}
