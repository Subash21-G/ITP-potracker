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
  MaterialDetails,
  MaterialPurchaseOrderOption,
  MaterialUpsertRequest,
} from '../../../core/models/material-crud.model';
import {
  MaterialCrudApiService,
} from '../../../core/services/material-crud-api.service';

@Component({
  selector: 'app-material-form-page',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './material-form-page.html',
  styleUrl: './material-form-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaterialFormPage implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly materialApi =
    inject(MaterialCrudApiService);

  readonly materialId = signal<number | null>(null);
  readonly purchaseOrders =
    signal<MaterialPurchaseOrderOption[]>([]);

  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');

  readonly isEditMode = signal(false);

  readonly form = this.formBuilder.nonNullable.group({
    purchaseOrderId: [
      0,
      [
        Validators.required,
        Validators.min(1),
      ],
    ],

    lineItem: [''],
    itemType: [''],
    materialNo: [''],
    hsnSacCode: [''],
    shortDescription: [''],
    briefDescription: [''],
    receiver: [''],
    unloadingPoint: [''],

    qty: [
      0,
      [
        Validators.required,
        Validators.min(0.01),
      ],
    ],
    unit: [''],
    dep: [''],
    stockQty: [0, [Validators.min(0)]],

    cost: [0, [Validators.min(0)]],
    valueOfGoods: [0, [Validators.min(0)]],
    priceUnit: [''],
    totalValue: [0, [Validators.min(0)]],

    rmGrade: [''],
    rmSource: [''],
    rmProcess: [''],

    surfaceFinish: [''],
    machiningType: [''],

    processTime: [''],
    materialProcessTime: [''],

    poDate: [''],
    receivingDate: [''],
    deliveryDate: [''],
    dispatchDate: [''],
    drawingUpdatedDate: [''],

    drawingNo: [''],
    drawingVersion: [''],

    processStatus: ['Pending'],
    assignedDashboard: [''],
    remark: [''],
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
        'Invalid material number.',
      );
      this.isLoading.set(false);
      return;
    }

    this.materialId.set(parsedId);
    this.isEditMode.set(parsedId !== null);

    if (parsedId === null) {
      this.loadCreateForm();
      return;
    }

    this.loadEditForm(parsedId);
  }

  submit(): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errorMessage.set(
        'Please complete all required fields correctly.',
      );
      return;
    }

    const request = this.buildRequest();
    const id = this.materialId();

    this.isSaving.set(true);

    const operation =
      id === null
        ? this.materialApi.create(request)
        : this.materialApi.update(id, request);

    operation
      .pipe(
        finalize(() => {
          this.isSaving.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          this.successMessage.set(
            response.message,
          );

          void this.router.navigate(
            ['/materials', response.materialId],
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
    const id = this.materialId();

    return id === null
      ? ['/materials']
      : ['/materials', id];
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

    this.materialApi
      .getPurchaseOrderOptions()
      .pipe(
        finalize(() => {
          this.isLoading.set(false);
        }),
      )
      .subscribe({
        next: (options) => {
          this.purchaseOrders.set(options);

          if (options.length === 1) {
            this.form.controls.purchaseOrderId.setValue(
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
      material:
        this.materialApi.getDetails(id),
      options:
        this.materialApi.getPurchaseOrderOptions(),
    })
      .pipe(
        finalize(() => {
          this.isLoading.set(false);
        }),
      )
      .subscribe({
        next: ({ material, options }) => {
          this.purchaseOrders.set(options);
          this.patchForm(material);
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  private patchForm(
    material: MaterialDetails,
  ): void {
    this.form.patchValue({
      purchaseOrderId:
        material.purchaseOrderId,

      lineItem:
        material.lineItem ?? '',
      itemType:
        material.itemType ?? '',
      materialNo:
        material.materialNo ?? '',
      hsnSacCode:
        material.hsnSacCode ?? '',
      shortDescription:
        material.shortDescription ?? '',
      briefDescription:
        material.briefDescription ?? '',
      receiver:
        material.receiver ?? '',
      unloadingPoint:
        material.unloadingPoint ?? '',

      qty:
        material.qty ?? 0,
      unit:
        material.unit ?? '',
      dep:
        material.dep ?? '',
      stockQty:
        material.stockQty ?? 0,

      cost:
        material.cost ?? 0,
      valueOfGoods:
        material.valueOfGoods ?? 0,
      priceUnit:
        material.priceUnit ?? '',
      totalValue:
        material.totalValue ?? 0,

      rmGrade:
        material.rmGrade ?? '',
      rmSource:
        material.rmSource ?? '',
      rmProcess:
        material.rmProcess ?? '',

      surfaceFinish:
        material.surfaceFinish ?? '',
      machiningType:
        material.machiningType ?? '',

      processTime:
        material.processTime ?? '',
      materialProcessTime:
        material.materialProcessTime ?? '',

      poDate:
        this.toDateInput(material.poDate),
      receivingDate:
        this.toDateInput(material.receivingDate),
      deliveryDate:
        this.toDateInput(material.deliveryDate),
      dispatchDate:
        this.toDateInput(material.dispatchDate),
      drawingUpdatedDate:
        this.toDateInput(
          material.drawingUpdatedDate,
        ),

      drawingNo:
        material.drawingNo ?? '',
      drawingVersion:
        material.drawingVersion ?? '',

      processStatus:
        material.processStatus || 'Pending',
      assignedDashboard:
        material.assignedDashboard ?? '',
      remark:
        material.remark ?? '',
    });
  }

  private buildRequest():
    MaterialUpsertRequest {
    const value = this.form.getRawValue();

    return {
      purchaseOrderId:
        value.purchaseOrderId,

      lineItem:
        this.nullIfEmpty(value.lineItem),
      itemType:
        this.nullIfEmpty(value.itemType),
      materialNo:
        this.nullIfEmpty(value.materialNo),
      hsnSacCode:
        this.nullIfEmpty(value.hsnSacCode),
      shortDescription:
        this.nullIfEmpty(
          value.shortDescription,
        ),
      briefDescription:
        this.nullIfEmpty(
          value.briefDescription,
        ),
      receiver:
        this.nullIfEmpty(value.receiver),
      unloadingPoint:
        this.nullIfEmpty(
          value.unloadingPoint,
        ),

      qty:
        Number(value.qty) || 0,
      unit:
        this.nullIfEmpty(value.unit),
      dep:
        this.nullIfEmpty(value.dep),
      stockQty:
        Number(value.stockQty) || 0,

      cost:
        Number(value.cost) || 0,
      valueOfGoods:
        Number(value.valueOfGoods) || 0,
      priceUnit:
        this.nullIfEmpty(value.priceUnit),
      totalValue:
        Number(value.totalValue) || 0,

      rmGrade:
        this.nullIfEmpty(value.rmGrade),
      rmSource:
        this.nullIfEmpty(value.rmSource),
      rmProcess:
        this.nullIfEmpty(value.rmProcess),

      surfaceFinish:
        this.nullIfEmpty(
          value.surfaceFinish,
        ),
      machiningType:
        this.nullIfEmpty(
          value.machiningType,
        ),

      processTime:
        this.nullIfEmpty(value.processTime),
      materialProcessTime:
        this.nullIfEmpty(
          value.materialProcessTime,
        ),

      poDate:
        this.nullIfEmpty(value.poDate),
      receivingDate:
        this.nullIfEmpty(
          value.receivingDate,
        ),
      deliveryDate:
        this.nullIfEmpty(
          value.deliveryDate,
        ),
      dispatchDate:
        this.nullIfEmpty(
          value.dispatchDate,
        ),
      drawingUpdatedDate:
        this.nullIfEmpty(
          value.drawingUpdatedDate,
        ),

      drawingNo:
        this.nullIfEmpty(value.drawingNo),
      drawingVersion:
        this.nullIfEmpty(
          value.drawingVersion,
        ),

      processStatus:
        this.nullIfEmpty(
          value.processStatus,
        ),
      assignedDashboard:
        this.nullIfEmpty(
          value.assignedDashboard,
        ),
      remark:
        this.nullIfEmpty(value.remark),
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

      if (
        errors &&
        typeof errors === 'object'
      ) {
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

      return 'Please check the entered material values.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Please sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to save materials.';
    }

    if (error.status === 404) {
      return this.isEditMode()
        ? 'Material was not found.'
        : 'The selected purchase order was not found.';
    }

    const apiMessage =
      error.error?.message ??
      error.error?.error;

    return typeof apiMessage === 'string'
      ? apiMessage
      : 'Material could not be saved.';
  }
}
