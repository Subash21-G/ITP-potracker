import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { Quotation } from '../../core/api.models';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-quotations',
  imports: [CurrencyPipe, DatePipe, FormsModule, MatButtonModule, RouterLink],
  templateUrl: './quotations.html',
  styleUrl: '../shared-list.scss',
})
export class Quotations {
  protected readonly api = inject(ApiService);
  protected readonly rows = signal<Quotation[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly message = signal('');
  protected readonly exporting = signal(false);
  protected readonly selected = signal<Quotation | null>(null);
  protected customerPoNumber = '';

  constructor() {
    this.api.quotations().subscribe({
      next: (rows) => { this.rows.set(rows); this.loading.set(false); },
      error: () => { this.error.set('Unable to load quotations.'); this.loading.set(false); },
    });
  }

  protected selectExport(quote: Quotation): void {
    this.selected.set(quote);
    this.customerPoNumber = quote.poTrackerPoNumber ?? '';
    this.error.set('');
    this.message.set('');
  }

  protected sendToPoTracker(): void {
    const quote = this.selected();
    if (!quote || this.exporting() || !this.customerPoNumber.trim()) return;
    this.exporting.set(true);
    this.error.set('');
    this.api.exportQuotation(quote.id, this.customerPoNumber.trim()).subscribe({
      next: (result) => {
        this.rows.update(rows => rows.map(row => row.id === quote.id ? {
          ...row, poTrackerPurchaseOrderId: result.purchaseOrderId,
          poTrackerPoNumber: result.poNumber, poTrackerExportedRevision: quote.revision,
        } : row));
        this.message.set(`PO ${result.poNumber} ${result.alreadyImported ? 'already exists' : 'created'} in POTracker (ID ${result.purchaseOrderId}).`);
        this.exporting.set(false);
        this.selected.set(null);
      },
      error: (response) => {
        this.error.set(response.error?.detail ?? 'Unable to send to POTracker. Retry with the same customer PO number.');
        this.exporting.set(false);
      },
    });
  }
}
