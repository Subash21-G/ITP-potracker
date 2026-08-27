import { ManageOnlyDirective } from '../../../core/directives/manage-only.directive';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  DomSanitizer,
  SafeResourceUrl,
} from '@angular/platform-browser';
import {
  ActivatedRoute,
  Router,
  RouterLink,
} from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import {
  MaterialDetails,
  RelatedMaterialPurchaseOrder,
} from '../../../core/models/material-crud.model';
import {
  MaterialCrudApiService,
} from '../../../core/services/material-crud-api.service';
import {
  MaterialsApiService,
} from '../../../core/services/materials-api.service';
import {
  findRelatedMaterial,
} from './related-material-selection';

interface DocumentPreview {
  readonly path: string;
  readonly name: string;
  readonly safeUrl: SafeResourceUrl;
}

@Component({
  selector: 'app-material-details-page',
  imports: [ManageOnlyDirective, 
    CommonModule,
    RouterLink,
  ],
  templateUrl: './material-details-page.html',
  styleUrl: './material-details-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaterialDetailsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly materialApi =
    inject(MaterialCrudApiService);
  private readonly materialsApi =
    inject(MaterialsApiService);
  private readonly sanitizer =
    inject(DomSanitizer);

  readonly material =
    signal<MaterialDetails | null>(null);

  readonly documentPreview = computed<DocumentPreview | null>(
    () => {
      const item = this.material();

      if (!item) {
        return null;
      }

      const path = [
        item.drawingFilePath,
        item.pdfFilePath,
      ].find((candidate) =>
        this.canPreviewDocument(candidate),
      );

      if (!path) {
        return null;
      }

      return {
        path,
        name: this.fileName(path),
        safeUrl:
          this.sanitizer.bypassSecurityTrustResourceUrl(
            this.inlinePreviewPath(path),
          ),
      };
    },
  );

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

  ngOnInit(): void {
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
        },
        error: (error: HttpErrorResponse) => {
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

  fileName(path: string): string {
    if (!path) {
      return '';
    }

    const parts = path.split('/');

    return parts[parts.length - 1] || path;
  }

  private canPreviewDocument(
    path: string | null | undefined,
  ): path is string {
    if (!path) {
      return false;
    }

    const cleanPath = path
      .split(/[?#]/, 1)[0]
      .toLowerCase();

    return /\.(pdf|png|jpe?g|webp|gif|bmp|svg)$/.test(
      cleanPath,
    );
  }

  private inlinePreviewPath(path: string): string {
    const separator = path.includes('?')
      ? '&'
      : '?';

    return `${path}${separator}inline=true`;
  }
}
