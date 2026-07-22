import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { PurchaseOrderCrudDetails } from '../../../core/models/purchase-order-crud.model';
import { PurchaseOrderCrudApiService } from '../../../core/services/purchase-order-crud-api.service';

@Component({
  selector: 'app-purchase-order-manage-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './purchase-order-manage-page.html',
  styleUrl: './purchase-order-manage-page.css',
})
export class PurchaseOrderManagePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly purchaseOrderApi = inject(PurchaseOrderCrudApiService);

  readonly purchaseOrder = signal<PurchaseOrderCrudDetails | null>(null);
  readonly loading = signal(true);
  readonly uploading = signal(false);
  readonly deleting = signal(false);
  readonly selectedPdf = signal<File | null>(null);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');

  purchaseOrderId = 0;

  ngOnInit(): void {
    const routeId = Number(this.route.snapshot.paramMap.get('id'));

    if (!Number.isInteger(routeId) || routeId <= 0) {
      this.loading.set(false);
      this.errorMessage.set('The Purchase Order address contains an invalid ID.');
      return;
    }

    this.purchaseOrderId = routeId;
    this.loadPurchaseOrder();
  }

  onPdfSelected(event: Event): void {
    this.clearMessages();

    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    if (!file) {
      this.selectedPdf.set(null);
      return;
    }

    const isPdf =
      file.type === 'application/pdf' ||
      file.name.toLowerCase().endsWith('.pdf');

    if (!isPdf) {
      input.value = '';
      this.selectedPdf.set(null);
      this.errorMessage.set('Please select a PDF file only.');
      return;
    }

    const maximumBytes = 50 * 1024 * 1024;

    if (file.size > maximumBytes) {
      input.value = '';
      this.selectedPdf.set(null);
      this.errorMessage.set('The PDF file must not exceed 50 MB.');
      return;
    }

    this.selectedPdf.set(file);
  }

  uploadPdf(fileInput: HTMLInputElement): void {
    const file = this.selectedPdf();

    if (!file || this.uploading()) {
      this.errorMessage.set('Select a PDF file before uploading.');
      return;
    }

    this.clearMessages();
    this.uploading.set(true);

    this.purchaseOrderApi
      .uploadPdf(this.purchaseOrderId, file)
      .pipe(finalize(() => this.uploading.set(false)))
      .subscribe({
        next: (response) => {
          if (response.purchaseOrder) {
            this.purchaseOrder.set(response.purchaseOrder);
          }

          this.successMessage.set(response.message);
          this.selectedPdf.set(null);
          fileInput.value = '';
        },
        error: (error) => {
          this.errorMessage.set(this.getApiError(error, 'Unable to upload the PO PDF.'));
        },
      });
  }

  deletePurchaseOrder(): void {
    const purchaseOrder = this.purchaseOrder();

    if (!purchaseOrder || this.deleting()) {
      return;
    }

    if (!purchaseOrder.canDelete) {
      this.errorMessage.set(
        `Cannot delete PO ${purchaseOrder.poNumber}. ` +
          `${purchaseOrder.materialCount} material(s) are linked to it.`,
      );
      return;
    }

    const confirmed = window.confirm(
      `Delete Purchase Order ${purchaseOrder.poNumber}? This action cannot be undone.`,
    );

    if (!confirmed) {
      return;
    }

    this.clearMessages();
    this.deleting.set(true);

    this.purchaseOrderApi
      .delete(purchaseOrder.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          void this.router.navigate(['/purchase-orders'], {
            queryParams: {
              deleted: '1',
            },
          });
        },
        error: (error) => {
          this.errorMessage.set(this.getApiError(error, 'Unable to delete the Purchase Order.'));
          this.loadPurchaseOrder(false);
        },
      });
  }

  fileName(path: string): string {
    if (!path) {
      return 'No PDF uploaded';
    }

    const normalized = path.replaceAll('\\', '/');
    return normalized.split('/').pop() || 'PO document.pdf';
  }

  private loadPurchaseOrder(showLoader = true): void {
    if (showLoader) {
      this.loading.set(true);
    }

    this.purchaseOrderApi
      .getDetails(this.purchaseOrderId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (purchaseOrder) => {
          this.purchaseOrder.set(purchaseOrder);
          this.errorMessage.set('');
        },
        error: (error) => {
          this.purchaseOrder.set(null);
          this.errorMessage.set(this.getApiError(error, 'Purchase Order was not found.'));
        },
      });
  }

  private clearMessages(): void {
    this.errorMessage.set('');
    this.successMessage.set('');
  }

  private getApiError(error: unknown, fallback: string): string {
    const apiError = error as {
      error?: {
        error?: string;
        message?: string;
        title?: string;
        errors?: Record<string, string[]>;
      };
    };

    const validationErrors = apiError.error?.errors;

    if (validationErrors) {
      const firstMessage = Object.values(validationErrors).flat()[0];

      if (firstMessage) {
        return firstMessage;
      }
    }

    return (
      apiError.error?.error ||
      apiError.error?.message ||
      apiError.error?.title ||
      fallback
    );
  }
}
