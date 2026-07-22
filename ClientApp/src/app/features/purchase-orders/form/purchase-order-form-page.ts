import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  ActivatedRoute,
  Router,
  RouterLink,
} from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize, forkJoin } from 'rxjs';
import {
  PurchaseOrderCrudDetails,
  PurchaseOrderPurchaserOption,
  PurchaseOrderUpsertRequest,
} from '../../../core/models/purchase-order-crud.model';
import {
  PurchaseOrderCrudApiService,
} from '../../../core/services/purchase-order-crud-api.service';

@Component({
  selector: 'app-purchase-order-form-page',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './purchase-order-form-page.html',
  styleUrl: './purchase-order-form-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PurchaseOrderFormPage implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly purchaseOrderApi =
    inject(PurchaseOrderCrudApiService);

  readonly purchaseOrderId = signal<number | null>(null);
  readonly purchasers =
    signal<PurchaseOrderPurchaserOption[]>([]);

  readonly isEditMode = signal(false);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal('');

  readonly form = this.formBuilder.nonNullable.group({
    purchaserId: [
      0,
      [
        Validators.required,
        Validators.min(1),
      ],
    ],
    poNumber: [
      '',
      [
        Validators.required,
        Validators.maxLength(100),
      ],
    ],
    poDate: [''],
    receivedDate: [''],
    buyerContactPerson: [
      '',
      [Validators.maxLength(200)],
    ],
  });

  ngOnInit(): void {
    const routeId =
      this.route.snapshot.paramMap.get('id');

    const parsedId = routeId
      ? Number(routeId)
      : null;

    if (
      parsedId !== null &&
      (!Number.isInteger(parsedId) || parsedId <= 0)
    ) {
      this.errorMessage.set(
        'Invalid purchase order number.',
      );
      this.isLoading.set(false);
      return;
    }

    this.purchaseOrderId.set(parsedId);
    this.isEditMode.set(parsedId !== null);

    if (parsedId === null) {
      this.loadCreateForm();
      return;
    }

    this.loadEditForm(parsedId);
  }

  submit(): void {
    this.errorMessage.set('');

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errorMessage.set(
        'Please complete all required fields correctly.',
      );
      return;
    }

    const request = this.buildRequest();
    const id = this.purchaseOrderId();

    this.isSaving.set(true);

    const operation = id === null
      ? this.purchaseOrderApi.create(request)
      : this.purchaseOrderApi.update(id, request);

    operation
      .pipe(
        finalize(() => {
          this.isSaving.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          void this.router.navigate(
            [
              '/purchase-orders',
              response.purchaseOrderId,
            ],
            {
              queryParams: {
                saved: 'true',
              },
            },
          );
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  cancelLink(): readonly (string | number)[] {
    const id = this.purchaseOrderId();

    return id === null
      ? ['/purchase-orders']
      : ['/purchase-orders', id];
  }

  fieldInvalid(
    fieldName: keyof typeof this.form.controls,
  ): boolean {
    const control = this.form.controls[fieldName];

    return (
      control.invalid &&
      (control.dirty || control.touched)
    );
  }

  private loadCreateForm(): void {
    this.isLoading.set(true);

    this.purchaseOrderApi
      .getPurchaserOptions()
      .pipe(
        finalize(() => {
          this.isLoading.set(false);
        }),
      )
      .subscribe({
        next: (options) => {
          this.purchasers.set(options);

          if (options.length === 1) {
            this.form.controls.purchaserId.setValue(
              options[0].id,
            );
          }
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  private loadEditForm(id: number): void {
    this.isLoading.set(true);

    forkJoin({
      purchaseOrder:
        this.purchaseOrderApi.getDetails(id),
      purchasers:
        this.purchaseOrderApi.getPurchaserOptions(),
    })
      .pipe(
        finalize(() => {
          this.isLoading.set(false);
        }),
      )
      .subscribe({
        next: ({ purchaseOrder, purchasers }) => {
          this.purchasers.set(purchasers);
          this.patchForm(purchaseOrder);
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  private patchForm(
    purchaseOrder: PurchaseOrderCrudDetails,
  ): void {
    this.form.patchValue({
      purchaserId:
        purchaseOrder.purchaserId,
      poNumber:
        purchaseOrder.poNumber ?? '',
      poDate:
        this.toDateInput(purchaseOrder.poDate),
      receivedDate:
        this.toDateInput(
          purchaseOrder.receivedDate,
        ),
      buyerContactPerson:
        purchaseOrder.buyerContactPerson ?? '',
    });
  }

  private buildRequest():
    PurchaseOrderUpsertRequest {
    const value = this.form.getRawValue();

    return {
      purchaserId:
        value.purchaserId,
      poNumber:
        value.poNumber.trim(),
      poDate:
        this.nullIfEmpty(value.poDate),
      receivedDate:
        this.nullIfEmpty(value.receivedDate),
      buyerContactPerson:
        this.nullIfEmpty(
          value.buyerContactPerson,
        ),
    };
  }

  private nullIfEmpty(
    value: string,
  ): string | null {
    const cleaned = value.trim();

    return cleaned.length > 0
      ? cleaned
      : null;
  }

  private toDateInput(
    value: string | null,
  ): string {
    if (!value) {
      return '';
    }

    return value.substring(0, 10);
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

      return 'Please check the entered purchase order values.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Please sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to save purchase orders.';
    }

    if (error.status === 404) {
      return this.isEditMode()
        ? 'Purchase order was not found.'
        : 'The selected purchaser was not found.';
    }

    if (error.status === 409) {
      return (
        error.error?.message ||
        'This PO number already exists.'
      );
    }

    const apiMessage =
      error.error?.message ??
      error.error?.error;

    return typeof apiMessage === 'string'
      ? apiMessage
      : 'Purchase order could not be saved.';
  }
}
