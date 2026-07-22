import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { ManageOnlyDirective } from '../../../core/directives/manage-only.directive';
import {
  PendingInvoiceItem,
  PendingInvoiceMatchCandidate,
} from '../../../core/models/invoice-api.model';
import { InvoiceApiService } from '../../../core/services/invoice-api.service';

@Component({
  selector: 'app-pending-invoices-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ManageOnlyDirective,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './pending-invoices-page.html',
  styleUrl: './pending-invoices-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PendingInvoicesPage {
  private readonly invoiceApi = inject(InvoiceApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly filters = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    take: new FormControl(500, { nonNullable: true }),
  });

  readonly items = signal<readonly PendingInvoiceItem[]>([]);
  readonly loading = signal(false);
  readonly retrying = signal(false);
  readonly loadingCandidates = signal(false);
  readonly matchingMaterialId = signal<number | null>(null);
  readonly selectedPendingItem = signal<PendingInvoiceItem | null>(null);
  readonly candidates = signal<readonly PendingInvoiceMatchCandidate[]>([]);
  readonly candidateSearch = new FormControl('', { nonNullable: true });
  readonly errorMessage = signal('');
  readonly successMessage = signal('');

  readonly rowCount = computed(() => this.items().length);

  readonly invoiceCount = computed(() =>
    new Set(
      this.items()
        .map((item) => item.invoiceNumber.trim())
        .filter(Boolean),
    ).size,
  );

  readonly purchaseOrderCount = computed(() =>
    new Set(
      this.items()
        .map((item) => item.poNumber.trim())
        .filter(Boolean),
    ).size,
  );

  readonly totalQuantity = computed(() =>
    this.items().reduce(
      (total, item) => total + Number(item.qty || 0),
      0,
    ),
  );

  readonly totalAmount = computed(() =>
    this.items().reduce(
      (total, item) => total + Number(item.amount || 0),
      0,
    ),
  );

  constructor() {
    this.loadPendingItems();
  }

  applyFilters(): void {
    this.successMessage.set('');
    this.loadPendingItems();
  }

  clearFilters(): void {
    this.successMessage.set('');

    this.filters.reset({
      search: '',
      take: 500,
    });

    this.loadPendingItems();
  }


  openManualMatch(item: PendingInvoiceItem): void {
    this.selectedPendingItem.set(item);
    this.candidateSearch.setValue('');
    this.candidates.set([]);
    this.loadCandidates();
  }

  closeManualMatch(): void {
    if (this.matchingMaterialId() !== null) {
      return;
    }

    this.selectedPendingItem.set(null);
    this.candidateSearch.setValue('');
    this.candidates.set([]);
  }

  searchCandidates(): void {
    this.loadCandidates();
  }

  manualMatch(candidate: PendingInvoiceMatchCandidate): void {
    const pending = this.selectedPendingItem();

    if (
      !pending ||
      this.matchingMaterialId() !== null ||
      !candidate.hasEnoughBalance
    ) {
      return;
    }

    const mismatchParts: string[] = [];

    if (!candidate.isExactPO) {
      mismatchParts.push('PO number');
    }

    if (!candidate.isExactMaterial) {
      mismatchParts.push('material number');
    }

    if (!candidate.isExactLineItem) {
      mismatchParts.push('line item');
    }

    const mismatchWarning = mismatchParts.length > 0
      ? `\n\nMismatch: ${mismatchParts.join(', ')}.`
      : '';

    const confirmed = window.confirm(
      `Match invoice ${pending.invoiceNumber} row to:\n` +
      `PO: ${candidate.poNumber}\n` +
      `Line: ${candidate.lineItem || '—'}\n` +
      `Material: ${candidate.materialNo}\n` +
      `Invoice Qty: ${pending.qty}\n` +
      `Available Balance: ${candidate.balanceQty}` +
      mismatchWarning +
      '\n\nThis will update InvoiceQty and DispatchQty.',
    );

    if (!confirmed) {
      return;
    }

    this.matchingMaterialId.set(candidate.materialId);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.invoiceApi
      .matchPendingItemManually(
        pending.id,
        {
          materialId: candidate.materialId,
          confirmMismatch: mismatchParts.length > 0,
        },
      )
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.matchingMaterialId.set(null)),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(response.message);
          this.closeManualMatch();
          this.loadPendingItems();
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(this.describeManualMatchError(error));
        },
      });
  }

  retryMatching(): void {
    if (this.retrying() || this.loading() || this.rowCount() === 0) {
      return;
    }

    const confirmed = window.confirm(
      `Retry matching for ${this.rowCount()} pending invoice row(s)?\n\n` +
      'Rows will be matched only when PO number, material number, ' +
      'line item and balance quantity are safe.',
    );

    if (!confirmed) {
      return;
    }

    this.retrying.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.invoiceApi
      .retryPendingItems()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.retrying.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(response.message);
          this.loadPendingItems();
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(this.describeRetryError(error));
        },
      });
  }

  retryLoad(): void {
    this.loadPendingItems();
  }


  private loadCandidates(): void {
    const pending = this.selectedPendingItem();

    if (!pending || this.loadingCandidates()) {
      return;
    }

    this.loadingCandidates.set(true);
    this.errorMessage.set('');

    this.invoiceApi
      .getPendingMatchCandidates(
        pending.id,
        this.candidateSearch.value,
        100,
      )
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loadingCandidates.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.candidates.set(response.candidates);
        },
        error: (error: HttpErrorResponse) => {
          this.candidates.set([]);
          this.errorMessage.set(this.describeCandidateError(error));
        },
      });
  }

  private loadPendingItems(): void {
    if (this.loading()) {
      return;
    }

    const values = this.filters.getRawValue();

    this.loading.set(true);
    this.errorMessage.set('');

    this.invoiceApi
      .getPendingItems(
        values.search,
        values.take,
      )
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (items) => {
          this.items.set(items);
        },
        error: (error: HttpErrorResponse) => {
          this.items.set([]);
          this.errorMessage.set(this.describeError(error));
        },
      });
  }

  private describeCandidateError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. Start the ASP.NET application and try again.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Only Admin and Production users can search manual match candidates.';
    }

    if (error.status === 404) {
      return 'The pending row no longer exists or is already matched.';
    }

    return this.readApiMessage(error) ||
      'Manual match candidates could not be loaded.';
  }

  private describeManualMatchError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. Start the ASP.NET application and try again.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Only Admin and Production users can manually match invoice rows.';
    }

    if (error.status === 404) {
      return 'The pending row or selected material no longer exists.';
    }

    if (error.status === 400) {
      return this.readApiMessage(error) ||
        'The selected material failed validation.';
    }

    return this.readApiMessage(error) ||
      'The pending invoice row could not be matched.';
  }

  private describeRetryError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. Start the ASP.NET application and try again.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Only Admin and Production users can retry invoice matching.';
    }

    if (error.status === 400) {
      return this.readApiMessage(error) ||
        'The retry matching request was rejected.';
    }

    return this.readApiMessage(error) ||
      'Pending invoice matching could not be retried.';
  }

  private describeError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. Start the ASP.NET application and try again.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Your account does not have permission to view pending invoice rows.';
    }

    if (error.status === 404) {
      return 'The pending invoice API was not found. Rebuild the ASP.NET application.';
    }

    return this.readApiMessage(error) ||
      'Pending invoice rows could not be loaded.';
  }

  private readApiMessage(error: HttpErrorResponse): string {
    const message = error.error?.message ?? error.error?.error;

    return typeof message === 'string' ? message : '';
  }
}
