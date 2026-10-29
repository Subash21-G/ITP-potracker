import { NumericLinePipe } from '../../../shared/numeric-line.pipe';
import { ManageOnlyDirective } from '../../../core/directives/manage-only.directive';
import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  DomSanitizer,
  SafeResourceUrl,
} from '@angular/platform-browser';
import { ElementRef, viewChild } from '@angular/core';
import { DxfViewer } from 'dxf-viewer';
import {
  ActivatedRoute,
  Router,
  RouterLink,
} from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { catchError, finalize, firstValueFrom, of } from 'rxjs';
import {
  MaterialDetails,
  MaterialFileUpload,
  RelatedMaterialPurchaseOrder,
} from '../../../core/models/material-crud.model';
import {
  MaterialHistoryItem,
  MaterialHistoryOutcome,
} from '../../../core/models/material-history-api.model';
import {
  MaterialCrudApiService,
} from '../../../core/services/material-crud-api.service';
import {
  MaterialHistoryApiService,
} from '../../../core/services/material-history-api.service';
import {
  MaterialsApiService,
} from '../../../core/services/materials-api.service';
import { MasterSheetApiService } from '../../../core/services/mastersheet-api.service';
import { AuthApiService } from '../../../core/services/auth-api.service';
import {
  findRelatedMaterial,
} from './related-material-selection';
import {
  PdfRedactionEditor,
} from '../../../shared/pdf-redaction-editor/pdf-redaction-editor';
import {
  DrawingFrontSource,
  createDrawingFrontBackPdf,
} from './drawing-front-back-pdf';

interface DocumentPreview {
  readonly path: string;
  readonly name: string;
  readonly label: string;
  readonly kind: 'pdf' | 'image' | 'cad' | 'file';
  readonly safeUrl: SafeResourceUrl;
}

