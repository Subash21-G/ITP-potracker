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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { ManageOnlyDirective } from '../../../core/directives/manage-only.directive';
import { InvoiceDetails } from '../../../core/models/invoice-api.model';
import { InvoiceApiService } from '../../../core/services/invoice-api.service';

@Component({
  selector: 'app-invoice-details-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ManageOnlyDirective,
    RouterLink,
  ],
  templateUrl: './invoice-details-page.html',
  styleUrl: './invoice-details-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceDetailsPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly invoiceApi = inject(InvoiceApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly invoice = signal<InvoiceDetails | null>(null);
  readonly loading = signal(false);
  readonly deleting = signal(false);
  readonly errorMessage = signal('');

  readonly itemCount = computed(() => this.invoice()?.items.length ?? 0);

  private readonly invoiceId = Number(
    this.route.snapshot.paramMap.get('id'),
  );

  constructor() {
    if (!Number.isInteger(this.invoiceId) || this.invoiceId <= 0) {
      this.errorMessage.set('The invoice ID in the address is invalid.');
      return;
    }

    this.loadInvoice();
  }

  retry(): void {
    this.loadInvoice();
  }

  deleteInvoice(): void {
    const invoice = this.invoice();

    if (!invoice || this.deleting()) {
      return;
    }

    const confirmed = window.confirm(
      `Delete invoice ${invoice.invoiceNumber}?\n\n` +
        'The linked material invoice and dispatch quantities will be restored.',
    );

    if (!confirmed) {
      return;
    }

    this.deleting.set(true);
    this.errorMessage.set('');

    this.invoiceApi
      .deleteInvoice(invoice.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.deleting.set(false)),
      )
      .subscribe({
        next: () => {
          void this.router.navigate(['/invoices'], {
            replaceUrl: true,
          });
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(this.describeDeleteError(error));
        },
      });
  }

  private loadInvoice(): void {
    if (this.loading() || this.invoiceId <= 0) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.invoiceApi
      .getDetails(this.invoiceId)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (invoice) => {
          this.invoice.set(invoice);
        },
        error: (error: HttpErrorResponse) => {
          this.invoice.set(null);
          this.errorMessage.set(this.describeLoadError(error));
        },
      });
  }

  private describeLoadError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable. Start the ASP.NET application and try again.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Your account does not have permission to view invoices.';
    }

    if (error.status === 404) {
      return 'The selected invoice was not found.';
    }

    return this.readApiMessage(error) || 'Invoice details could not be loaded.';
  }

  private describeDeleteError(error: HttpErrorResponse): string {
    if (error.status === 401) {
      return 'Your session has expired. Sign in again before deleting the invoice.';
    }

    if (error.status === 403) {
      return 'Only Admin and Production users can delete invoices.';
    }

    if (error.status === 404) {
      return 'The selected invoice no longer exists.';
    }

    return this.readApiMessage(error) || 'The invoice could not be deleted.';
  }

  private readApiMessage(error: HttpErrorResponse): string {
    const message = error.error?.message ?? error.error?.error;

    return typeof message === 'string' ? message : '';
  }
}
