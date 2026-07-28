import {
  HttpErrorResponse,
} from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
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
import {
  finalize,
} from 'rxjs';

import {
  PurchaserDetails,
  PurchaserSaveRequest,
} from '../../../core/models/purchaser-api.model';
import {
  PurchaserApiService,
} from '../../../core/services/purchaser-api.service';
import {
  ToastService,
} from '../../../core/services/toast.service';

@Component({
  selector: 'app-purchaser-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './purchaser-form-page.html',
  styleUrl: './purchaser-form-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class PurchaserFormPage
  implements OnInit {
  private readonly formBuilder =
    inject(FormBuilder);

  private readonly route =
    inject(ActivatedRoute);

  private readonly router =
    inject(Router);

  private readonly purchaserApi =
    inject(PurchaserApiService);

  private readonly toastService =
    inject(ToastService);

  readonly purchaserId =
    signal<number | null>(null);

  readonly isEditMode =
    signal(false);

  readonly isLoading =
    signal(false);

  readonly isSaving =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly purchaseOrderCount =
    signal(0);

  readonly form =
    this.formBuilder.nonNullable.group({
      purchaserName: [
        '',
        [
          Validators.required,
          Validators.maxLength(200),
        ],
      ],

      contactPerson: [
        '',
        [
          Validators.maxLength(200),
        ],
      ],

      mobileNo: [
        '',
        [
          Validators.maxLength(50),
        ],
      ],

      email: [
        '',
        [
          Validators.email,
          Validators.maxLength(250),
        ],
      ],

      address: [
        '',
        [
          Validators.maxLength(1000),
        ],
      ],
    });

  ngOnInit(): void {
    const routeId =
      this.route.snapshot.paramMap.get(
        'id',
      );

    if (!routeId) {
      return;
    }

    const parsedId = Number(routeId);

    if (
      !Number.isInteger(parsedId) ||
      parsedId <= 0
    ) {
      this.errorMessage.set(
        'Invalid purchaser number.',
      );
      return;
    }

    this.purchaserId.set(parsedId);
    this.isEditMode.set(true);

    this.loadPurchaser(parsedId);
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

    const request =
      this.buildRequest();

    const purchaserId =
      this.purchaserId();

    this.isSaving.set(true);

    const operation =
      purchaserId === null
        ? this.purchaserApi.create(
            request,
          )
        : this.purchaserApi.update(
            purchaserId,
            request,
          );

    operation
      .pipe(
        finalize(() => {
          this.isSaving.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          this.toastService.success(
            this.isEditMode()
              ? 'Purchaser updated'
              : 'Purchaser created',
            response.message,
          );

          void this.router.navigate(
            ['/purchasers'],
          );
        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  fieldInvalid(
    fieldName:
      keyof typeof this.form.controls,
  ): boolean {
    const control =
      this.form.controls[fieldName];

    return (
      control.invalid &&
      (
        control.dirty ||
        control.touched
      )
    );
  }

  private loadPurchaser(
    id: number,
  ): void {
    this.isLoading.set(true);

    this.purchaserApi
      .getDetails(id)
      .pipe(
        finalize(() => {
          this.isLoading.set(false);
        }),
      )
      .subscribe({
        next: (
          purchaser:
            PurchaserDetails,
        ) => {
          this.purchaseOrderCount.set(
            purchaser.purchaseOrderCount,
          );

          this.form.patchValue({
            purchaserName:
              purchaser.purchaserName,

            contactPerson:
              purchaser.contactPerson,

            mobileNo:
              purchaser.mobileNo,

            email:
              purchaser.email,

            address:
              purchaser.address,
          });
        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  private buildRequest():
    PurchaserSaveRequest {
    const value =
      this.form.getRawValue();

    return {
      purchaserName:
        value.purchaserName.trim(),

      contactPerson:
        this.nullIfEmpty(
          value.contactPerson,
        ),

      mobileNo:
        this.nullIfEmpty(
          value.mobileNo,
        ),

      email:
        this.nullIfEmpty(
          value.email,
        ),

      address:
        this.nullIfEmpty(
          value.address,
        ),
    };
  }

  private nullIfEmpty(
    value: string,
  ): string | null {
    const cleaned = value.trim();

    return cleaned
      ? cleaned
      : null;
  }

  private describeError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 400) {
      const validationErrors =
        error.error?.errors;

      if (
        validationErrors &&
        typeof validationErrors ===
          'object'
      ) {
        const messages =
          Object.values(
            validationErrors,
          )
            .flatMap((value) =>
              Array.isArray(value)
                ? value
                : [String(value)],
            )
            .filter(
              (
                value,
              ): value is string =>
                typeof value ===
                'string',
            );

        if (messages.length > 0) {
          return messages.join(' ');
        }
      }

      return 'Please check the entered purchaser information.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Please sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to save purchasers.';
    }

    if (error.status === 404) {
      return 'The purchaser was not found.';
    }

    if (error.status === 409) {
      return (
        error.error?.error ??
        'A purchaser with this name already exists.'
      );
    }

    const apiMessage =
      error.error?.error ??
      error.error?.message;

    return typeof apiMessage ===
      'string'
      ? apiMessage
      : 'Purchaser could not be saved.';
  }
}
