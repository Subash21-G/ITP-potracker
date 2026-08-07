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
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  RouterLink,
} from '@angular/router';
import {
  Observable,
  finalize,
} from 'rxjs';

import {
  ManageOnlyDirective,
} from '../../core/directives/manage-only.directive';
import {
  LocalStockListItem,
  LocalStockSaveResponse,
  LocalStockSummary,
  LocalStockUpsertRequest,
} from '../../core/models/local-stock-api.model';
import {
  LocalStockApiService,
} from '../../core/services/local-stock-api.service';

@Component({
  selector: 'app-local-stocks-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ManageOnlyDirective,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './local-stocks-page.html',
  styleUrl: './local-stocks-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class LocalStocksPage {
  private readonly api =
    inject(LocalStockApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly searchControl =
    new FormControl(
      '',
      {
        nonNullable: true,
      },
    );

  readonly columnFilters =
    new FormGroup({
      material:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      rmGrade:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      location:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      minAvailableQty:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      maxAvailableQty:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      minMinimumQty:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      maxMinimumQty:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      status:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      updatedFrom:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      updatedTo:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),
    });

  readonly form =
    new FormGroup({
      materialNo:
        new FormControl(
          '',
          {
            nonNullable: true,
            validators: [
              Validators.required,
            ],
          },
        ),

      materialName:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      rmGrade:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      location:
        new FormControl(
          '',
          {
            nonNullable: true,
          },
        ),

      availableQty:
        new FormControl(
          0,
          {
            nonNullable: true,
          },
        ),

      minimumQty:
        new FormControl(
          0,
          {
            nonNullable: true,
          },
        ),
    });

  readonly items =
    signal<readonly LocalStockListItem[]>([]);

  readonly summary =
    signal<LocalStockSummary>({
      totalItems: 0,
      lowStockItems: 0,
      healthyItems: 0,
      updatedToday: 0,
    });

  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly totalRecords = signal(0);
  readonly totalPages = signal(1);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly deletingId =
    signal<number | null>(null);

  readonly editingId =
    signal<number | null>(null);

  readonly formVisible = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');

  readonly isEditing =
    computed(() =>
      this.editingId() !== null,
    );

  readonly hasPreviousPage =
    computed(() =>
      this.page() > 1,
    );

  readonly hasNextPage =
    computed(() =>
      this.page() <
      this.totalPages(),
    );

  readonly showingFrom =
    computed(() =>
      this.totalRecords() === 0
        ? 0
        : (
            this.page() - 1
          ) *
            this.pageSize() +
          1,
    );

  readonly showingTo =
    computed(() =>
      Math.min(
        this.page() *
          this.pageSize(),
        this.totalRecords(),
      ),
    );

  constructor() {
    this.load();
  }

  startCreate(): void {
    this.editingId.set(null);
    this.form.reset({
      materialNo: '',
      materialName: '',
      rmGrade: '',
      location: '',
      availableQty: 0,
      minimumQty: 0,
    });

    this.errorMessage.set('');
    this.successMessage.set('');
    this.formVisible.set(true);
  }

  startEdit(
    item: LocalStockListItem,
  ): void {
    this.editingId.set(
      item.id,
    );

    this.form.reset({
      materialNo:
        item.materialNo,
      materialName:
        item.materialName,
      rmGrade:
        item.rmGrade,
      location:
        item.location,
      availableQty:
        item.availableQty,
      minimumQty:
        item.minimumQty,
    });

    this.errorMessage.set('');
    this.successMessage.set('');
    this.formVisible.set(true);
  }

  cancelForm(): void {
    if (this.saving()) {
      return;
    }

    this.editingId.set(null);
    this.formVisible.set(false);
    this.form.reset();
  }

  save(): void {
    if (
      this.form.invalid ||
      this.saving()
    ) {
      this.form.markAllAsTouched();
      return;
    }

    const values =
      this.form.getRawValue();

    const request:
      LocalStockUpsertRequest = {
        materialNo:
          values.materialNo.trim(),

        materialName:
          this.normalizeOptional(
            values.materialName,
          ),

        rmGrade:
          this.normalizeOptional(
            values.rmGrade,
          ),

        location:
          this.normalizeOptional(
            values.location,
          ),

        availableQty:
          Number(
            values.availableQty,
          ) || 0,

        minimumQty:
          Number(
            values.minimumQty,
          ) || 0,
      };

    if (!request.materialNo) {
      this.form.controls.materialNo
        .setErrors({
          required: true,
        });

      return;
    }

    this.saving.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    const editingId =
      this.editingId();

    const operation:
      Observable<LocalStockSaveResponse> =
        editingId === null
          ? this.api.create(
              request,
            )
          : this.api.update(
              editingId,
              request,
            );

    operation
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() =>
          this.saving.set(false),
        ),
      )
      .subscribe({
        next: response => {
          this.successMessage.set(
            response.message,
          );

          this.editingId.set(null);
          this.formVisible.set(false);
          this.form.reset();
          this.load();
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
              'The local stock item could not be saved.',
            ),
          );
        },
      });
  }

  deleteStock(
    item: LocalStockListItem,
  ): void {
    if (
      this.deletingId() !== null ||
      this.loading()
    ) {
      return;
    }

    const confirmed =
      window.confirm(
        `Delete local stock item ${item.materialNo}?`,
      );

    if (!confirmed) {
      return;
    }

    this.deletingId.set(
      item.id,
    );

    this.errorMessage.set('');
    this.successMessage.set('');

    this.api
      .delete(
        item.id,
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() =>
          this.deletingId.set(null),
        ),
      )
      .subscribe({
        next: response => {
          if (
            this.items().length === 1 &&
            this.page() > 1
          ) {
            this.page.update(
              current =>
                current - 1,
            );
          }

          this.successMessage.set(
            response.message,
          );

          if (
            this.editingId() === item.id
          ) {
            this.cancelForm();
          }

          this.load();
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
              'The local stock item could not be deleted.',
            ),
          );
        },
      });
  }

  applySearch(): void {
    this.page.set(1);
    this.successMessage.set('');
    this.load();
  }

  applyColumnFilter(
    filter: HTMLDetailsElement,
  ): void {
    filter.open = false;
    this.page.set(1);
    this.successMessage.set('');
    this.load();
  }

  clearColumnFilter(
    column:
      | 'material'
      | 'grade'
      | 'location'
      | 'available'
      | 'minimum'
      | 'status'
      | 'updated',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'material':
        this.columnFilters.controls
          .material.setValue('');
        break;

      case 'grade':
        this.columnFilters.controls
          .rmGrade.setValue('');
        break;

      case 'location':
        this.columnFilters.controls
          .location.setValue('');
        break;

      case 'available':
        this.columnFilters.patchValue({
          minAvailableQty: '',
          maxAvailableQty: '',
        });
        break;

      case 'minimum':
        this.columnFilters.patchValue({
          minMinimumQty: '',
          maxMinimumQty: '',
        });
        break;

      case 'status':
        this.columnFilters.controls
          .status.setValue('');
        break;

      case 'updated':
        this.columnFilters.patchValue({
          updatedFrom: '',
          updatedTo: '',
        });
        break;
    }

    filter.open = false;
    this.page.set(1);
    this.successMessage.set('');
    this.load();
  }

  columnFilterActive(
    column:
      | 'material'
      | 'grade'
      | 'location'
      | 'available'
      | 'minimum'
      | 'status'
      | 'updated',
  ): boolean {
    const values =
      this.columnFilters.getRawValue();

    switch (column) {
      case 'material':
        return Boolean(
          values.material.trim(),
        );

      case 'grade':
        return Boolean(
          values.rmGrade.trim(),
        );

      case 'location':
        return Boolean(
          values.location.trim(),
        );

      case 'available':
        return Boolean(
          values.minAvailableQty.trim() ||
          values.maxAvailableQty.trim(),
        );

      case 'minimum':
        return Boolean(
          values.minMinimumQty.trim() ||
          values.maxMinimumQty.trim(),
        );

      case 'status':
        return Boolean(
          values.status,
        );

      case 'updated':
        return Boolean(
          values.updatedFrom ||
          values.updatedTo,
        );
    }

    return false;
  }

  clearSearch(): void {
    this.searchControl.setValue('');
    this.page.set(1);
    this.successMessage.set('');
    this.load();
  }

  changePageSize(
    event: Event,
  ): void {
    const value =
      Number(
        (
          event.target as
            HTMLSelectElement
        ).value,
      );

    if (
      ![
        10,
        25,
        50,
        100,
      ].includes(value)
    ) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.load();
  }

  previousPage(): void {
    if (
      !this.hasPreviousPage() ||
      this.loading()
    ) {
      return;
    }

    this.page.update(
      current =>
        current - 1,
    );

    this.load();
  }

  nextPage(): void {
    if (
      !this.hasNextPage() ||
      this.loading()
    ) {
      return;
    }

    this.page.update(
      current =>
        current + 1,
    );

    this.load();
  }

  retry(): void {
    this.load();
  }

  private load(): void {
    if (this.loading()) {
      return;
    }

    const columnValues =
      this.columnFilters.getRawValue();

    this.loading.set(true);
    this.errorMessage.set('');

    this.api
      .getStocks({
        search:
          this.searchControl.value,

        material:
          columnValues.material,

        rmGrade:
          columnValues.rmGrade,

        location:
          columnValues.location,

        minAvailableQty:
          columnValues.minAvailableQty,

        maxAvailableQty:
          columnValues.maxAvailableQty,

        minMinimumQty:
          columnValues.minMinimumQty,

        maxMinimumQty:
          columnValues.maxMinimumQty,

        status:
          columnValues.status,

        updatedFrom:
          columnValues.updatedFrom,

        updatedTo:
          columnValues.updatedTo,

        page:
          this.page(),

        pageSize:
          this.pageSize(),
      })
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() =>
          this.loading.set(false),
        ),
      )
      .subscribe({
        next: response => {
          this.items.set(
            response.items,
          );

          this.summary.set(
            response.summary,
          );

          this.page.set(
            response.page,
          );

          this.pageSize.set(
            response.pageSize,
          );

          this.totalRecords.set(
            response.totalRecords,
          );

          this.totalPages.set(
            response.totalPages,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.items.set([]);
          this.totalRecords.set(0);
          this.totalPages.set(1);

          this.errorMessage.set(
            this.describeError(
              error,
              'Local stock records could not be loaded.',
            ),
          );
        },
      });
  }

  private normalizeOptional(
    value: string,
  ): string | null {
    const normalized =
      value.trim();

    return normalized
      ? normalized
      : null;
  }

  private describeError(
    error: HttpErrorResponse,
    fallbackMessage: string,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Your account does not have permission to manage local stock.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message ===
      'string'
      ? message
      : fallbackMessage;
  }
}