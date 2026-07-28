import {
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
  ReactiveFormsModule,
} from '@angular/forms';
import {
  RouterLink,
} from '@angular/router';
import {
  finalize,
} from 'rxjs';

import {
  ManageOnlyDirective,
} from '../../core/directives/manage-only.directive';
import {
  PurchaserListItem,
} from '../../core/models/purchaser-api.model';
import {
  AuthApiService,
} from '../../core/services/auth-api.service';
import {
  PurchaserApiService,
} from '../../core/services/purchaser-api.service';
import {
  ToastService,
} from '../../core/services/toast.service';

@Component({
  selector: 'app-purchasers-page',
  imports: [
    DecimalPipe,
    ManageOnlyDirective,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './purchasers-page.html',
  styleUrl: './purchasers-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class PurchasersPage {
  private readonly purchaserApi =
    inject(PurchaserApiService);

  private readonly authApi =
    inject(AuthApiService);

  private readonly toastService =
    inject(ToastService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly searchControl =
    new FormControl(
      '',
      {
        nonNullable: true,
      },
    );

  readonly items =
    signal<readonly PurchaserListItem[]>([]);

  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly deletingId =
    signal<number | null>(null);

  readonly errorMessage = signal('');

  readonly canOperate =
    this.authApi.canOperate;

  readonly linkedPurchaseOrders =
    computed(() =>
      this.items().reduce(
        (total, item) =>
          total +
          item.purchaseOrderCount,
        0,
      ),
    );

  readonly linkedPurchasers =
    computed(() =>
      this.items().filter(
        (item) =>
          item.purchaseOrderCount > 0,
      ).length,
    );

  readonly standalonePurchasers =
    computed(() =>
      this.items().filter(
        (item) =>
          item.purchaseOrderCount === 0,
      ).length,
    );

  constructor() {
    this.loadPurchasers();
  }

  search(): void {
    this.loadPurchasers();
  }

  clearSearch(): void {
    this.searchControl.setValue('');
    this.loadPurchasers();
  }

  refresh(): void {
    this.loadPurchasers();
  }

  deletePurchaser(
    purchaser: PurchaserListItem,
  ): void {
    if (
      purchaser.purchaseOrderCount > 0
    ) {
      this.toastService.warning(
        'Purchaser is in use',
        `This purchaser is linked to ${purchaser.purchaseOrderCount} Purchase Order record(s) and cannot be deleted.`,
      );
      return;
    }

    const confirmed =
      window.confirm(
        `Delete purchaser "${purchaser.purchaserName}"?`,
      );

    if (!confirmed) {
      return;
    }

    this.deletingId.set(
      purchaser.id,
    );

    this.purchaserApi
      .delete(purchaser.id)
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() => {
          this.deletingId.set(null);
        }),
      )
      .subscribe({
        next: (response) => {
          this.toastService.success(
            'Purchaser deleted',
            response.message,
          );

          this.loadPurchasers();
        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.toastService.error(
            'Delete failed',
            this.describeError(error),
          );
        },
      });
  }

  private loadPurchasers(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.purchaserApi
      .getPurchasers(
        this.searchControl.value,
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
        finalize(() => {
          this.loading.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          this.items.set(
            response.items,
          );

          this.totalCount.set(
            response.totalCount,
          );
        },
        error: (
          error: HttpErrorResponse,
        ) => {
          this.items.set([]);
          this.totalCount.set(0);

          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  private describeError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Please sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to manage purchasers.';
    }

    if (error.status === 404) {
      return 'The purchaser was not found.';
    }

    const apiMessage =
      error.error?.error ??
      error.error?.message;

    return typeof apiMessage ===
      'string'
      ? apiMessage
      : 'Purchasers could not be loaded.';
  }
}
