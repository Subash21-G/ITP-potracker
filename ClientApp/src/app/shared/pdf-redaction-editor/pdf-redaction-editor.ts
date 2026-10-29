import { CommonModule, DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  inject,
  signal,
} from '@angular/core';
import {
  GlobalWorkerOptions,
  PDFDocumentLoadingTask,
  PDFDocumentProxy,
  PageViewport,
  getDocument,
} from 'pdfjs-dist';
import {
  PDFDocument,
  RGB,
  rgb,
} from 'pdf-lib';
import { firstValueFrom } from 'rxjs';

type EditorTool = 'view' | 'erase';
type MaskFill = 'white' | 'black' | 'gray';

interface PdfPageView {
  readonly pageNumber: number;
  readonly width: number;
  readonly height: number;
}

interface PdfMask {
  readonly id: number;
  readonly pageNumber: number;
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
  readonly fill: MaskFill;
}

interface DraftPdfMask extends PdfMask {
  readonly startX: number;
  readonly startY: number;
}

@Component({
  selector: 'app-pdf-redaction-editor',
  imports: [CommonModule],
  templateUrl: './pdf-redaction-editor.html',
  styleUrl: './pdf-redaction-editor.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PdfRedactionEditor implements OnChanges, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly changeDetector = inject(ChangeDetectorRef);

  @Input({ required: true }) sourceUrl = '';
  @Input() fileName = 'drawing.pdf';
  @Input() savePdf: ((file: File, sourceUrl: string) => Promise<void>) | null = null;

  readonly pages = signal<readonly PdfPageView[]>([]);
  readonly masks = signal<readonly PdfMask[]>([]);
  readonly draftMask = signal<DraftPdfMask | null>(null);
  readonly tool = signal<EditorTool>('view');
  readonly maskFill = signal<MaskFill>('white');
  readonly zoom = signal(1);
  readonly isLoading = signal(true);
  readonly isExporting = signal(false);
  readonly isSaving = signal(false);
  readonly saveMessage = signal('');
  readonly errorMessage = signal('');

  private pdfDocument: PDFDocumentProxy | null = null;
  private loadingTask: PDFDocumentLoadingTask | null = null;
  private sourcePdfBytes: Uint8Array | null = null;
  private readonly pageViewports = new Map<number, PageViewport>();
  private loadSequence = 0;
  private nextMaskId = 1;

  constructor() {
    const assetsRoot = new URL(
      'assets/pdfjs/',
      this.document.baseURI,
    );

    GlobalWorkerOptions.workerSrc = new URL(
      'pdf.worker.min.mjs',
      assetsRoot,
    ).href;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['sourceUrl'] && this.sourceUrl) {
      void this.loadPdf();
    }
  }

  ngOnDestroy(): void {
    this.loadSequence += 1;
    void this.destroyPdfDocument();
  }

  setTool(tool: EditorTool): void {
    this.tool.set(tool);
    this.draftMask.set(null);
  }

  setMaskFill(event: Event): void {
    const value =
      (event.target as HTMLSelectElement).value as MaskFill;

    if (value === 'white' || value === 'black' || value === 'gray') {
      this.maskFill.set(value);
    }
  }

  setZoom(value: number): void {
    const nextZoom = Math.min(2, Math.max(0.75, value));

    if (nextZoom === this.zoom()) {
      return;
    }

    this.zoom.set(nextZoom);
    void this.renderPages();
  }

  masksForPage(pageNumber: number): readonly PdfMask[] {
    return this.masks().filter(
      (mask) => mask.pageNumber === pageNumber,
    );
  }

  startMask(event: PointerEvent, pageNumber: number): void {
    if (this.tool() !== 'erase' || this.isLoading() || this.isSaving() || this.isExporting()) {
      return;
    }

    event.preventDefault();

    const layer = event.currentTarget as HTMLElement;
    const point = this.normalizedPoint(event, layer);

    layer.setPointerCapture(event.pointerId);

    this.draftMask.set({
      id: this.nextMaskId,
      pageNumber,
      startX: point.x,
      startY: point.y,
      x: point.x,
      y: point.y,
      width: 0,
      height: 0,
      fill: this.maskFill(),
    });
  }

  moveMask(event: PointerEvent, pageNumber: number): void {
    const draft = this.draftMask();

    if (!draft || draft.pageNumber !== pageNumber) {
      return;
    }

    event.preventDefault();

    const point = this.normalizedPoint(
      event,
      event.currentTarget as HTMLElement,
    );

    this.draftMask.set({
      ...draft,
      x: Math.min(draft.startX, point.x),
      y: Math.min(draft.startY, point.y),
      width: Math.abs(point.x - draft.startX),
      height: Math.abs(point.y - draft.startY),
    });
  }

  finishMask(event: PointerEvent, pageNumber: number): void {
    const draft = this.draftMask();

    if (!draft || draft.pageNumber !== pageNumber) {
      return;
    }

    const layer = event.currentTarget as HTMLElement;

    if (layer.hasPointerCapture(event.pointerId)) {
      layer.releasePointerCapture(event.pointerId);
    }

    this.draftMask.set(null);

    if (draft.width < 0.004 || draft.height < 0.004) {
      return;
    }

    const mask: PdfMask = {
      id: draft.id,
      pageNumber: draft.pageNumber,
      x: draft.x,
      y: draft.y,
      width: draft.width,
      height: draft.height,
      fill: draft.fill,
    };

    this.nextMaskId += 1;
    this.saveMessage.set('');
    this.masks.update((current) => [...current, mask]);
  }

  cancelMask(event: PointerEvent): void {
    const layer = event.currentTarget as HTMLElement;

    if (layer.hasPointerCapture(event.pointerId)) {
      layer.releasePointerCapture(event.pointerId);
    }

    this.draftMask.set(null);
  }

  undoMask(): void {
    if (this.isSaving()) return;
    this.saveMessage.set('');
    this.masks.update((current) => current.slice(0, -1));
  }

  clearMasks(): void {
    if (this.isSaving()) return;
    this.saveMessage.set('');
    this.masks.set([]);
    this.draftMask.set(null);
  }

  async downloadEditedPdf(): Promise<void> {
    await this.exportEditedPdf((url, editedName) => {
      const link = this.document.createElement('a');

      link.href = url;
      link.download = editedName;
      link.click();
    });
  }

  async saveEditedPdf(): Promise<void> {
    if (!this.savePdf || this.isLoading() || this.isSaving() || this.isExporting() || !this.masks().length) {
      return;
    }

    const sourceUrl = this.sourceUrl;
    this.isSaving.set(true);
    this.errorMessage.set('');
    this.saveMessage.set('');
    try {
      const bytes = await this.createEditedPdf();
      const file = new File([new Uint8Array(bytes).buffer], this.editedFileName(), {
        type: 'application/pdf',
      });
      await this.savePdf(file, sourceUrl);
      this.saveMessage.set('PDF edits saved.');
    } catch (error) {
      this.errorMessage.set(
        error instanceof Error ? error.message : 'PDF edits could not be saved. Please try again.',
      );
    } finally {
      this.isSaving.set(false);
    }
  }

  async printEditedPdf(): Promise<void> {
    if (this.isLoading() || this.isExporting() || this.isSaving()) return;
    const printWindow = window.open('', '_blank');

    if (!printWindow) {
      this.errorMessage.set(
        'Allow pop-ups for PO Tracker to open the edited PDF for printing.',
      );
      return;
    }

    printWindow.document.title = 'Preparing edited drawing';
    printWindow.document.body.textContent =
      'Preparing edited drawing for printing...';

    await this.exportEditedPdf((url) => {
      printWindow.location.replace(url);

      window.setTimeout(() => {
        URL.revokeObjectURL(url);
      }, 120_000);
    }, false);
  }

  private async loadPdf(): Promise<void> {
    const sequence = ++this.loadSequence;

    this.isLoading.set(true);
    this.errorMessage.set('');
    if (!this.isSaving()) this.saveMessage.set('');
    this.pages.set([]);
    this.masks.set([]);
    this.draftMask.set(null);
    this.pageViewports.clear();
    this.sourcePdfBytes = null;

    await this.destroyPdfDocument();

    try {
      const buffer = await firstValueFrom(
        this.http.get(this.sourceUrl, {
          responseType: 'arraybuffer',
          withCredentials: true,
        }),
      );

      if (sequence !== this.loadSequence) {
        return;
      }

      const sourceBytes = new Uint8Array(buffer);

      this.sourcePdfBytes = sourceBytes.slice();

      const assetsRoot = new URL(
        'assets/pdfjs/',
        this.document.baseURI,
      );

      this.loadingTask = getDocument({
        data: sourceBytes,
        cMapUrl: new URL('cmaps/', assetsRoot).href,
        cMapPacked: true,
        standardFontDataUrl: new URL(
          'standard_fonts/',
          assetsRoot,
        ).href,
        wasmUrl: new URL('wasm/', assetsRoot).href,
      });

      const pdfDocument = await this.loadingTask.promise;

      if (sequence !== this.loadSequence) {
        await pdfDocument.cleanup();
        return;
      }

      this.pdfDocument = pdfDocument;

      const pageViews: PdfPageView[] = [];

      for (
        let pageNumber = 1;
        pageNumber <= pdfDocument.numPages;
        pageNumber += 1
      ) {
        const page = await pdfDocument.getPage(pageNumber);
        const viewport = page.getViewport({ scale: 1 });

        this.pageViewports.set(pageNumber, viewport);
        pageViews.push({
          pageNumber,
          width: viewport.width,
          height: viewport.height,
        });
      }

      this.pages.set(pageViews);
      this.isLoading.set(false);
      this.changeDetector.detectChanges();

      await this.renderPages();
    } catch {
      if (sequence !== this.loadSequence) {
        return;
      }

      this.isLoading.set(false);
      this.errorMessage.set(
        'The PDF editor could not load this document. You can still use Open full view.',
      );
    }
  }

  private async renderPages(): Promise<void> {
    const pdfDocument = this.pdfDocument;

    if (!pdfDocument || this.pages().length === 0) {
      return;
    }

    this.changeDetector.detectChanges();

    await new Promise<void>((resolve) => {
      requestAnimationFrame(() => resolve());
    });

    const zoom = this.zoom();
    const renderScale = 1.5 * zoom;
    const outputScale = Math.min(
      window.devicePixelRatio || 1,
      2,
    );

    for (const pageView of this.pages()) {
      const canvas = this.host.nativeElement.querySelector(
        `canvas[data-page-number="${pageView.pageNumber}"]`,
      ) as HTMLCanvasElement | null;

      if (!canvas) {
        continue;
      }

      const page = await pdfDocument.getPage(pageView.pageNumber);
      const viewport = page.getViewport({ scale: renderScale });

      canvas.width = Math.floor(viewport.width * outputScale);
      canvas.height = Math.floor(viewport.height * outputScale);
      canvas.style.width = `${pageView.width * zoom}px`;
      canvas.style.height = `${pageView.height * zoom}px`;

      await page.render({
        canvas,
        viewport,
        transform:
          outputScale === 1
            ? undefined
            : [outputScale, 0, 0, outputScale, 0, 0],
      }).promise;
    }
  }

  private normalizedPoint(
    event: PointerEvent,
    layer: HTMLElement,
  ): { x: number; y: number } {
    const bounds = layer.getBoundingClientRect();

    return {
      x: Math.min(
        1,
        Math.max(0, (event.clientX - bounds.left) / bounds.width),
      ),
      y: Math.min(
        1,
        Math.max(0, (event.clientY - bounds.top) / bounds.height),
      ),
    };
  }

  private async exportEditedPdf(
    useUrl: (url: string, fileName: string) => void,
    revokeAfterUse = true,
  ): Promise<void> {
    if (!this.sourcePdfBytes || this.isLoading() || this.isExporting() || this.isSaving()) {
      return;
    }

    this.isExporting.set(true);
    this.errorMessage.set('');

    try {
      const bytes = await this.createEditedPdf();
      const blobBytes = new Uint8Array(bytes.byteLength);

      blobBytes.set(bytes);

      const url = URL.createObjectURL(
        new Blob([blobBytes.buffer], {
          type: 'application/pdf',
        }),
      );

      useUrl(url, this.editedFileName());

      if (revokeAfterUse) {
        window.setTimeout(() => URL.revokeObjectURL(url), 1_000);
      }
    } catch {
      this.errorMessage.set(
        'The edited PDF could not be created. Please try again.',
      );
    } finally {
      this.isExporting.set(false);
    }
  }

  async createEditedPdf(): Promise<Uint8Array> {
    if (!this.sourcePdfBytes || this.isLoading()) {
      throw new Error('Wait for the PDF preview to finish loading, then try again.');
    }

    // Snapshot the edits before yielding so every export uses one consistent revision.
    const masks = this.masks();
    const viewports = new Map(this.pageViewports);

    const pdfDocument = await PDFDocument.load(
      this.sourcePdfBytes,
    );

    for (const mask of masks) {
      const page = pdfDocument.getPage(mask.pageNumber - 1);
      const viewport = viewports.get(mask.pageNumber);

      if (!page || !viewport) {
        continue;
      }

      const [firstX, firstY] = viewport.convertToPdfPoint(
        mask.x * viewport.width,
        mask.y * viewport.height,
      );

      const [secondX, secondY] = viewport.convertToPdfPoint(
        (mask.x + mask.width) * viewport.width,
        (mask.y + mask.height) * viewport.height,
      );

      page.drawRectangle({
        x: Math.min(firstX, secondX),
        y: Math.min(firstY, secondY),
        width: Math.abs(secondX - firstX),
        height: Math.abs(secondY - firstY),
        color: this.pdfColor(mask.fill),
      });
    }

    return pdfDocument.save();
  }

  private pdfColor(fill: MaskFill): RGB {
    switch (fill) {
      case 'black':
        return rgb(0, 0, 0);
      case 'gray':
        return rgb(0.72, 0.72, 0.72);
      default:
        return rgb(1, 1, 1);
    }
  }

  private editedFileName(): string {
    const baseName = this.fileName
      .replace(/\.pdf$/i, '')
      .trim();

    return `${baseName || 'drawing'}-edited.pdf`;
  }

  private async destroyPdfDocument(): Promise<void> {
    const loadingTask = this.loadingTask;
    const pdfDocument = this.pdfDocument;

    this.loadingTask = null;
    this.pdfDocument = null;

    if (loadingTask) {
      await loadingTask.destroy();
    } else if (pdfDocument) {
      await pdfDocument.cleanup();
    }
  }
}
