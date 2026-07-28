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
  inject,
  signal,
} from '@angular/core';
import {
  takeUntilDestroyed,
} from '@angular/core/rxjs-interop';
import {
  FormsModule,
} from '@angular/forms';
import {
  RouterLink,
} from '@angular/router';
import {
  finalize,
} from 'rxjs';

import {
  AlertMaterial,
  AlertsResponse,
} from '../../core/models/alert-api.model';
import {
  AlertApiService,
} from '../../core/services/alert-api.service';

interface AlertGroup {
  key: string;
  title: string;
  description: string;
  dateLabel: string;
  dateField:
    | 'alertDate'
    | 'deliveryDate'
    | 'dispatchDate';
  tone:
    | 'blue'
    | 'red'
    | 'yellow'
    | 'purple'
    | 'orange'
    | 'green'
    | 'dark';
  items: AlertMaterial[];
}

const EMPTY_RESPONSE: AlertsResponse = {
  generatedAt: '',

  summary: {
    processStartAlerts: 0,
    overdueMaterials: 0,
    deliveryWithinSevenDays: 0,
    pendingProcess: 0,
    dispatchPending: 0,
    dispatchDateAlerts: 0,
    lowStock: 0,
    totalAlerts: 0,
  },

  processStartAlerts: [],
  overdueMaterials: [],
  deliveryWithinSevenDays: [],
  pendingProcess: [],
  dispatchPending: [],
  dispatchDateAlerts: [],
  lowStock: [],
};

@Component({
  selector: 'app-alerts-page',
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterLink,
  ],
  templateUrl:
    './alerts-page.html',
  styleUrl:
    './alerts-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class AlertsPage {
  private readonly api =
    inject(AlertApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly data =
    signal<AlertsResponse>(
      EMPTY_RESPONSE,
    );

  readonly loading =
    signal(false);

  readonly errorMessage =
    signal('');

  searchText = '';

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.api
      .getAlerts()
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.loading.set(false)),
      )
      .subscribe({
        next: response =>
          this.data.set(response),

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(error),
          );
        },
      });
  }

  getGroups(): AlertGroup[] {
    const response =
      this.data();

    return [
      {
        key: 'process-start',
        title:
          'Process Start Alerts',
        description:
          'Materials whose process alert date has been reached.',
        dateLabel:
          'Alert Date',
        dateField:
          'alertDate',
        tone:
          'blue',
        items:
          response.processStartAlerts,
      },
      {
        key: 'overdue',
        title:
          'Overdue Materials',
        description:
          'Delivery date has passed and processing is incomplete.',
        dateLabel:
          'Delivery Date',
        dateField:
          'deliveryDate',
        tone:
          'red',
        items:
          response.overdueMaterials,
      },
      {
        key: 'delivery-seven-days',
        title:
          'Delivery Within 7 Days',
        description:
          'Materials approaching their committed delivery date.',
        dateLabel:
          'Delivery Date',
        dateField:
          'deliveryDate',
        tone:
          'yellow',
        items:
          response.deliveryWithinSevenDays,
      },
      {
        key: 'pending-process',
        title:
          'Pending Process Materials',
        description:
          'Materials currently pending, in process or on hold.',
        dateLabel:
          'Delivery Date',
        dateField:
          'deliveryDate',
        tone:
          'purple',
        items:
          response.pendingProcess,
      },
      {
        key: 'dispatch-pending',
        title:
          'Dispatch Pending Materials',
        description:
          'Materials with pending or partial dispatch status.',
        dateLabel:
          'Delivery Date',
        dateField:
          'deliveryDate',
        tone:
          'orange',
        items:
          response.dispatchPending,
      },
      {
        key: 'dispatch-date',
        title:
          'Dispatch Date Alerts',
        description:
          'Dispatch date is due within the next seven days.',
        dateLabel:
          'Dispatch Date',
        dateField:
          'dispatchDate',
        tone:
          'green',
        items:
          response.dispatchDateAlerts,
      },
      {
        key: 'low-stock',
        title:
          'Stock Shortage Materials',
        description:
          'Materials whose pending requirement exceeds their priority-allocated live stock.',
        dateLabel:
          'Delivery Date',
        dateField:
          'deliveryDate',
        tone:
          'dark',
        items:
          response.lowStock,
      },
    ];
  }

  filterItems(
    items: AlertMaterial[],
  ): AlertMaterial[] {
    const search =
      this.searchText
        .trim()
        .toLowerCase();

    if (!search) {
      return items;
    }

    return items.filter(item =>
      item.materialNo
        .toLowerCase()
        .includes(search) ||

      item.lineItem
        .toLowerCase()
        .includes(search) ||

      item.description
        .toLowerCase()
        .includes(search) ||

      item.processStatus
        .toLowerCase()
        .includes(search) ||

      item.dispatchStatus
        .toLowerCase()
        .includes(search) ||

      item.stockStatus
        .toLowerCase()
        .includes(search),
    );
  }

  getDate(
    item: AlertMaterial,
    field:
      | 'alertDate'
      | 'deliveryDate'
      | 'dispatchDate',
  ): string | null {
    return item[field];
  }

  statusClass(
    status: string,
  ): string {
    const normalized =
      status.toLowerCase();

    if (
      normalized.includes('completed') ||
      normalized.includes('dispatched')
    ) {
      return 'status status--success';
    }

    if (
      normalized.includes('partial') ||
      normalized.includes('process')
    ) {
      return 'status status--info';
    }

    if (
      normalized.includes('hold') ||
      normalized.includes('overdue')
    ) {
      return 'status status--danger';
    }

    return 'status status--warning';
  }


  stockStatusClass(
    status: string,
  ): string {
    const normalized =
      status.toLowerCase();

    if (
      normalized.includes('available') ||
      normalized.includes('no pending')
    ) {
      return 'status status--success';
    }

    if (
      normalized.includes('partial')
    ) {
      return 'status status--info';
    }

    if (
      normalized.includes('shortage') ||
      normalized.includes('out of stock') ||
      normalized.includes('offline')
    ) {
      return 'status status--danger';
    }

    return 'status status--warning';
  }
  private describeError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message === 'string'
      ? message
      : 'Smart Alerts could not be loaded.';
  }
}

