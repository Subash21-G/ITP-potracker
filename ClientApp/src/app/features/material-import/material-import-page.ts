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
  MaterialImportPreview,
} from '../../core/models/material-import-api.model';
import {
  PoImportFilePreview,
  PoImportSaveResponse,
} from '../../core/models/po-import-api.model';
import {
  MaterialImportApiService,
} from '../../core/services/material-import-api.service';
import {
  PoImportApiService,
} from '../../core/services/po-import-api.service';

type MaterialImportMode =
  | 'excel'
  | 'pdf';

@Component({
  selector: 'app-material-import-page',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
  ],
  templateUrl:
    './material-import-page.html',
  styleUrl:
    './material-import-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class MaterialImportPage {
  private readonly materialImportApi =
    inject(MaterialImportApiService);

  private readonly poImportApi =
    inject(PoImportApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly mode =
    signal<MaterialImportMode>('excel');

  readonly selectedFile =
    signal<File | null>(null);

  readonly preview =
    signal<MaterialImportPreview | null>(
      null,
    );

  readonly pdfPreview =
    signal<PoImportFilePreview | null>(
      null,
    );

  readonly pdfResult =
    signal<PoImportSaveResponse | null>(
      null,
    );

  readonly loading =
    signal(false);

  readonly committing =
    signal(false);

  readonly cancelling =
    signal(false);

  readonly completed =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly successMessage =
    signal('');

  changeMode(
    mode: MaterialImportMode,
  ): void {
    if (
      this.loading() ||
      this.committing() ||
      this.cancelling()
    ) {
      return;
    }

    this.mode.set(mode);
    this.resetPage();
  }

  onFileSelected(
    event: Event,
  ): void {
    const input =
      event.target as HTMLInputElement;

    const file =
      input.files?.[0] ?? null;

    this.preview.set(null);
    this.pdfPreview.set(null);
    this.pdfResult.set(null);
    this.completed.set(false);
    this.clearMessages();

    if (!file) {
      this.selectedFile.set(null);
      return;
    }

    const lowerName =
      file.name.toLowerCase();

    if (
      this.mode() === 'excel' &&
      !lowerName.endsWith('.xlsx') &&
      !lowerName.endsWith('.xlsm')
    ) {
      this.selectedFile.set(null);

      this.errorMessage.set(
        'Only XLSX and XLSM Excel files are supported.',
      );

      input.value = '';
      return;
    }

    if (
      this.mode() === 'pdf' &&
      !lowerName.endsWith('.pdf')
    ) {
      this.selectedFile.set(null);

      this.errorMessage.set(
        'Only PDF purchase-order files are supported.',
      );

      input.value = '';
      return;
    }

    const maximumFileSize =
      20 * 1024 * 1024;

    if (
      file.size >
      maximumFileSize
    ) {
      this.selectedFile.set(null);

      this.errorMessage.set(
        'The selected file exceeds the 20 MB file limit.',
      );

      input.value = '';
      return;
    }

    this.selectedFile.set(file);
  }

  extractPreview(): void {
    const file =
      this.selectedFile();

    if (!file) {
      this.errorMessage.set(
        this.mode() === 'pdf'
          ? 'Select a purchase-order PDF file.'
          : 'Select a material Excel file.',
      );

      return;
    }

    if (this.mode() === 'pdf') {
      this.extractPdfPreview(file);
      return;
    }

    this.extractExcelPreview(file);
  }

  commitImport(): void {
    if (this.mode() === 'pdf') {
      this.commitPdfImport();
      return;
    }

    this.commitExcelImport();
  }

  cancelPreview(): void {
    if (this.mode() === 'pdf') {
      this.resetPage();

      this.successMessage.set(
        'PDF preview cleared.',
      );

      return;
    }

    const preview =
      this.preview();

    if (!preview) {
      this.resetPage();
      return;
    }

    this.cancelling.set(true);
    this.clearMessages();

    this.materialImportApi
      .cancel(preview.batchId)
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.cancelling.set(false)),
      )
      .subscribe({
        next: (result) => {
          this.selectedFile.set(null);
          this.preview.set(null);
          this.completed.set(false);

          this.successMessage.set(
            result.message,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
              'The material preview could not be cancelled.',
            ),
          );
        },
      });
  }

  resetPage(): void {
    this.selectedFile.set(null);
    this.preview.set(null);
    this.pdfPreview.set(null);
    this.pdfResult.set(null);
    this.completed.set(false);
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

  private extractExcelPreview(
    file: File,
  ): void {
    this.loading.set(true);
    this.preview.set(null);
    this.pdfPreview.set(null);
    this.pdfResult.set(null);
    this.completed.set(false);
    this.clearMessages();

    this.materialImportApi
      .preview(file)
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.loading.set(false)),
      )
      .subscribe({
        next: (preview) => {
          this.preview.set(preview);

          if (
            preview.invalidRows > 0
          ) {
            this.errorMessage.set(
              `${preview.invalidRows} invalid row(s) were found. Correct the Excel file and upload it again before importing.`,
            );

            return;
          }

          this.successMessage.set(
            'Excel validation completed successfully. Review the rows before importing.',
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
              'The material Excel file could not be processed.',
            ),
          );
        },
      });
  }

  private extractPdfPreview(
    file: File,
  ): void {
    this.loading.set(true);
    this.preview.set(null);
    this.pdfPreview.set(null);
    this.pdfResult.set(null);
    this.completed.set(false);
    this.clearMessages();

    this.poImportApi
      .previewSingle(file)
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.loading.set(false)),
      )
      .subscribe({
        next: (preview) => {
          if (
            !preview.isSuccessful ||
            !preview.purchaseOrder
          ) {
            this.errorMessage.set(
              preview.errorMessage ||
              'No purchase-order material rows could be extracted from the PDF.',
            );

            return;
          }

          this.pdfPreview.set(preview);

          this.successMessage.set(
            'PDF extraction completed successfully. Review the purchase order and material rows before importing.',
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
              'The purchase-order PDF could not be processed.',
            ),
          );
        },
      });
  }

  private commitExcelImport(): void {
    const preview =
      this.preview();

    if (!preview) {
      this.errorMessage.set(
        'No material import preview is available.',
      );

      return;
    }

    if (
      preview.invalidRows > 0
    ) {
      this.errorMessage.set(
        'Invalid rows must be corrected before importing.',
      );

      return;
    }

    this.committing.set(true);
    this.clearMessages();

    this.materialImportApi
      .commit(preview.batchId)
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.committing.set(false)),
      )
      .subscribe({
        next: (result) => {
          this.completed.set(true);

          this.successMessage.set(
            result.message,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
              'The material Excel import could not be completed.',
            ),
          );
        },
      });
  }

  private commitPdfImport(): void {
    const purchaseOrder =
      this.pdfPreview()
        ?.purchaseOrder;

    if (!purchaseOrder) {
      this.errorMessage.set(
        'No valid PDF material preview is available.',
      );

      return;
    }

    this.committing.set(true);
    this.pdfResult.set(null);
    this.clearMessages();

    this.poImportApi
      .saveSingle(purchaseOrder)
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.committing.set(false)),
      )
      .subscribe({
        next: (result) => {
          this.pdfResult.set(result);
          this.completed.set(true);

          this.successMessage.set(
            `${result.message} ${result.materialRowsSaved} material row(s) saved.`,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
              'The PDF material import could not be completed.',
            ),
          );
        },
      });
  }

  private clearMessages(): void {
    this.errorMessage.set('');
    this.successMessage.set('');
  }

  private describeError(
    error: HttpErrorResponse,
    fallbackMessage: string,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Your account cannot import materials.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : fallbackMessage;
  }
}