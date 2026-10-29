import {
  DatePipe,
} from '@angular/common';
import {
  HttpErrorResponse,
} from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnDestroy,
  computed,
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
  Observable,
  catchError,
  finalize,
  of,
  switchMap,
  tap,
} from 'rxjs';

import {
  InvoiceBatchImportPreview,
  InvoiceBatchImportResult,
  InvoiceImportFilePreview,
  InvoiceImportSaveResponse,
} from '../../core/models/invoice-import-api.model';
import {
  InvoiceImportApiService,
} from '../../core/services/invoice-import-api.service';

type ImportMode =
  | 'single'
  | 'batch';

@Component({
  selector: 'app-invoice-import-page',
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
  ],
  templateUrl:
    './invoice-import-page.html',
  styleUrl:
    './invoice-import-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class InvoiceImportPage implements OnDestroy {
  private readonly importApi =
    inject(InvoiceImportApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  private readonly pendingPreviewPaths =
    new Set<string>();

  private readonly releasedPreviewPaths =
    new Set<string>();

  private cleanupInFlight =
    false;

  readonly mode =
    signal<ImportMode>('single');

  readonly selectedFiles =
    signal<File[]>([]);

  readonly singlePreview =
    signal<InvoiceImportFilePreview | null>(
      null,
    );

  readonly batchPreview =
    signal<InvoiceBatchImportPreview | null>(
      null,
    );

  readonly singleResult =
    signal<InvoiceImportSaveResponse | null>(
      null,
    );

  readonly batchResult =
    signal<InvoiceBatchImportResult | null>(
      null,
    );

  readonly loading =
    signal(false);

  readonly saving =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly successMessage =
    signal('');

  readonly selectedBatchCount =
    computed(() =>
      this.batchPreview()
        ?.files
        .filter((file) =>
          file.isSelected &&
          file.isSuccessful &&
          !file.isDuplicateInBatch &&
          !file.existsInDatabase)
        .length ?? 0,
    );

  readonly selectedFileNames =
    computed(() =>
      this.selectedFiles()
        .map((file) => file.name)
        .join(', '),
    );

  ngOnDestroy(): void {
    this.rememberCurrentPreviewPaths();

    this.discardPendingPreviewFiles(
      false,
      false,
    );
  }

  changeMode(
    mode: ImportMode,
  ): void {
    if (
      this.loading() ||
      this.saving()
    ) {
      return;
    }

    this.mode.set(mode);
    this.resetImport();
  }

  onFilesSelected(
    event: Event,
  ): void {
    const input =
      event.target as HTMLInputElement;

    const files =
      Array.from(input.files ?? []);

    this.rememberCurrentPreviewPaths();
    this.discardPendingPreviewFiles();
    this.clearMessages();
    this.singlePreview.set(null);
    this.batchPreview.set(null);
    this.singleResult.set(null);
    this.batchResult.set(null);

    if (files.length === 0) {
      this.selectedFiles.set([]);
      return;
    }

    const allowedExtensions =
      [
        '.pdf',
        '.xlsx',
        '.xls',
      ];

    const invalidFile =
      files.find((file) => {
        const lowerName =
          file.name.toLowerCase();

        return !allowedExtensions.some(
          (extension) =>
            lowerName.endsWith(extension),
        );
      });

    if (invalidFile) {
      this.selectedFiles.set([]);

      this.errorMessage.set(
        `${invalidFile.name} is not a supported invoice file.`,
      );

      input.value = '';
      return;
    }

    if (
      this.mode() === 'single' &&
      files.length > 1
    ) {
      this.selectedFiles.set([
        files[0],
      ]);

      return;
    }

    if (
      this.mode() === 'batch' &&
      files.length > 300
    ) {
      this.selectedFiles.set([]);

      this.errorMessage.set(
        'You can select a maximum of 300 invoice files.',
      );

      input.value = '';
      return;
    }

    const maximumSingleSize =
      20 * 1024 * 1024;

    const oversizedFile =
      files.find((file) =>
        file.size > maximumSingleSize,
      );

    if (oversizedFile) {
      this.selectedFiles.set([]);

      this.errorMessage.set(
        `${oversizedFile.name} exceeds the 20 MB file limit.`,
      );

      input.value = '';
      return;
    }

    const totalSize =
      files.reduce(
        (sum, file) =>
          sum + file.size,
        0,
      );

    const maximumBatchSize =
      200 * 1024 * 1024;

    if (
      this.mode() === 'batch' &&
      totalSize > maximumBatchSize
    ) {
      this.selectedFiles.set([]);

      this.errorMessage.set(
        'The combined upload size cannot exceed 200 MB.',
      );

      input.value = '';
      return;
    }

    this.selectedFiles.set(files);
  }

  preview(): void {
    const files =
      this.selectedFiles();

    if (files.length === 0) {
      this.errorMessage.set(
        'Select at least one invoice file.',
      );

      return;
    }

    this.rememberCurrentPreviewPaths();

    const cleanupRequest =
      this.cleanupPendingPreviewPathsForNextPreview();

    this.loading.set(true);
    this.clearMessages();
    this.singlePreview.set(null);
    this.batchPreview.set(null);
    this.singleResult.set(null);
    this.batchResult.set(null);

    if (this.mode() === 'single') {
      cleanupRequest
        .pipe(
          switchMap(() =>
            this.importApi.previewSingle(
              files[0],
            ),
          ),
          takeUntilDestroyed(
            this.destroyRef,
          ),

          finalize(() =>
            this.loading.set(false)),
        )
        .subscribe({
          next: (preview) => {
            this.singlePreview.set(
              preview,
            );

            this.successMessage.set(
              'Invoice data extracted successfully. Review the information before saving.',
            );
          },

          error: (
            error: HttpErrorResponse,
          ) => {
            this.errorMessage.set(
              this.describeError(error),
            );
          },
        });

      return;
    }

    cleanupRequest
      .pipe(
        switchMap(() =>
          this.importApi.previewBatch(
            files,
          ),
        ),
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.loading.set(false)),
      )
      .subscribe({
        next: (preview) => {
          this.batchPreview.set(
            preview,
          );

          this.successMessage.set(
            'Batch extraction completed. Review and select the valid invoices.',
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  saveSingle(): void {
    const invoice =
      this.singlePreview()
        ?.invoice;

    if (!invoice) {
      this.errorMessage.set(
        'No valid invoice preview is available.',
      );

      return;
    }

    invoice.invoiceNumber =
      invoice.invoiceNumber.trim();

    if (
      this.requiresInvoiceNumberReview(
        invoice.invoiceNumber,
      )
    ) {
      this.errorMessage.set(
        'Enter the correct invoice number before saving.',
      );

      return;
    }

    if (
      !invoice.items ||
      invoice.items.length === 0
    ) {
      this.errorMessage.set(
        'No invoice rows are available to save.',
      );

      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.singleResult.set(null);

    this.importApi
      .saveSingle(invoice)
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.saving.set(false)),
      )
      .subscribe({
        next: (result) => {
          this.singleResult.set(result);

          this.rememberCurrentPreviewPaths();
          this.discardPendingPreviewFiles();

          this.successMessage.set(
            result.message,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  saveBatch(): void {
    const preview =
      this.batchPreview();

    if (!preview) {
      this.errorMessage.set(
        'No batch preview is available.',
      );

      return;
    }

    if (
      this.selectedBatchCount() === 0
    ) {
      this.errorMessage.set(
        'Select at least one valid new invoice.',
      );

      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.batchResult.set(null);

    this.importApi
      .saveBatch(preview)
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.saving.set(false)),
      )
      .subscribe({
        next: (result) => {
          this.batchResult.set(result);

          this.rememberCurrentPreviewPaths();
          this.discardPendingPreviewFiles();

          this.successMessage.set(
            `Batch completed. ${result.importedCount} invoice(s) imported.`,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  toggleBatchFile(
    batchItemId: string,
    checked: boolean,
  ): void {
    this.batchPreview.update(
      (preview) => {
        if (!preview) {
          return preview;
        }

        return {
          ...preview,

          files: preview.files.map(
            (file) =>
              file.batchItemId ===
              batchItemId
                ? {
                    ...file,

                    isSelected:
                      file.isSuccessful &&
                      !file.isDuplicateInBatch &&
                      !file.existsInDatabase
                        ? checked
                        : false,
                  }
                : file,
          ),
        };
      },
    );
  }

  selectAllValid(): void {
    this.batchPreview.update(
      (preview) => {
        if (!preview) {
          return preview;
        }

        return {
          ...preview,

          files: preview.files.map(
            (file) => ({
              ...file,

              isSelected:
                file.isSuccessful &&
                !file.isDuplicateInBatch &&
                !file.existsInDatabase,
            }),
          ),
        };
      },
    );
  }

  clearSelection(): void {
    this.batchPreview.update(
      (preview) => {
        if (!preview) {
          return preview;
        }

        return {
          ...preview,

          files: preview.files.map(
            (file) => ({
              ...file,
              isSelected: false,
            }),
          ),
        };
      },
    );
  }

  removeSingleRow(
    rowIndex: number,
  ): void {
    this.singlePreview.update(
      (preview) => {
        if (!preview) {
          return preview;
        }

        return {
          ...preview,

          invoice: {
            ...preview.invoice,

            items:
              preview.invoice.items.filter(
                (_, index) =>
                  index !== rowIndex,
              ),
          },
        };
      },
    );
  }

  resetImport(): void {
    this.rememberCurrentPreviewPaths();
    this.discardPendingPreviewFiles(true);

    this.selectedFiles.set([]);
    this.singlePreview.set(null);
    this.batchPreview.set(null);
    this.singleResult.set(null);
    this.batchResult.set(null);
    this.clearMessages();
  }

  formatFileSize(
    bytes: number,
  ): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }

    if (
      bytes <
      1024 * 1024
    ) {
      return `${(
        bytes / 1024
      ).toFixed(1)} KB`;
    }

    return `${(
      bytes /
      (1024 * 1024)
    ).toFixed(2)} MB`;
  }

  dateInputValue(
    value: string | null,
  ): string {
    if (!value) {
      return '';
    }

    return value.slice(0, 10);
  }

  requiresInvoiceNumberReview(
    invoiceNumber: string,
  ): boolean {
    const value =
      invoiceNumber.trim();

    return (
      value.length === 0 ||
      value.toUpperCase() ===
        'NOT FOUND' ||
      value.toUpperCase() ===
        'UNKNOWN' ||
      value.toUpperCase().startsWith(
        'REVIEW-',
      )
    );
  }

  private currentPreviewFilePaths(): string[] {
    const paths =
      new Set<string>();

    const singlePath =
      this.singlePreview()
        ?.invoice
        .uploadedFilePath
        ?.trim();

    if (singlePath) {
      paths.add(singlePath);
    }

    for (
      const file
      of this.batchPreview()?.files ?? []
    ) {
      const batchPath =
        file.invoice
          ?.uploadedFilePath
          ?.trim();

      if (batchPath) {
        paths.add(batchPath);
      }
    }

    return Array.from(paths);
  }

  private rememberCurrentPreviewPaths(): void {
    for (
      const path
      of this.currentPreviewFilePaths()
    ) {
      if (
        !this.releasedPreviewPaths.has(
          path,
        )
      ) {
        this.pendingPreviewPaths.add(
          path,
        );
      }
    }
  }

  private cleanupPendingPreviewPathsForNextPreview(
  ): Observable<unknown> {
    const paths =
      Array.from(
        this.pendingPreviewPaths,
      );

    if (paths.length === 0) {
      return of(null);
    }

    return this.importApi
      .discardPreviews(paths)
      .pipe(
        tap(() => {
          this.releasePreviewPaths(
            paths,
          );
        }),

        catchError(() =>
          of(null)),
      );
  }

  private discardPendingPreviewFiles(
    reportFailure = false,
    updateLoading = true,
  ): void {
    if (this.cleanupInFlight) {
      return;
    }

    const paths =
      Array.from(
        this.pendingPreviewPaths,
      );

    if (paths.length === 0) {
      return;
    }

    this.cleanupInFlight =
      true;

    if (updateLoading) {
      this.loading.set(true);
    }

    this.importApi
      .discardPreviews(paths)
      .pipe(
        finalize(() => {
          this.cleanupInFlight =
            false;

          if (updateLoading) {
            this.loading.set(false);
          }
        }),
      )
      .subscribe({
        next: () => {
          this.releasePreviewPaths(
            paths,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          if (!reportFailure) {
            return;
          }

          this.errorMessage.set(
            'The preview was reset locally, but its ' +
              'temporary files could not be removed. ' +
              this.describeError(error),
          );
        },
      });
  }

  private releasePreviewPaths(
    paths: string[],
  ): void {
    for (const path of paths) {
      this.pendingPreviewPaths.delete(
        path,
      );

      this.releasedPreviewPaths.add(
        path,
      );
    }
  }

  private clearMessages(): void {
    this.errorMessage.set('');
    this.successMessage.set('');
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
      return 'Your account cannot import invoices.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : 'The invoice file could not be processed.';
  }
}

