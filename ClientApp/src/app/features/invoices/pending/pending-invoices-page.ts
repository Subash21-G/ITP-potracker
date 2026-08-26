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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { ManageOnlyDirective } from '../../../core/directives/manage-only.directive';
import {
  PendingInvoiceExcelFilterColumn,
  PendingInvoiceExcelFilterSelections,
  PendingInvoiceExcelFilterValue,
  PendingInvoiceItem,
  PendingInvoiceMatchCandidate,
  PendingInvoiceSortDirection,
} from '../../../core/models/invoice-api.model';
import { InvoiceApiService } from '../../../core/services/invoice-api.service';
import {
  ServerExcelFilter,
} from '../../../shared/server-excel-filter/server-excel-filter';

@Component({
  selector: 'app-pending-invoices-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ManageOnlyDirective,
    ReactiveFormsModule,
    RouterLink,
    ServerExcelFilter,
  ],
  templateUrl: './pending-invoices-page.html',
  styleUrl: './pending-invoices-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PendingInvoicesPage {
  private readonly invoiceApi = inject(InvoiceApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

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

  readonly sortColumn =
    signal<
      PendingInvoiceExcelFilterColumn | null
    >(
      null,
    );

  readonly sortDirection =
    signal<
      PendingInvoiceSortDirection | null
    >(
      null,
    );

  readonly excelFilterSelections =
    signal<
      PendingInvoiceExcelFilterSelections
    >(
      {},
    );

  readonly excelFilterOptions =
    signal<
      Partial<
        Record<
          PendingInvoiceExcelFilterColumn,
          readonly PendingInvoiceExcelFilterValue[]
        >
      >
    >({});

  readonly excelFilterLoadingColumns =
    signal<
      ReadonlySet<
        PendingInvoiceExcelFilterColumn
      >
    >(
      new Set<
        PendingInvoiceExcelFilterColumn
      >(),
    );

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
    this.readMaterialCreateResult();
    this.loadPendingItems();
  }

  applyFilters(): void {
    this.successMessage.set('');
    this.loadPendingItems();
  }

  excelFilterOptionsFor(
    column:
      PendingInvoiceExcelFilterColumn,
  ): readonly PendingInvoiceExcelFilterValue[] {
    return (
      this.excelFilterOptions()[
        column
      ] ?? []
    );
  }

  excelFilterSelectedValues(
    column:
      PendingInvoiceExcelFilterColumn,
  ): readonly string[] {
    return (
      this.excelFilterSelections()[
        column
      ] ?? []
    );
  }

  excelFilterActive(
    column:
      PendingInvoiceExcelFilterColumn,
  ): boolean {
    return Object.prototype
      .hasOwnProperty.call(
        this.excelFilterSelections(),
        column,
      );
  }

  excelFilterLoading(
    column:
      PendingInvoiceExcelFilterColumn,
  ): boolean {
    return this
      .excelFilterLoadingColumns()
      .has(
        column,
      );
  }

  excelSortDirection(
    column:
      PendingInvoiceExcelFilterColumn,
  ): PendingInvoiceSortDirection | null {
    return this.sortColumn() ===
      column
      ? this.sortDirection()
      : null;
  }

  loadExcelFilterValues(
    column:
      PendingInvoiceExcelFilterColumn,
  ): void {
    if (
      this.excelFilterLoading(
        column,
      )
    ) {
      return;
    }

    this.excelFilterLoadingColumns
      .update(
        columns => {
          const updated =
            new Set(
              columns,
            );

          updated.add(
            column,
          );

          return updated;
        },
      );

    this.invoiceApi
      .getPendingExcelFilterValues(
        this.filters.controls.search.value,
        column,
        this.serializeExcelFilters(),
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(
          () =>
            this.excelFilterLoadingColumns
              .update(
                columns => {
                  const updated =
                    new Set(
                      columns,
                    );

                  updated.delete(
                    column,
                  );

                  return updated;
                },
              ),
        ),
      )
      .subscribe({
        next: options => {
          this.excelFilterOptions
            .update(
              current => ({
                ...current,
                [column]:
                  options,
              }),
            );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
            ),
          );
        },
      });
  }

  applyExcelSort(
    column:
      PendingInvoiceExcelFilterColumn,
    direction:
      PendingInvoiceSortDirection,
  ): void {
    if (this.loading()) {
      return;
    }

    this.sortColumn.set(
      column,
    );

    this.sortDirection.set(
      direction,
    );

    this.successMessage.set('');
    this.loadPendingItems();
  }

  applyExcelFilter(
    column:
      PendingInvoiceExcelFilterColumn,
    values: readonly string[],
  ): void {
    const availableValues =
      this.excelFilterOptions()[
        column
      ] ?? [];

    const selectedSet =
      new Set(
        values,
      );

    const selectsEverything =
      availableValues.length >
        0 &&
      availableValues.length ===
        selectedSet.size &&
      availableValues.every(
        option =>
          selectedSet.has(
            option.value,
          ),
      );

    this.excelFilterSelections
      .update(
        current => {
          const updated = {
            ...current,
          };

          if (selectsEverything) {
            delete updated[
              column
            ];
          } else {
            updated[column] =
              [...values];
          }

          return updated;
        },
      );

    this.successMessage.set('');
    this.loadPendingItems();
  }

  clearExcelFilter(
    column:
      PendingInvoiceExcelFilterColumn,
  ): void {
    this.excelFilterSelections
      .update(
        current => {
          const updated = {
            ...current,
          };

          delete updated[
            column
          ];

          return updated;
        },
      );

    this.successMessage.set('');
    this.loadPendingItems();
  }


  clearFilters(): void {
    this.successMessage.set('');

    this.filters.reset({
      search: '',
      take: 500,
    });

    this.excelFilterSelections.set(
      {},
    );

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

    const isExactMatch =
      candidate.isExactPO &&
      candidate.isExactMaterial &&
      candidate.isExactLineItem;

    if (
      !pending ||
      this.matchingMaterialId() !== null ||
      !candidate.hasEnoughBalance ||
      !isExactMatch
    ) {
      return;
    }

    const confirmed = window.confirm(
      `Match invoice ${pending.invoiceNumber} row to:\n` +
        `PO: ${candidate.poNumber}\n` +
        `Line: ${candidate.lineItem || '—'}\n` +
        `Material: ${candidate.materialNo}\n` +
        `Invoice Qty: ${pending.qty}\n` +
        `Available Balance: ${candidate.balanceQty}` +
        '\n\nPO, material and line item are exact. This will update InvoiceQty and DispatchQty.',
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
          confirmMismatch: false,
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


  createMissingMaterial(item: PendingInvoiceItem): void {
    if (!item || item.id <= 0) {
      return;
    }

    void this.router.navigate(
      ['/materials/new'],
      {
        queryParams: {
          source: 'pending-invoice',
          pendingInvoiceId: item.id,
          invoiceNumber: item.invoiceNumber,
          invoiceDate: item.invoiceDate?.substring(0, 10) ?? '',
          customerName: item.customerName,
          poNumber: item.poNumber,
          lineItem: item.lineItem,
          materialNo: item.materialNo,
          description: item.description,
          hsnSacCode: item.hsnSacCode,
          qty: item.qty,
          returnUrl: '/invoices/pending',
        },
      },
    );
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



  private readMaterialCreateResult(): void {
    const params = this.route.snapshot.queryParamMap;

    if (params.get('materialCreated') !== 'true') {
      return;
    }

    const matchedRows = Number(
      params.get('matchedPendingInvoiceRows') ?? '0',
    );

    const invoiceNumber =
      params.get('invoiceNumber')?.trim() ?? '';

    if (Number.isFinite(matchedRows) && matchedRows > 0) {
      this.successMessage.set(
        `Material created successfully. ${matchedRows} pending invoice row(s)` +
          `${invoiceNumber ? ` from ${invoiceNumber}` : ''} were automatically matched.`,
      );
    } else {
      this.successMessage.set(
        `Material created successfully, but no pending invoice row was automatically matched.` +
          ` Verify the PO number, material number, line item and ordered quantity.`,
      );
    }

    void this.router.navigate(
      [],
      {
        relativeTo: this.route,
        replaceUrl: true,
        queryParamsHandling: 'merge',
        queryParams: {
          materialCreated: null,
          materialId: null,
          matchedPendingInvoiceRows: null,
          invoiceNumber: null,
          pendingInvoiceId: null,
        },
      },
    );
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
        this.sortColumn() ??
          undefined,
        this.sortDirection() ??
          undefined,
        this.serializeExcelFilters(),
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

  private serializeExcelFilters():
    string | undefined {
    const selections =
      this.excelFilterSelections();

    return Object.keys(
      selections,
    ).length > 0
      ? JSON.stringify(
          selections,
        )
      : undefined;
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