@Component({
  selector: 'app-material-details-page',
  imports: [NumericLinePipe, ManageOnlyDirective, 
    CommonModule,
    PdfRedactionEditor,
    RouterLink,
  ],
  templateUrl: './material-details-page.html',
  styleUrl: './material-details-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaterialDetailsPage implements OnInit, OnDestroy {
  readonly pdfEditor = viewChild(PdfRedactionEditor);
  readonly authApi = inject(AuthApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly materialApi =
    inject(MaterialCrudApiService);
  private readonly masterSheetApi = inject(MasterSheetApiService);
  private readonly materialsApi =
    inject(MaterialsApiService);
  private readonly materialHistoryApi =
    inject(MaterialHistoryApiService);
  private readonly sanitizer =
    inject(DomSanitizer);

  readonly material =
    signal<MaterialDetails | null>(null);

  private readonly selectedDocumentPath =
    signal<string | null>(null);

  readonly documentPreview = computed<DocumentPreview | null>(
    () => {
      const item = this.material();

      if (!item) {
        return null;
      }

      const previews = [
        {
          path: item.drawingFilePath,
          label: 'CAD drawing preview',
        },
        {
          path: item.handSketchFilePath,
          label: 'Hand sketch preview',
        },
        {
          path: item.outsourceDrawingFilePath,
          label: 'Outsourced drawing preview',
        },
        {
          path: item.webDrawingFilePath,
          label: 'Web drawing preview',
        },
        {
          path: item.pdfFilePath,
          label: 'Material PDF preview',
        },
      ].filter(
        (
          preview,
        ): preview is {
          path: string;
          label: string;
        } => Boolean(preview.path),
      );

      const selectedPath =
        this.selectedDocumentPath();

      const preview =
        previews.find(
          (candidate) =>
            candidate.path === selectedPath,
        ) ?? previews[0];

      if (!preview) {
        return null;
      }

      return {
        path: preview.path,
        name: this.fileName(preview.path),
        label: preview.label,
        kind: this.documentPreviewKind(preview.path),
        safeUrl:
          this.sanitizer.bypassSecurityTrustResourceUrl(
            this.inlinePreviewPath(preview.path),
          ),
      };
    },
  );
  private readonly cadHost = viewChild<ElementRef<HTMLDivElement>>('cadHost');
  private cadViewer: DxfViewer | null = null;
  private cadBlobUrl: string | null = null;
  readonly cadPreviewError = signal<string | null>(null);

  private readonly cadPreviewEffect = effect(() => {
    const preview = this.documentPreview();
    const host = this.cadHost();
    if (!host || preview?.kind !== 'cad') return;
    this.cadViewer?.Destroy();
    if (this.cadBlobUrl) URL.revokeObjectURL(this.cadBlobUrl);
    this.cadBlobUrl = null;
    this.cadPreviewError.set(null);
    this.cadViewer = new DxfViewer(host.nativeElement, { autoResize: true, clearColor: 0x101827 });
    void fetch(preview.path, { credentials: 'include' })
      .then(response => {
        if (!response.ok) throw new Error(`CAD file request failed (${response.status})`);
        return response.blob();
      })
      .then(blob => {
        this.cadBlobUrl = URL.createObjectURL(blob);
        return this.cadViewer?.Load({ url: this.cadBlobUrl }).then(() => this.fitCadView());
      })
      .catch(() => this.cadPreviewError.set('This CAD file could not be rendered. Download it to open in a CAD application.'));
  });

  zoomCad(direction: 'in' | 'out'): void {
    const camera = this.cadViewer?.GetCamera();
    if (!camera) return;
    camera.zoom *= direction === 'in' ? 1.25 : 0.8;
    camera.updateProjectionMatrix();
    this.cadViewer?.Render();
  }

  fitCadView(): void {
    const bounds = this.cadViewer?.GetBounds();
    if (!bounds) return;
    this.cadViewer?.FitView(bounds.minX, bounds.maxX, bounds.minY, bounds.maxY, 1.1);
  }

  resetCadView(): void { this.fitCadView(); }

  async fullscreenCad(): Promise<void> {
    const host = this.cadHost()?.nativeElement;
    if (host && !document.fullscreenElement) await host.requestFullscreen();
    else if (document.fullscreenElement) await document.exitFullscreen();
  }

  readonly isPreparingDrawingPrint = signal(false);
  readonly drawingPrintError = signal('');

  readonly saveEditedDocument = async (file: File, sourceUrl: string): Promise<void> => {
    const item = this.material();
    if (!item) throw new Error('Material is unavailable. Reload the page and try again.');

    const documentFields = [
      ['drawingFilePath', 'drawingFile'],
      ['handSketchFilePath', 'handSketchFile'],
      ['outsourceDrawingFilePath', 'outsourceDrawingFile'],
      ['webDrawingFilePath', 'webDrawingFile'],
      ['pdfFilePath', 'pdfFile'],
    ] as const;
    const field = documentFields.find(([path]) => item[path] === sourceUrl);
    if (!field) throw new Error('This document has changed. Reload it before saving edits.');

    const files: MaterialFileUpload = { [field[1]]: file };
    try {
      const response = await firstValueFrom(this.materialApi.uploadFiles(item.id, files));
      if (!response.success) throw new Error(response.message || 'PDF edits could not be saved.');
      const updated = response.material ?? await firstValueFrom(this.materialApi.getDetails(item.id, true));
      if (this.material()?.id === item.id) {
        this.material.set(updated);
        this.selectedDocumentPath.set(updated[field[0]]);
        this.actionMessage.set('PDF edits saved. Front + backside printing uses the edited drawing.');
      }
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        const validation = error.error?.errors;
        const message = validation && typeof validation === 'object'
          ? Object.values(validation).flat().join(' ')
          : error.error?.message || error.error?.error;
        throw new Error(
          error.status === 403 ? 'You do not have permission to save material files.' :
          error.status === 401 ? 'Your session has expired. Please sign in again.' :
          message || 'PDF edits could not be saved. Your edits are still in the preview; please try again.',
          { cause: error },
        );
      }
      throw error;
    }
  };

  async printOutsourceDetails(): Promise<void> {
    const item = this.material();
    const drawingPath = item
      ? this.backsideDrawingPath(item)
      : null;

    if (
      !item ||
      !drawingPath ||
      this.isPreparingDrawingPrint() || this.pdfEditor()?.isSaving()
    ) {
      return;
    }

    const printWindow = window.open('', '_blank');

    if (!printWindow) {
      this.drawingPrintError.set(
        'Allow pop-ups for PO Tracker to open the front and backside PDF.',
      );
      return;
    }

    printWindow.document.title =
      'Preparing drawing front and backside';
    printWindow.document.body.textContent =
      'Preparing drawing front and backside for printing...';

    this.isPreparingDrawingPrint.set(true);
    this.drawingPrintError.set('');

    try {
      const source =
        await this.loadDrawingFrontSource(
          drawingPath,
        );
      const masterRecord = await firstValueFrom(
        this.masterSheetApi.getRecord(item.materialNo).pipe(catchError((error) => {
          if (error instanceof HttpErrorResponse && error.status === 404) return of(null);
          throw new Error('Master data could not be loaded for the drawing backside. Please try printing again.');
        })),
      );
      const processSteps = masterRecord?.processSteps ?? [];
      const pdfBytes =
        await createDrawingFrontBackPdf(
          source,
          {
            materialNo: item.materialNo || '',
            shortDescription: item.shortDescription || '',
            fullDescription:
              item.briefDescription ||
              item.shortDescription ||
              '',
            drawingCode: masterRecord?.drawingNo || item.drawingNo || '',
            finishSize: masterRecord?.finishSize || '',
            grade: item.rmGrade || '',
            processQty: item.dispatchQty > 0 ? item.dispatchQty : undefined,
            requiredQty: item.requiredQty ?? item.pendingQty,
            stockQty: item.stockQty,
            rmSize: item.rmSize || '',
            rmProcess: processSteps.length
              ? processSteps.map((step) => `Process ${step.sequence}: ${step.processName}`).join(', ')
              : item.rmProcess || '',
            rmSource: item.rmSource || '',
            vendor: processSteps.length
              ? processSteps.map((step) => step.vendorName || 'No vendor').join(', ')
              : item.rmSource || '',
            processSteps: processSteps.map((step) => ({
              processName: step.processName,
              vendorName: step.vendorName,
            })),
            drawingVersion: item.drawingVersion || '',
          },
        );
      const blobBytes = new Uint8Array(
        pdfBytes.byteLength,
      );

      blobBytes.set(pdfBytes);

      const url = URL.createObjectURL(
        new Blob([blobBytes.buffer], {
          type: 'application/pdf',
        }),
      );

      printWindow.location.replace(url);

      window.setTimeout(() => {
        URL.revokeObjectURL(url);
      }, 120_000);
    } catch (error) {
      printWindow.close();
      this.drawingPrintError.set(
        error instanceof Error
          ? error.message
          : 'The drawing front and backside PDF could not be created.',
      );
    } finally {
      this.isPreparingDrawingPrint.set(false);
    }
  }

  private backsideDrawingPath(
    item: MaterialDetails,
  ): string | null {
    // Use the drawing currently selected in the files list. This is
    // important for outsourced materials: many records have both an
    // in-house CAD drawing and an outsourced PDF, and the export must use
    // the file the user chose rather than always taking the CAD path first.
    const selectedPath = this.selectedDocumentPath();
    if (selectedPath) {
      return selectedPath;
    }

    const previewPath = this.documentPreview()?.path;
    if (previewPath) {
      return previewPath;
    }

    return (
      item.drawingFilePath ||
      item.outsourceDrawingFilePath ||
      item.webDrawingFilePath ||
      null
    );
  }

  private async loadDrawingFrontSource(
    drawingPath: string,
  ): Promise<DrawingFrontSource> {
    const editor = this.pdfEditor();
    if (editor && editor.sourceUrl === drawingPath) {
      return { kind: 'pdf', bytes: await editor.createEditedPdf() };
    }

    const response = await fetch(drawingPath, {
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error(
        `The drawing could not be downloaded (${response.status}).`,
      );
    }

    const drawingBlob = await response.blob();
    const extension = this.documentExtension(
      drawingPath,
    ).toLowerCase();

    if (extension === 'pdf') {
      return {
        kind: 'pdf',
        bytes: new Uint8Array(
          await drawingBlob.arrayBuffer(),
        ),
      };
    }

    if (extension === 'png') {
      return {
        kind: 'png',
        bytes: new Uint8Array(
          await drawingBlob.arrayBuffer(),
        ),
      };
    }

    if (
      extension === 'jpg' ||
      extension === 'jpeg'
    ) {
      return {
        kind: 'jpeg',
        bytes: new Uint8Array(
          await drawingBlob.arrayBuffer(),
        ),
      };
    }

    if (
      ['webp', 'gif', 'bmp', 'svg'].includes(
        extension,
      )
    ) {
      return {
        kind: 'png',
        bytes:
          await this.convertImageBlobToPng(
            drawingBlob,
          ),
      };
    }

    if (
      ['dwg', 'dxf', 'step', 'stp', 'iges', 'igs'].includes(
        extension,
      )
    ) {
      const preview = this.documentPreview();
      const canvas = this.cadHost()
        ?.nativeElement
        .querySelector('canvas');

      if (
        preview?.path === drawingPath &&
        canvas
      ) {
        const canvasResponse = await fetch(
          canvas.toDataURL('image/png'),
        );

        return {
          kind: 'png',
          bytes: new Uint8Array(
            await canvasResponse.arrayBuffer(),
          ),
        };
      }

      throw new Error(
        'Wait for the CAD drawing preview to finish loading, then create the front and backside PDF again.',
      );
    }

    throw new Error(
      'Create front and backside PDF supports PDF and image drawings. Export this CAD drawing to PDF first.',
    );
  }

  private async convertImageBlobToPng(
    source: Blob,
  ): Promise<Uint8Array> {
    const sourceUrl = URL.createObjectURL(source);

    try {
      const image = await new Promise<HTMLImageElement>(
        (resolve, reject) => {
          const element = new Image();

          element.onload = () => resolve(element);
          element.onerror = () =>
            reject(
              new Error(
                'The drawing image could not be prepared for printing.',
              ),
            );
          element.src = sourceUrl;
        },
      );
      const canvas = document.createElement('canvas');

      canvas.width = image.naturalWidth;
      canvas.height = image.naturalHeight;

      const context = canvas.getContext('2d');

      if (!context) {
        throw new Error(
          'The drawing image could not be prepared for printing.',
        );
      }

      context.fillStyle = '#ffffff';
      context.fillRect(
        0,
        0,
        canvas.width,
        canvas.height,
      );
      context.drawImage(image, 0, 0);

      const pngBlob = await new Promise<Blob>(
        (resolve, reject) => {
          canvas.toBlob(
            (value) => {
              if (value) {
                resolve(value);
              } else {
                reject(
                  new Error(
                    'The drawing image could not be prepared for printing.',
                  ),
                );
              }
            },
            'image/png',
          );
        },
      );

      return new Uint8Array(
        await pngBlob.arrayBuffer(),
      );
    } finally {
      URL.revokeObjectURL(sourceUrl);
    }
  }

  readonly isLoading = signal(true);
  readonly isDeleting = signal(false);
  readonly errorMessage = signal('');
  readonly actionMessage = signal('');
  readonly relatedPurchaseOrders =
    signal<RelatedMaterialPurchaseOrder[]>([]);
  readonly relatedPurchaseOrdersLoading = signal(false);
  readonly relatedPurchaseOrdersError = signal('');
  readonly selectingPurchaseOrderId =
    signal<number | null>(null);
  readonly relatedPurchaseOrderNavigationError = signal('');
  readonly materialHistoryItems =
    signal<readonly MaterialHistoryItem[]>([]);
  readonly materialHistoryTotal = signal(0);
  readonly materialHistoryLoading = signal(false);
  readonly materialHistoryError = signal('');

  ngOnInit(): void {
    void this.cadPreviewEffect;
    this.route.paramMap.subscribe((params) => {
      const id = Number(
        params.get('id'),
      );

      if (!Number.isInteger(id) || id <= 0) {
        this.material.set(null);
        this.errorMessage.set(
          'Invalid material number.',
        );
        this.isLoading.set(false);
        return;
      }

      this.loadMaterial(id);
    });
  }

  ngOnDestroy(): void {
    this.cadViewer?.Destroy();
    if (this.cadBlobUrl) URL.revokeObjectURL(this.cadBlobUrl);
  }

  loadMaterial(id?: number): void {
    const materialId =
      id ?? this.material()?.id;

    if (!materialId) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');
    this.actionMessage.set('');

    this.materialApi
      .getDetails(materialId)
      .pipe(
        finalize(() => {
          this.isLoading.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          this.material.set(response);
          this.loadRelatedPurchaseOrders(response.id);
          this.loadMaterialHistory(response.id);
        },
        error: (error: HttpErrorResponse) => {
          this.materialHistoryItems.set([]);
          this.materialHistoryTotal.set(0);
          this.materialHistoryError.set('');

          if (error.status === 404) {
            this.errorMessage.set(
              'Material was not found.',
            );
            return;
          }

          if (error.status === 401) {
            this.errorMessage.set(
              'Your session has expired. Please sign in again.',
            );
            return;
          }

          if (error.status === 403) {
            this.errorMessage.set(
              'You do not have permission to view this material.',
            );
            return;
          }

          this.errorMessage.set(
            'Material details could not be loaded.',
          );
        },
      });
  }

  private loadMaterialHistory(materialId: number): void {
    this.materialHistoryLoading.set(true);
    this.materialHistoryError.set('');

    this.materialHistoryApi
      .getHistory({
        materialId,
        page: 1,
        pageSize: 10,
      })
      .pipe(
        finalize(() => {
          this.materialHistoryLoading.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          if (this.material()?.id !== materialId) {
            return;
          }

          this.materialHistoryItems.set(response.items);
          this.materialHistoryTotal.set(response.totalRecords);
        },
        error: () => {
          if (this.material()?.id !== materialId) {
            return;
          }

          this.materialHistoryItems.set([]);
          this.materialHistoryTotal.set(0);
          this.materialHistoryError.set(
            'Remark history could not be loaded.',
          );
        },
      });
  }

  private loadRelatedPurchaseOrders(
    materialId: number,
  ): void {
    this.relatedPurchaseOrdersLoading.set(true);
    this.relatedPurchaseOrdersError.set('');

    this.materialApi
      .getRelatedPurchaseOrders(materialId)
      .pipe(
        finalize(() => {
          this.relatedPurchaseOrdersLoading.set(false);
        }),
      )
      .subscribe({
        next: (purchaseOrders) => {
          this.relatedPurchaseOrders.set(purchaseOrders);
        },
        error: () => {
          this.relatedPurchaseOrders.set([]);
          this.relatedPurchaseOrdersError.set(
            'Related purchase orders could not be loaded.',
          );
        },
      });
  }

  openRelatedPurchaseOrder(
    purchaseOrder: RelatedMaterialPurchaseOrder,
  ): void {
    const current = this.material();

    if (
      !current ||
      this.selectingPurchaseOrderId() !== null ||
      purchaseOrder.purchaseOrderId ===
        current.purchaseOrderId
    ) {
      return;
    }

    this.selectingPurchaseOrderId.set(
      purchaseOrder.purchaseOrderId,
    );
    this.relatedPurchaseOrderNavigationError.set('');

    this.materialsApi
      .getMaterials(
        {
          page: 1,
          pageSize: Math.min(
            200,
            Math.max(
              20,
              purchaseOrder.materialCount,
            ),
          ),
          sortColumn: 'line',
          sortDirection: 'asc',
          columnPoNumber:
            purchaseOrder.poNumber ||
            undefined,
          materialNo: current.materialNo,
        },
        true,
      )
      .pipe(
        finalize(() => {
          this.selectingPurchaseOrderId.set(null);
        }),
      )
      .subscribe({
        next: (response) => {
          const targetMaterial =
            findRelatedMaterial(
              response.items,
              purchaseOrder.purchaseOrderId,
              current.materialNo,
            );

          if (!targetMaterial) {
            this.relatedPurchaseOrderNavigationError.set(
              'The matching material line could not be found for this purchase order.',
            );
            return;
          }

          void this.router.navigate([
            '/materials',
            targetMaterial.id,
          ]);
        },
        error: () => {
          this.relatedPurchaseOrderNavigationError.set(
            'The selected purchase order could not be opened here. Please try again.',
          );
        },
      });
  }

  deleteMaterial(): void {
    const current = this.material();

    if (!current || this.isDeleting()) {
      return;
    }

    if (!current.canDelete) {
      this.actionMessage.set(
        'This material cannot be deleted because invoice or dispatch records are linked.',
      );
      return;
    }

    const confirmed = window.confirm(
      `Delete material ${current.materialNo || current.id}?`,
    );

    if (!confirmed) {
      return;
    }

    this.isDeleting.set(true);
    this.actionMessage.set('');

    this.materialApi
      .delete(current.id)
      .pipe(
        finalize(() => {
          this.isDeleting.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          window.alert(response.message);
          void this.router.navigateByUrl(
            '/materials',
          );
        },
        error: (error: HttpErrorResponse) => {
          const message =
            typeof error.error?.message === 'string'
              ? error.error.message
              : '';

          if (error.status === 409) {
            this.actionMessage.set(
              message ||
                'This material has linked records and cannot be deleted.',
            );
            return;
          }

          if (error.status === 403) {
            this.actionMessage.set(
              'You do not have permission to delete materials.',
            );
            return;
          }

          this.actionMessage.set(
            message ||
              'Material could not be deleted.',
          );
        },
      });
  }

  statusClass(status: string): string {
    return status
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-');
  }

  historyOutcomeLabel(outcome: MaterialHistoryOutcome): string {
    switch (outcome) {
      case 'PerfectDelivery':
        return 'Perfect delivery';
      case 'ReworkAndDelivery':
        return 'Rework and delivery';
      case 'Rejected':
        return 'Rejected';
    }
  }

  historyOutcomeClass(outcome: MaterialHistoryOutcome): string {
    switch (outcome) {
      case 'PerfectDelivery':
        return 'perfect';
      case 'ReworkAndDelivery':
        return 'rework';
      case 'Rejected':
        return 'rejected';
    }
  }

  fileName(path: string): string {
    if (!path) {
      return '';
    }

    const parts = path.split('/');

    return parts[parts.length - 1] || path;
  }

  selectDocumentPreview(path: string): void {
    if (path && !this.pdfEditor()?.isSaving()) {
      this.selectedDocumentPath.set(path);
    }
  }

  isDocumentPreviewSelected(path: string): boolean {
    return this.documentPreview()?.path === path;
  }

  canPreviewDocument(
    path: string | null | undefined,
  ): path is string {
    if (!path) {
      return false;
    }

    const cleanPath = path
      .split(/[?#]/, 1)[0]
      .toLowerCase();

    return /\.(pdf|png|jpe?g|webp|gif|bmp|svg|dwg|dxf|step|stp|iges|igs)$/.test(
      cleanPath,
    );
  }

  documentExtension(path: string): string {
    const cleanPath = path
      .split(/[?#]/, 1)[0]
      .toLowerCase();

    const lastDot = cleanPath.lastIndexOf('.');

    return lastDot >= 0
      ? cleanPath.substring(lastDot + 1).toUpperCase()
      : 'FILE';
  }

  private documentPreviewKind(
    path: string,
  ): DocumentPreview['kind'] {
    const cleanPath = path
      .split(/[?#]/, 1)[0]
      .toLowerCase();

    if (cleanPath.endsWith('.pdf')) {
      return 'pdf';
    }

    if (/\.(png|jpe?g|webp|gif|bmp|svg)$/.test(cleanPath)) {
      return 'image';
    }

    if (/\.(dwg|dxf|step|stp|iges|igs)$/.test(cleanPath)) {
      return 'cad';
    }

    return 'file';
  }

  private inlinePreviewPath(path: string): string {
    const separator = path.includes('?')
      ? '&'
      : '?';

    return `${path}${separator}inline=true`;
  }
}
