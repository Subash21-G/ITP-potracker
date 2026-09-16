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
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, finalize } from 'rxjs';

import { ManageOnlyDirective } from '../../core/directives/manage-only.directive';
import {
  MaterialHistoryItem,
  MaterialHistoryExcelFilterColumn,
  MaterialHistoryExcelFilterSelections,
  MaterialHistoryExcelFilterValue,
  MaterialHistoryMaterialOption,
  MaterialHistoryOutcome,
  MaterialHistorySaveResponse,
  MaterialHistorySummary,
  MaterialHistorySortDirection,
  MaterialHistoryUpsertRequest,
} from '../../core/models/material-history-api.model';
import { MaterialHistoryApiService } from '../../core/services/material-history-api.service';
import { ServerExcelFilter } from '../../shared/server-excel-filter/server-excel-filter';

@Component({
  selector: 'app-material-history-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ManageOnlyDirective,
    ReactiveFormsModule,
    RouterLink,
    ServerExcelFilter,
  ],
  templateUrl: './material-history-page.html',
  styleUrl: './material-history-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaterialHistoryPage {
  private readonly api = inject(MaterialHistoryApiService);

  private readonly route = inject(ActivatedRoute);

  private readonly destroyRef = inject(DestroyRef);

  readonly searchControl = new FormControl('', { nonNullable: true });

  readonly outcomeFilterControl = new FormControl<MaterialHistoryOutcome | ''>('', {
    nonNullable: true,
  });

  readonly materialFilterControl = new FormControl<number | null>(null);

  readonly form = new FormGroup({
    materialId: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    occurredDate: new FormControl(this.todayInputValue(), {
      nonNullable: true,
      validators: [Validators.required],
    }),
    outcome: new FormControl<MaterialHistoryOutcome>('PerfectDelivery', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    remark: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(2000)],
    }),
  });

  readonly items = signal<readonly MaterialHistoryItem[]>([]);

  readonly materialOptions = signal<readonly MaterialHistoryMaterialOption[]>([]);

  readonly summary = signal<MaterialHistorySummary>({
    totalEntries: 0,
    perfectDeliveries: 0,
    reworkDeliveries: 0,
    rejected: 0,
  });

  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly totalRecords = signal(0);
  readonly totalPages = signal(1);
  readonly sortColumn = signal<MaterialHistoryExcelFilterColumn | null>(null);
  readonly sortDirection = signal<MaterialHistorySortDirection | null>(null);
  readonly excelFilterSelections = signal<MaterialHistoryExcelFilterSelections>({});
  readonly excelFilterOptions = signal<
    Partial<Record<MaterialHistoryExcelFilterColumn, readonly MaterialHistoryExcelFilterValue[]>>
  >({});
  readonly excelFilterLoadingColumns = signal<ReadonlySet<MaterialHistoryExcelFilterColumn>>(
    new Set<MaterialHistoryExcelFilterColumn>(),
  );
  readonly loading = signal(false);
  readonly optionsLoading = signal(false);
  readonly saving = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly formVisible = signal(false);
  readonly errorMessage = signal('');
  readonly formErrorMessage = signal('');
  readonly successMessage = signal('');

  readonly isEditing = computed(() => this.editingId() !== null);

  readonly hasPreviousPage = computed(() => this.page() > 1);

  readonly hasNextPage = computed(() => this.page() < this.totalPages());

  readonly showingFrom = computed(() =>
    this.totalRecords() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );

  readonly showingTo = computed(() => Math.min(this.page() * this.pageSize(), this.totalRecords()));

  constructor() {
    this.loadMaterialOptions();
  }

  startCreate(): void {
    this.editingId.set(null);
    this.form.reset({
      materialId: this.materialFilterControl.value,
      occurredDate: this.todayInputValue(),
      outcome: 'PerfectDelivery',
      remark: '',
    });
    this.formErrorMessage.set('');
    this.successMessage.set('');
    this.formVisible.set(true);
  }

  startEdit(item: MaterialHistoryItem): void {
    this.editingId.set(item.id);
    this.form.reset({
      materialId: item.materialId,
      occurredDate: item.occurredDate.slice(0, 10),
      outcome: item.outcome,
      remark: item.remark ?? '',
    });
    this.formErrorMessage.set('');
    this.successMessage.set('');
    this.formVisible.set(true);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  cancelForm(): void {
    if (this.saving()) {
      return;
    }

    this.editingId.set(null);
    this.formVisible.set(false);
    this.formErrorMessage.set('');
  }

  save(): void {
    if (
      this.form.controls.outcome.value !== 'Rejected' &&
      this.form.controls.remark.hasError('rejectionReasonRequired')
    ) {
      const remainingErrors = { ...this.form.controls.remark.errors };

      delete remainingErrors['rejectionReasonRequired'];
      this.form.controls.remark.setErrors(
        Object.keys(remainingErrors).length > 0 ? remainingErrors : null,
      );
    }

    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const values = this.form.getRawValue();
    const remark = values.remark.trim();

    if (values.outcome === 'Rejected' && !remark) {
      this.form.controls.remark.setErrors({ rejectionReasonRequired: true });
      this.form.controls.remark.markAsTouched();
      this.formErrorMessage.set('Please enter why this material was rejected.');
      return;
    }

    const request: MaterialHistoryUpsertRequest = {
      materialId: Number(values.materialId),
      occurredDate: values.occurredDate,
      outcome: values.outcome,
      remark: remark || null,
    };

    this.saving.set(true);
    this.formErrorMessage.set('');
    this.successMessage.set('');

    const editingId = this.editingId();
    const operation: Observable<MaterialHistorySaveResponse> =
      editingId === null ? this.api.create(request) : this.api.update(editingId, request);

    operation
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(response.message);
          this.editingId.set(null);
          this.formVisible.set(false);
          this.load();
        },
        error: (error: HttpErrorResponse) => {
          this.formErrorMessage.set(
            this.describeError(error, 'The material history entry could not be saved.'),
          );
        },
      });
  }

  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  clearFilters(): void {
    this.searchControl.setValue('');
    this.outcomeFilterControl.setValue('');
    this.materialFilterControl.setValue(null);
    this.page.set(1);
    this.load();
  }

  excelFilterOptionsFor(
    column: MaterialHistoryExcelFilterColumn,
  ): readonly MaterialHistoryExcelFilterValue[] {
    return this.excelFilterOptions()[column] ?? [];
  }

  excelFilterSelectedValues(column: MaterialHistoryExcelFilterColumn): readonly string[] {
    return this.excelFilterSelections()[column] ?? [];
  }

  excelFilterActive(column: MaterialHistoryExcelFilterColumn): boolean {
    return Object.prototype.hasOwnProperty.call(this.excelFilterSelections(), column);
  }

  excelFilterLoading(column: MaterialHistoryExcelFilterColumn): boolean {
    return this.excelFilterLoadingColumns().has(column);
  }

  excelSortDirection(
    column: MaterialHistoryExcelFilterColumn,
  ): MaterialHistorySortDirection | null {
    return this.sortColumn() === column ? this.sortDirection() : null;
  }

  loadExcelFilterValues(column: MaterialHistoryExcelFilterColumn): void {
    if (this.excelFilterLoading(column)) {
      return;
    }

    this.excelFilterLoadingColumns.update((columns) => {
      const updated = new Set(columns);

      updated.add(column);

      return updated;
    });

    this.api
      .getExcelFilterValues(this.buildHistoryQuery(false), column)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() =>
          this.excelFilterLoadingColumns.update((columns) => {
            const updated = new Set(columns);

            updated.delete(column);

            return updated;
          }),
        ),
      )
      .subscribe({
        next: (options) => {
          this.excelFilterOptions.update((current) => ({
            ...current,
            [column]: options,
          }));
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            this.describeError(error, 'Material history filter values could not be loaded.'),
          );
        },
      });
  }

  applyExcelSort(
    column: MaterialHistoryExcelFilterColumn,
    direction: MaterialHistorySortDirection | null,
  ): void {
    if (this.loading()) {
      return;
    }

    this.sortColumn.set(direction === null ? null : column);
    this.sortDirection.set(direction);
    this.page.set(1);
    this.load();
  }

  applyExcelFilter(column: MaterialHistoryExcelFilterColumn, values: readonly string[]): void {
    const availableValues = this.excelFilterOptions()[column] ?? [];
    const selectedSet = new Set(values);
    const selectsEverything =
      availableValues.length > 0 &&
      availableValues.length === selectedSet.size &&
      availableValues.every((option) => selectedSet.has(option.value));

    this.excelFilterSelections.update((current) => {
      const updated = { ...current };

      if (selectsEverything) {
        delete updated[column];
      } else {
        updated[column] = [...values];
      }

      return updated;
    });

    this.page.set(1);
    this.load();
  }

  clearExcelFilter(column: MaterialHistoryExcelFilterColumn): void {
    this.excelFilterSelections.update((current) => {
      const updated = { ...current };

      delete updated[column];

      return updated;
    });

    this.page.set(1);
    this.load();
  }

  previousPage(): void {
    if (!this.hasPreviousPage() || this.loading()) {
      return;
    }

    this.page.update((value) => value - 1);
    this.load();
  }

  nextPage(): void {
    if (!this.hasNextPage() || this.loading()) {
      return;
    }

    this.page.update((value) => value + 1);
    this.load();
  }

  changePageSize(value: string): void {
    this.pageSize.set(Number(value) || 25);
    this.page.set(1);
    this.load();
  }

  outcomeLabel(outcome: MaterialHistoryOutcome): string {
    switch (outcome) {
      case 'PerfectDelivery':
        return 'Perfect delivery';
      case 'ReworkAndDelivery':
        return 'Rework and delivery';
      case 'Rejected':
        return 'Rejected';
    }
  }

  outcomeClass(outcome: MaterialHistoryOutcome): string {
    switch (outcome) {
      case 'PerfectDelivery':
        return 'perfect';
      case 'ReworkAndDelivery':
        return 'rework';
      case 'Rejected':
        return 'rejected';
    }
  }

  private loadMaterialOptions(): void {
    this.optionsLoading.set(true);

    this.api
      .getMaterialOptions()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.optionsLoading.set(false)),
      )
      .subscribe({
        next: (options) => {
          this.materialOptions.set(options);

          const routeMaterialId = Number(this.route.snapshot.queryParamMap.get('materialId'));

          if (Number.isInteger(routeMaterialId) && options.some((x) => x.id === routeMaterialId)) {
            this.materialFilterControl.setValue(routeMaterialId);
          }

          this.load();
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(this.describeError(error, 'Material choices could not be loaded.'));
          this.load();
        },
      });
  }

  private load(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.api
      .getHistory(this.buildHistoryQuery(true))
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.items.set(response.items);
          this.summary.set(response.summary);
          this.page.set(response.page);
          this.pageSize.set(response.pageSize);
          this.totalRecords.set(response.totalRecords);
          this.totalPages.set(response.totalPages);
        },
        error: (error: HttpErrorResponse) => {
          this.items.set([]);
          this.errorMessage.set(this.describeError(error, 'Material history could not be loaded.'));
        },
      });
  }

  private buildHistoryQuery(includePaging: boolean) {
    const selectedOutcome = this.outcomeFilterControl.value;
    const selectedMaterial = this.materialFilterControl.value;

    return {
      search: this.searchControl.value,
      outcome: selectedOutcome || undefined,
      materialId: selectedMaterial ?? undefined,
      sortColumn: this.sortColumn() ?? undefined,
      sortDirection: this.sortDirection() ?? undefined,
      excelFilters: this.serializeExcelFilters(),
      page: includePaging ? this.page() : 1,
      pageSize: this.pageSize(),
    };
  }

  private serializeExcelFilters(): string | undefined {
    const selections = this.excelFilterSelections();

    return Object.keys(selections).length > 0 ? JSON.stringify(selections) : undefined;
  }

  private describeError(error: HttpErrorResponse, fallback: string): string {
    const apiMessage =
      typeof error.error?.error === 'string'
        ? error.error.error
        : typeof error.error?.message === 'string'
          ? error.error.message
          : '';

    if (apiMessage) {
      return apiMessage;
    }

    if (error.status === 401) {
      return 'Your session has expired. Please sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to change material history.';
    }

    return fallback;
  }

  private todayInputValue(): string {
    const today = new Date();
    const offset = today.getTimezoneOffset();

    return new Date(today.getTime() - offset * 60_000).toISOString().slice(0, 10);
  }
}
