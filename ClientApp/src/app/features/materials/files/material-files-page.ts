import { NumericLinePipe } from '../../../shared/numeric-line.pipe';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { MaterialDetails } from '../../../core/models/material-crud.model';
import { MaterialCrudApiService } from '../../../core/services/material-crud-api.service';

@Component({
  selector: 'app-material-files-page',
  imports: [NumericLinePipe, CommonModule, FormsModule, RouterLink],
  templateUrl: './material-files-page.html',
  styleUrl: './material-files-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaterialFilesPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly materialApi = inject(MaterialCrudApiService);

  private readonly pdfInput =
    viewChild<ElementRef<HTMLInputElement>>('pdfInput');

  private readonly drawingInput =
    viewChild<ElementRef<HTMLInputElement>>('drawingInput');

  private readonly handSketchInput =
    viewChild<ElementRef<HTMLInputElement>>('handSketchInput');

  private readonly outsourceDrawingInput =
    viewChild<ElementRef<HTMLInputElement>>('outsourceDrawingInput');
  private readonly webDrawingInput =
    viewChild<ElementRef<HTMLInputElement>>('webDrawingInput');

  readonly materialId = signal(0);
  readonly material = signal<MaterialDetails | null>(null);
  readonly selectedPdf = signal<File | null>(null);
  readonly selectedDrawing = signal<File | null>(null);
  readonly selectedHandSketch = signal<File | null>(null);
  readonly selectedOutsourceDrawing = signal<File | null>(null);
  readonly selectedWebDrawing = signal<File | null>(null);
  readonly hasSelectedFiles = computed(
    () =>
      this.selectedPdf() !== null ||
      this.selectedDrawing() !== null ||
      this.selectedHandSketch() !== null ||
      this.selectedOutsourceDrawing() !== null ||
      this.selectedWebDrawing() !== null,
  );
  readonly isLoading = signal(true);
  readonly isUploading = signal(false);
  readonly isDeletingDrawing = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');

  ngOnInit(): void {
    const id = Number(
      this.route.snapshot.paramMap.get('id'),
    );

    if (!Number.isInteger(id) || id <= 0) {
      this.errorMessage.set('Invalid material number.');
      this.isLoading.set(false);
      return;
    }

    this.materialId.set(id);
    this.loadMaterial();
  }

  onPdfSelected(event: Event): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0) ?? null;

    if (!file) {
      this.selectedPdf.set(null);
      return;
    }

    if (this.extension(file.name) !== '.pdf') {
      this.selectedPdf.set(null);
      input.value = '';
      this.errorMessage.set(
        'Material PDF must be a .pdf file.',
      );
      return;
    }

    if (file.size > 50 * 1024 * 1024) {
      this.selectedPdf.set(null);
      input.value = '';
      this.errorMessage.set(
        'Material PDF size must not exceed 50 MB.',
      );
      return;
    }

    this.selectedPdf.set(file);
  }

  onDrawingSelected(event: Event): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0) ?? null;

    if (!file) {
      this.selectedDrawing.set(null);
      return;
    }

    const allowedExtensions = [
      '.pdf',
      '.dwg',
      '.dxf',
      '.step',
      '.stp',
      '.iges',
      '.igs',
      '.png',
      '.jpg',
      '.jpeg',
      '.webp',
      '.zip',
    ];

    if (!allowedExtensions.includes(this.extension(file.name))) {
      this.selectedDrawing.set(null);
      input.value = '';
      this.errorMessage.set(
        'Allowed drawing files: PDF, PNG, JPG, WebP, DWG, DXF, STEP, STP, IGES, IGS or ZIP.',
      );
      return;
    }

    if (file.size > 100 * 1024 * 1024) {
      this.selectedDrawing.set(null);
      input.value = '';
      this.errorMessage.set(
        'Drawing file size must not exceed 100 MB.',
      );
      return;
    }

    this.selectedDrawing.set(file);
  }

  onHandSketchSelected(event: Event): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0) ?? null;

    if (!file) {
      this.selectedHandSketch.set(null);
      return;
    }

    const allowedExtensions = [
      '.pdf',
      '.png',
      '.jpg',
      '.jpeg',
      '.webp',
      '.gif',
      '.bmp',
    ];

    if (!allowedExtensions.includes(this.extension(file.name))) {
      this.selectedHandSketch.set(null);
      input.value = '';
      this.errorMessage.set(
        'Allowed hand sketches: PDF, PNG, JPG, JPEG, WebP, GIF or BMP.',
      );
      return;
    }

    if (file.size > 100 * 1024 * 1024) {
      this.selectedHandSketch.set(null);
      input.value = '';
      this.errorMessage.set(
        'Hand sketch size must not exceed 100 MB.',
      );
      return;
    }

    this.selectedHandSketch.set(file);
  }

  onOutsourceDrawingSelected(event: Event): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0) ?? null;

    if (!file) {
      this.selectedOutsourceDrawing.set(null);
      return;
    }

    const allowedExtensions = [
      '.pdf',
      '.dwg',
      '.dxf',
      '.step',
      '.stp',
      '.iges',
      '.igs',
      '.png',
      '.jpg',
      '.jpeg',
      '.webp',
      '.zip',
    ];

    if (!allowedExtensions.includes(this.extension(file.name))) {
      this.selectedOutsourceDrawing.set(null);
      input.value = '';
      this.errorMessage.set(
        'Allowed outsourced drawings: PDF, PNG, JPG, WebP, DWG, DXF, STEP, STP, IGES, IGS or ZIP.',
      );
      return;
    }

    if (file.size > 100 * 1024 * 1024) {
      this.selectedOutsourceDrawing.set(null);
      input.value = '';
      this.errorMessage.set(
        'Outsourced drawing size must not exceed 100 MB.',
      );
      return;
    }

    this.selectedOutsourceDrawing.set(file);
  }

  onWebDrawingSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (file && file.size > 100 * 1024 * 1024) {
      this.errorMessage.set('Web drawing size must not exceed 100 MB.');
      this.selectedWebDrawing.set(null);
      return;
    }
    this.errorMessage.set('');
    this.selectedWebDrawing.set(file);
  }

  upload(): void {
    if (this.isDeletingDrawing()) {
      return;
    }

    this.errorMessage.set('');
    this.successMessage.set('');

    const pdfFile = this.selectedPdf();
    const drawingFile = this.selectedDrawing();
    const handSketchFile = this.selectedHandSketch();
    const outsourceDrawingFile = this.selectedOutsourceDrawing();
    const webDrawingFile = this.selectedWebDrawing();

    if (!this.hasSelectedFiles()) {
      this.errorMessage.set(
        'Select a material PDF, CAD drawing, hand sketch, or outsourced drawing.',
      );
      return;
    }

    this.isUploading.set(true);

    this.materialApi
      .uploadFiles(
        this.materialId(),
        {
          pdfFile,
          drawingFile,
          handSketchFile,
          outsourceDrawingFile,
          webDrawingFile,
        },
      )
      .pipe(
        finalize(() => {
          this.isUploading.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(response.message);

          if (response.material) {
            this.material.set(response.material);
          } else {
            this.loadMaterial(false);
          }

          this.selectedPdf.set(null);
          this.selectedDrawing.set(null);
          this.selectedHandSketch.set(null);
          this.selectedOutsourceDrawing.set(null);
          this.selectedWebDrawing.set(null);
          this.resetFileInputs();
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  clearSelection(): void {
    this.selectedPdf.set(null);
    this.selectedDrawing.set(null);
    this.selectedHandSketch.set(null);
    this.selectedOutsourceDrawing.set(null);
    this.selectedWebDrawing.set(null);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.resetFileInputs();
  }

  deleteDrawing(documentType = 'drawing'): void {
    const material =
      this.material();

    if (
      !material ||
      this.isUploading() ||
      this.isDeletingDrawing()
    ) {
      return;
    }

    const materialLabel =
      material.materialNo?.trim();

    const confirmationMessage =
      materialLabel
        ? `Delete the drawing for material ${materialLabel}? It will be removed from every record with the same material number.`
        : 'Delete this drawing?';

    if (!window.confirm(confirmationMessage)) {
      return;
    }

    this.errorMessage.set('');
    this.successMessage.set('');
    this.isDeletingDrawing.set(true);

      this.materialApi
      .deleteDrawing(
        material.id,
        documentType,
      )
      .pipe(
        finalize(() => {
          this.isDeletingDrawing.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(response.message);

          if (response.material) {
            this.material.set(response.material);
          } else {
            this.loadMaterial(false);
          }
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  fileName(path: string): string {
    if (!path) {
      return '';
    }

    const parts = path.split('/');

    return parts[parts.length - 1] || path;
  }

  fileSize(file: File | null): string {
    if (!file) {
      return '';
    }

    const megabytes = file.size / (1024 * 1024);

    if (megabytes >= 1) {
      return `${megabytes.toFixed(2)} MB`;
    }

    return `${(file.size / 1024).toFixed(1)} KB`;
  }

  private loadMaterial(showLoading = true): void {
    if (showLoading) {
      this.isLoading.set(true);
    }

    this.errorMessage.set('');

    this.materialApi
      .getDetails(this.materialId())
      .pipe(
        finalize(() => {
          if (showLoading) {
            this.isLoading.set(false);
          }
        }),
      )
      .subscribe({
        next: (response) => {
          this.material.set(response);
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  private resetFileInputs(): void {
    const pdfInput = this.pdfInput();
    const drawingInput = this.drawingInput();
    const handSketchInput = this.handSketchInput();
    const outsourceDrawingInput = this.outsourceDrawingInput();
    const webDrawingInput = this.webDrawingInput();

    if (pdfInput) {
      pdfInput.nativeElement.value = '';
    }

    if (drawingInput) {
      drawingInput.nativeElement.value = '';
    }

    if (handSketchInput) {
      handSketchInput.nativeElement.value = '';
    }

    if (outsourceDrawingInput) {
      outsourceDrawingInput.nativeElement.value = '';
    }
    if (webDrawingInput) {
      webDrawingInput.nativeElement.value = '';
    }
  }

  private extension(fileName: string): string {
    const lastDot = fileName.lastIndexOf('.');

    return lastDot < 0
      ? ''
      : fileName.substring(lastDot).toLowerCase();
  }

  private describeError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 400) {
      const errors = error.error?.errors;

      if (errors && typeof errors === 'object') {
        const messages = Object.values(errors)
          .flatMap((value) =>
            Array.isArray(value)
              ? value
              : [String(value)],
          )
          .filter(
            (value): value is string =>
              typeof value === 'string',
          );

        if (messages.length > 0) {
          return messages.join(' ');
        }
      }

      return 'Please check the selected files.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Please sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to manage material files.';
    }

    if (error.status === 404) {
      return 'Material was not found.';
    }

    if (error.status === 413) {
      return 'The selected upload is too large. The PDF limit is 50 MB and the drawing limit is 100 MB.';
    }

    if (error.status === 415) {
      return 'The selected drawing format is not supported.';
    }

    const apiMessage =
      error.error?.message ??
      error.error?.error;

    return typeof apiMessage === 'string'
      ? apiMessage
      : 'Material files could not be updated.';
  }
}
