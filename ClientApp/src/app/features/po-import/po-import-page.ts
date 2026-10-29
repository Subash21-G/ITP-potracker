import { NumericLinePipe } from '../../shared/numeric-line.pipe';
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
  PoBatchImportPreview,
  PoBatchImportResult,
  PoImportFilePreview,
  PoImportSaveResponse,
} from '../../core/models/po-import-api.model';
import {
  PoImportApiService,
} from '../../core/services/po-import-api.service';

type ImportMode =
  | 'single'
  | 'batch';

@Component({
  selector: 'app-po-import-page',
  imports: [NumericLinePipe, 
    DatePipe,
    DecimalPipe,
    RouterLink,
  ],
  templateUrl: './po-import-page.html',
  styleUrl: './po-import-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class PoImportPage {
  private readonly importApi =
    inject(PoImportApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly mode =
    signal<ImportMode>('single');

  readonly selectedFiles =
    signal<File[]>([]);

  readonly singlePreview =
    signal<PoImportFilePreview | null>(
      null,
    );

  readonly batchPreview =
    signal<PoBatchImportPreview | null>(
      null,
    );

  readonly singleResult =
    signal<PoImportSaveResponse | null>(
      null,
    );

  readonly batchResult =
    signal<PoBatchImportResult | null>(
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
          !file.isDuplicateInBatch)
        .length ?? 0,
    );

  readonly selectedFileNames =
    computed(() =>
      this.selectedFiles()
        .map((file) => file.name)
        .join(', '),
    );

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

    this.errorMessage.set('');
    this.successMessage.set('');
    this.singlePreview.set(null);
    this.batchPreview.set(null);
    this.singleResult.set(null);
    this.batchResult.set(null);

    if (files.length === 0) {
      this.selectedFiles.set([]);
      return;
    }

    const invalidFile =
      files.find((file) =>
        !file.name
          .toLowerCase()
          .endsWith('.pdf'),
      );

    if (invalidFile) {
      this.selectedFiles.set([]);

      this.errorMessage.set(
        `${invalidFile.name} is not a PDF file.`,
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
      files.length > 100
    ) {
      this.selectedFiles.set([]);

      this.errorMessage.set(
        'You can select a maximum of 100 PDF files.',
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
        'Select at least one PO PDF file.',
      );

      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.singlePreview.set(null);
    this.batchPreview.set(null);
    this.singleResult.set(null);
    this.batchResult.set(null);

    if (this.mode() === 'single') {
      this.importApi
        .previewSingle(files[0])
        .pipe(
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
              'PO PDF extracted successfully. Review the details before saving.',
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

    this.importApi
      .previewBatch(files)
      .pipe(
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
            'Batch extraction completed. Review the valid files before saving.',
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
    const purchaseOrder =
      this.singlePreview()
        ?.purchaseOrder;

    if (!purchaseOrder) {
      this.errorMessage.set(
        'No valid PO preview is available.',
      );

      return;
    }

    this.saving.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.singleResult.set(null);

    this.importApi
      .saveSingle(purchaseOrder)
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

          this.successMessage.set(
            `${result.message} ${result.materialRowsSaved} material row(s) saved.`,
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
        'Select at least one valid PO file.',
      );

      return;
    }

    this.saving.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
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

          this.successMessage.set(
            `Batch completed. ${result.importedCount} imported and ${result.updatedCount} updated.`,
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
                      !file.isDuplicateInBatch
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
                !file.isDuplicateInBatch,
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

  resetImport(): void {
    this.selectedFiles.set([]);
    this.singlePreview.set(null);
    this.batchPreview.set(null);
    this.singleResult.set(null);
    this.batchResult.set(null);
    this.errorMessage.set('');
    this.successMessage.set('');
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
      return 'Your account cannot import purchase orders.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : 'The PO PDF could not be processed.';
  }
}
