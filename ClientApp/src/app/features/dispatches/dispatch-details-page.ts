import {
  DatePipe,
  DecimalPipe,
} from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
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
  ActivatedRoute,
  Router,
  RouterLink,
} from '@angular/router';
import { finalize } from 'rxjs';

import {
  ManageOnlyDirective,
} from '../../core/directives/manage-only.directive';
import {
  DispatchDetailsResponse,
  DispatchHistoryItem,
} from '../../core/models/dispatch-api.model';
import {
  DispatchApiService,
} from '../../core/services/dispatch-api.service';

function todayValue(): string {
  const date = new Date();
  const offset =
    date.getTimezoneOffset() * 60_000;

  return new Date(
    date.getTime() - offset,
  )
    .toISOString()
    .slice(0, 10);
}

@Component({
  selector: 'app-dispatch-details-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ManageOnlyDirective,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './dispatch-details-page.html',
  styleUrls: [
    '../invoices/invoices-page.css',
    './dispatch-details-page.css',
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DispatchDetailsPage {
  private readonly dispatchApi =
    inject(DispatchApiService);

  private readonly route =
    inject(ActivatedRoute);

  private readonly router =
    inject(Router);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly details =
    signal<DispatchDetailsResponse | null>(null);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly deletingId = signal<number | null>(null);
  readonly bulkDeleting = signal(false);

  readonly selectedIds =
    signal<ReadonlySet<number>>(new Set());

  readonly selectedCount =
    computed(() => this.selectedIds().size);

  readonly allSelected = computed(() => {
    const history =
      this.details()?.dispatches ?? [];

    return (
      history.length > 0 &&
      history.every((item) =>
        this.selectedIds().has(item.id))
    );
  });

  readonly errorMessage = signal('');
  readonly successMessage = signal('');
  readonly selectedFile = signal<File | null>(null);

  readonly form = new FormGroup({
    dispatchNo: new FormControl(
      '',
      {
        nonNullable: true,
        validators: [
          Validators.required,
        ],
      },
    ),

    dispatchDate: new FormControl(
      todayValue(),
      {
        nonNullable: true,
        validators: [
          Validators.required,
        ],
      },
    ),

    dispatchQuantity: new FormControl(
      0,
      {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.min(0.01),
        ],
      },
    ),

    remarks: new FormControl(
      '',
      {
        nonNullable: true,
      },
    ),
  });

  private readonly materialId =
    Number(
      this.route.snapshot.paramMap.get(
        'materialId',
      ),
    );

  constructor() {
    if (
      !Number.isInteger(this.materialId) ||
      this.materialId <= 0
    ) {
      void this.router.navigate([
        '/dispatches',
      ]);

      return;
    }

    this.loadDetails();
  }

  onFileSelected(event: Event): void {
    const input =
      event.target as HTMLInputElement;

    this.selectedFile.set(
      input.files?.[0] ?? null,
    );
  }

  submitDispatch(): void {
    const details = this.details();

    if (
      !details ||
      this.saving()
    ) {
      return;
    }

    this.form.markAllAsTouched();

    if (this.form.invalid) {
      this.errorMessage.set(
        'Enter Dispatch No, date and a quantity greater than zero.',
      );

      return;
    }

    const values =
      this.form.getRawValue();

    if (
      values.dispatchQuantity >
      details.material.pendingQuantity
    ) {
      this.errorMessage.set(
        `Dispatch quantity cannot exceed the pending quantity ${details.material.pendingQuantity}.`,
      );

      return;
    }

    this.saving.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.dispatchApi
      .createDispatch({
        materialId: this.materialId,
        dispatchNo: values.dispatchNo,
        dispatchDate: values.dispatchDate,
        dispatchQuantity:
          values.dispatchQuantity,
        remarks: values.remarks,
        dispatchFile:
          this.selectedFile(),
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),

        finalize(() =>
          this.saving.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(
            response.message,
          );

          this.form.reset({
            dispatchNo: '',
            dispatchDate: todayValue(),
            dispatchQuantity:
              response.pendingQuantity,
            remarks: '',
          });

          this.selectedFile.set(null);
          this.loadDetails(false);
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeMutationError(
              error,
              'The dispatch could not be saved.',
            ),
          );
        },
      });
  }

  deleteDispatch(
    dispatch: DispatchHistoryItem,
  ): void {
    if (
      this.deletingId() !== null ||
      this.bulkDeleting()
    ) {
      return;
    }

    const confirmed = window.confirm(
      `Delete dispatch ${dispatch.dispatchNo}?\n\n` +
      `${dispatch.dispatchQuantity} quantity will be restored to pending dispatch.`,
    );

    if (!confirmed) {
      return;
    }

    this.deletingId.set(dispatch.id);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.dispatchApi
      .deleteDispatch(dispatch.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),

        finalize(() =>
          this.deletingId.set(null)),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(
            response.message,
          );

          this.loadDetails(false);
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeMutationError(
              error,
              'The dispatch could not be deleted.',
            ),
          );
        },
      });
  }

  toggleDispatch(
    dispatchId: number,
    event: Event,
  ): void {
    const checked =
      (event.target as HTMLInputElement)
        .checked;

    this.selectedIds.update(
      (current) => {
        const updated =
          new Set(current);

        if (checked) {
          updated.add(dispatchId);
        } else {
          updated.delete(dispatchId);
        }

        return updated;
      },
    );
  }

  toggleAll(event: Event): void {
    const checked =
      (event.target as HTMLInputElement)
        .checked;

    if (!checked) {
      this.selectedIds.set(
        new Set(),
      );

      return;
    }

    this.selectedIds.set(
      new Set(
        this.details()
          ?.dispatches
          .map((item) => item.id) ??
          [],
      ),
    );
  }

  bulkDelete(): void {
    const dispatchIds =
      Array.from(this.selectedIds());

    if (
      dispatchIds.length === 0 ||
      this.bulkDeleting() ||
      this.deletingId() !== null
    ) {
      return;
    }

    const confirmed = window.confirm(
      `Delete ${dispatchIds.length} selected dispatch record(s)?\n\n` +
      'All selected quantities will be restored.',
    );

    if (!confirmed) {
      return;
    }

    this.bulkDeleting.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.dispatchApi
      .bulkDelete({
        materialId: this.materialId,
        dispatchIds,
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),

        finalize(() =>
          this.bulkDeleting.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(
            response.message,
          );

          this.selectedIds.set(
            new Set(),
          );

          this.loadDetails(false);
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeMutationError(
              error,
              'The selected dispatch records could not be deleted.',
            ),
          );
        },
      });
  }

  retry(): void {
    this.loadDetails();
  }

  private loadDetails(
    clearMessages = true,
  ): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);

    if (clearMessages) {
      this.errorMessage.set('');
      this.successMessage.set('');
    }

    this.dispatchApi
      .getDetails(this.materialId)
      .pipe(
        takeUntilDestroyed(this.destroyRef),

        finalize(() =>
          this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.details.set(response);

          this.selectedIds.set(
            new Set(),
          );

          if (
            !this.form.dirty ||
            this.form.controls
              .dispatchQuantity.value <= 0
          ) {
            this.form.controls
              .dispatchQuantity
              .setValue(
                response.material
                  .pendingQuantity,
              );
          }
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.details.set(null);

          this.errorMessage.set(
            this.describeLoadError(error),
          );
        },
      });
  }

  private describeLoadError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 404) {
      return 'The selected material was not found.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : 'Dispatch details could not be loaded.';
  }

  private describeMutationError(
    error: HttpErrorResponse,
    fallback: string,
  ): string {
    if (error.status === 403) {
      return 'Only Admin and Production users can change dispatch records.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : fallback;
  }
}
