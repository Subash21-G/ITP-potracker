import { NumericLinePipe } from '../../shared/numeric-line.pipe';
import { DatePipe, DecimalPipe, Location } from '@angular/common';

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

import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';

import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import {
  EMPTY,
  Subject,
  catchError,
  debounceTime,
  defer,
  distinctUntilChanged,
  finalize,
  switchMap,
  tap,
} from 'rxjs';

import {
  AlertCategory,
  AlertExcelFilterColumn,
  AlertExcelFilterSelections,
  AlertExcelFilterValue,
  AlertMaterial,
  AlertSortColumn,
  AlertSortDirection,
  AlertsPagedResponse,
  AlertsQuery,
  AlertsSummary,
} from '../../core/models/alert-api.model';

import { AlertApiService } from '../../core/services/alert-api.service';

import { ServerExcelFilter } from '../../shared/server-excel-filter/server-excel-filter';

interface AlertCategoryOption {
  readonly value: AlertCategory;

  readonly label: string;
  readonly description: string;
  readonly shortLabel: string;
  readonly tone: 'blue' | 'red' | 'amber' | 'violet' | 'orange' | 'green' | 'navy';

  readonly dateField: 'alertDate' | 'deliveryDate' | 'dispatchDate';
}

const EMPTY_SUMMARY: AlertsSummary = {
  processStartAlerts: 0,
  overdueMaterials: 0,
  deliveryWithinSevenDays: 0,
  pendingProcess: 0,
  dispatchPending: 0,
  dispatchDateAlerts: 0,
  lowStock: 0,
  totalAlerts: 0,
};

const EMPTY_RESPONSE: AlertsPagedResponse = {
  generatedAt: '',
  summary: EMPTY_SUMMARY,
  category: 'process-start',
  items: [],
  page: 1,
  pageSize: 25,
  totalCount: 0,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false,
};

@Component({
  selector: 'app-alerts-page',
  imports: [NumericLinePipe, DatePipe, DecimalPipe, ReactiveFormsModule, RouterLink, ServerExcelFilter],
  templateUrl: './alerts-page.html',
  styleUrl: './alerts-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AlertsPage {
  private readonly api = inject(AlertApiService);

  private readonly destroyRef = inject(DestroyRef);

  private readonly route = inject(ActivatedRoute);

  private readonly router = inject(Router);

  private readonly location = inject(Location);

  private readonly requestAlerts = new Subject<void>();

  private readonly initialParams = this.route.snapshot.queryParamMap;

  readonly categoryOptions: readonly AlertCategoryOption[] = [
    {
      value: 'process-start',
      label: 'Process Start Alerts',
      shortLabel: 'Process Start',
      description: 'Process alert date has been reached and work is not completed.',
      tone: 'blue',
      dateField: 'alertDate',
    },
    {
      value: 'overdue',
      label: 'Overdue Materials',
      shortLabel: 'Overdue',
      description: 'Delivery date has passed while processing remains incomplete.',
      tone: 'red',
      dateField: 'deliveryDate',
    },
    {
      value: 'delivery-seven-days',
      label: 'Delivery Within 7 Days',
      shortLabel: 'Due Soon',
      description: 'Committed delivery date falls within the next seven days.',
      tone: 'amber',
      dateField: 'deliveryDate',
    },
    {
      value: 'pending-process',
      label: 'Pending Process Materials',
      shortLabel: 'Pending Process',
      description: 'Material is pending, in process or currently on hold.',
      tone: 'violet',
      dateField: 'deliveryDate',
    },
    {
      value: 'dispatch-pending',
      label: 'Dispatch Pending Materials',
      shortLabel: 'Dispatch Pending',
      description: 'Dispatch status is pending or only partially completed.',
      tone: 'orange',
      dateField: 'deliveryDate',
    },
    {
      value: 'dispatch-date',
      label: 'Dispatch Date Alerts',
      shortLabel: 'Dispatch Date',
      description: 'Dispatch date is due within seven days and dispatch is incomplete.',
      tone: 'green',
      dateField: 'dispatchDate',
    },
    {
      value: 'low-stock',
      label: 'Stock Shortage Materials',
      shortLabel: 'Stock Shortage',
      description: 'Priority-allocated live stock cannot cover the pending requirement.',
      tone: 'navy',
      dateField: 'deliveryDate',
    },
  ];

  readonly data = signal<AlertsPagedResponse>(EMPTY_RESPONSE);

  readonly category = signal<AlertCategory>(this.parseCategory(this.initialParams.get('category')));

  readonly sortColumn = signal<AlertSortColumn | null>(
    this.parseSortColumn(this.initialParams.get('sortColumn')),
  );

  readonly sortDirection = signal<AlertSortDirection | null>(
    this.parseSortDirection(this.initialParams.get('sortDirection')),
  );

  readonly excelFilterSelections = signal<AlertExcelFilterSelections>({});

  readonly excelFilterOptions = signal<
    Partial<Record<AlertExcelFilterColumn, readonly AlertExcelFilterValue[]>>
  >({});

  readonly excelFilterLoadingColumns = signal<ReadonlySet<AlertExcelFilterColumn>>(
    new Set<AlertExcelFilterColumn>(),
  );

  readonly searchControl = new FormControl(this.initialParams.get('search') ?? '', {
    nonNullable: true,
  });

  readonly columnFilters = new FormGroup({
    material: new FormControl(this.initialParams.get('material') ?? '', { nonNullable: true }),
    description: new FormControl(this.initialParams.get('description') ?? '', {
      nonNullable: true,
    }),
    priorityDateFrom: new FormControl(this.initialParams.get('priorityDateFrom') ?? '', {
      nonNullable: true,
    }),
    priorityDateTo: new FormControl(this.initialParams.get('priorityDateTo') ?? '', {
      nonNullable: true,
    }),
    deliveryDateFrom: new FormControl(this.initialParams.get('deliveryDateFrom') ?? '', {
      nonNullable: true,
    }),
    deliveryDateTo: new FormControl(this.initialParams.get('deliveryDateTo') ?? '', {
      nonNullable: true,
    }),
    processStatus: new FormControl(this.initialParams.get('processStatus') ?? '', {
      nonNullable: true,
    }),
    dispatchStatus: new FormControl(this.initialParams.get('dispatchStatus') ?? '', {
      nonNullable: true,
    }),
    minAllocatedStock: new FormControl(this.initialParams.get('minAllocatedStock') ?? '', {
      nonNullable: true,
    }),
    maxAllocatedStock: new FormControl(this.initialParams.get('maxAllocatedStock') ?? '', {
      nonNullable: true,
    }),
    minLiveStock: new FormControl(this.initialParams.get('minLiveStock') ?? '', {
      nonNullable: true,
    }),
    maxLiveStock: new FormControl(this.initialParams.get('maxLiveStock') ?? '', {
      nonNullable: true,
    }),
    stockStatus: new FormControl(this.initialParams.get('stockStatus') ?? '', {
      nonNullable: true,
    }),
  });

  readonly page = signal(this.parsePositiveInteger(this.initialParams.get('page'), 1));

  readonly pageSize = signal(this.parsePageSize(this.initialParams.get('pageSize')));

  readonly loading = signal(false);

  readonly errorMessage = signal('');

  readonly selectedCategory = computed(
    () =>
      this.categoryOptions.find((option) => option.value === this.category()) ??
      this.categoryOptions[0],
  );

  readonly startRecord = computed(() =>
    this.data().totalCount === 0 ? 0 : (this.data().page - 1) * this.data().pageSize + 1,
  );

  readonly endRecord = computed(() =>
    Math.min(this.data().page * this.data().pageSize, this.data().totalCount),
  );

  readonly visiblePages = computed(() => {
    const current = this.data().page;

    const total = this.data().totalPages;

    const first = Math.max(1, current - 2);

    const last = Math.min(total, first + 4);

    const adjustedFirst = Math.max(1, last - 4);

    return Array.from(
      {
        length: last - adjustedFirst + 1,
      },
      (_, index) => adjustedFirst + index,
    );
  });

  constructor() {
    this.requestAlerts
      .pipe(
        switchMap(() =>
          defer(() => {
            this.loading.set(true);

            this.errorMessage.set('');

            this.replaceUrlState();

            return this.api.getAlerts(this.buildQuery()).pipe(
              tap((response) => {
                this.data.set(response);

                this.category.set(response.category);

                this.page.set(response.page);

                this.pageSize.set(response.pageSize);
              }),

              catchError((error: HttpErrorResponse) => {
                this.errorMessage.set(this.describeError(error));

                return EMPTY;
              }),

              finalize(() => this.loading.set(false)),
            );
          }),
        ),

        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();

    this.searchControl.valueChanges
      .pipe(
        debounceTime(320),

        distinctUntilChanged(),

        tap(() => this.page.set(1)),

        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.load());

    this.load();
  }

  excelFilterOptionsFor(column: AlertExcelFilterColumn): readonly AlertExcelFilterValue[] {
    return this.excelFilterOptions()[column] ?? [];
  }

  excelFilterSelectedValues(column: AlertExcelFilterColumn): readonly string[] {
    return this.excelFilterSelections()[column] ?? [];
  }

  excelFilterActive(column: AlertExcelFilterColumn): boolean {
    return Object.prototype.hasOwnProperty.call(this.excelFilterSelections(), column);
  }

  excelFilterLoading(column: AlertExcelFilterColumn): boolean {
    return this.excelFilterLoadingColumns().has(column);
  }

  excelSortDirection(column: AlertExcelFilterColumn): AlertSortDirection | null {
    return this.sortColumn() === column ? this.sortDirection() : null;
  }

  loadExcelFilterValues(column: AlertExcelFilterColumn): void {
    if (this.excelFilterLoading(column)) {
      return;
    }

    this.excelFilterLoadingColumns.update((columns) => {
      const updated = new Set(columns);

      updated.add(column);

      return updated;
    });

    this.api
      .getExcelFilterValues(this.buildQuery(), column)
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
          this.errorMessage.set(this.describeError(error));
        },
      });
  }

  applyExcelSort(column: AlertExcelFilterColumn, direction: AlertSortDirection | null): void {
    if (this.loading()) {
      return;
    }

    this.sortColumn.set(direction === null ? null : column);

    this.sortDirection.set(direction);

    this.page.set(1);
    this.load();
  }

  applyExcelFilter(column: AlertExcelFilterColumn, values: readonly string[]): void {
    const availableValues = this.excelFilterOptions()[column] ?? [];

    const selectedSet = new Set(values);

    const selectsEverything =
      availableValues.length > 0 &&
      availableValues.length === selectedSet.size &&
      availableValues.every((option) => selectedSet.has(option.value));

    this.excelFilterSelections.update((current) => {
      const updated = {
        ...current,
      };

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

  clearExcelFilter(column: AlertExcelFilterColumn): void {
    this.excelFilterSelections.update((current) => {
      const updated = {
        ...current,
      };

      delete updated[column];

      return updated;
    });

    this.page.set(1);
    this.load();
  }

  toggleSort(column: AlertSortColumn): void {
    if (this.loading()) {
      return;
    }

    if (this.sortColumn() !== column || this.sortDirection() === null) {
      this.sortColumn.set(column);

      this.sortDirection.set('asc');
    } else if (this.sortDirection() === 'asc') {
      this.sortDirection.set('desc');
    } else {
      this.sortColumn.set(null);

      this.sortDirection.set(null);
    }

    this.page.set(1);
    this.load();
  }

  sortIndicator(column: AlertSortColumn): string {
    if (this.sortColumn() !== column) {
      return '\u2195';
    }

    return this.sortDirection() === 'desc' ? '\u2193' : '\u2191';
  }

  sortTitle(column: AlertSortColumn, label: string): string {
    if (this.sortColumn() !== column) {
      return `Sort ${label} ascending`;
    }

    if (this.sortDirection() === 'asc') {
      return `Sort ${label} descending`;
    }

    return `Clear ${label} sort`;
  }

  applyColumnFilter(filter: HTMLDetailsElement): void {
    filter.open = false;
    this.page.set(1);
    this.load();
  }

  clearColumnFilter(
    column: 'material' | 'description' | 'priority' | 'delivery' | 'process' | 'dispatch' | 'stock',
    filter: HTMLDetailsElement,
  ): void {
    switch (column) {
      case 'material':
        this.columnFilters.controls.material.setValue('');
        break;
      case 'description':
        this.columnFilters.controls.description.setValue('');
        break;
      case 'priority':
        this.columnFilters.patchValue({ priorityDateFrom: '', priorityDateTo: '' });
        break;
      case 'delivery':
        this.columnFilters.patchValue({ deliveryDateFrom: '', deliveryDateTo: '' });
        break;
      case 'process':
        this.columnFilters.controls.processStatus.setValue('');
        break;
      case 'dispatch':
        this.columnFilters.controls.dispatchStatus.setValue('');
        break;
      case 'stock':
        this.columnFilters.patchValue({
          minAllocatedStock: '',
          maxAllocatedStock: '',
          minLiveStock: '',
          maxLiveStock: '',
          stockStatus: '',
        });
        break;
    }
    filter.open = false;
    this.page.set(1);
    this.load();
  }

  columnFilterActive(
    column: 'material' | 'description' | 'priority' | 'delivery' | 'process' | 'dispatch' | 'stock',
  ): boolean {
    const value = this.columnFilters.getRawValue();
    switch (column) {
      case 'material':
        return Boolean(value.material.trim());
      case 'description':
        return Boolean(value.description.trim());
      case 'priority':
        return Boolean(value.priorityDateFrom || value.priorityDateTo);
      case 'delivery':
        return Boolean(value.deliveryDateFrom || value.deliveryDateTo);
      case 'process':
        return Boolean(value.processStatus);
      case 'dispatch':
        return Boolean(value.dispatchStatus);
      case 'stock':
        return Boolean(
          value.minAllocatedStock ||
          value.maxAllocatedStock ||
          value.minLiveStock ||
          value.maxLiveStock ||
          value.stockStatus,
        );
    }
  }

  load(): void {
    this.requestAlerts.next();
  }

  setCategory(category: AlertCategory): void {
    if (category === this.category()) {
      return;
    }

    this.category.set(category);

    this.page.set(1);
    this.load();
  }

  changeCategory(value: string): void {
    this.setCategory(this.parseCategory(value));
  }

  changePageSize(value: string): void {
    const pageSize = this.parsePageSize(value);

    if (pageSize === this.pageSize()) {
      return;
    }

    this.pageSize.set(pageSize);

    this.page.set(1);
    this.load();
  }

  goToPage(page: number): void {
    const normalizedPage = Math.min(Math.max(page, 1), this.data().totalPages);

    if (normalizedPage === this.page()) {
      return;
    }

    this.page.set(normalizedPage);

    this.load();
  }

  clearSearch(): void {
    this.searchControl.setValue('');
  }

  countFor(category: AlertCategory): number {
    const summary = this.data().summary;

    switch (category) {
      case 'overdue':
        return summary.overdueMaterials;

      case 'delivery-seven-days':
        return summary.deliveryWithinSevenDays;

      case 'pending-process':
        return summary.pendingProcess;

      case 'dispatch-pending':
        return summary.dispatchPending;

      case 'dispatch-date':
        return summary.dispatchDateAlerts;

      case 'low-stock':
        return summary.lowStock;

      default:
        return summary.processStartAlerts;
    }
  }

  getDate(item: AlertMaterial): string | null {
    return item[this.selectedCategory().dateField];
  }

  statusClass(status: string): string {
    const normalized = status.trim().toLowerCase();

    if (normalized.includes('completed') || normalized.includes('dispatched')) {
      return 'status status--success';
    }

    if (normalized.includes('partial') || normalized.includes('process')) {
      return 'status status--info';
    }

    if (normalized.includes('hold') || normalized.includes('overdue')) {
      return 'status status--danger';
    }

    return 'status status--warning';
  }

  stockStatusClass(status: string): string {
    const normalized = status.trim().toLowerCase();

    if (normalized.includes('available') || normalized.includes('no pending')) {
      return 'status status--success';
    }

    if (normalized.includes('partial')) {
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

  private buildQuery(): AlertsQuery {
    return {
      category: this.category(),

      sortColumn: this.sortColumn() ?? undefined,

      sortDirection: this.sortDirection() ?? undefined,

      search: this.searchControl.value,
      ...this.columnFilters.getRawValue(),

      excelFilters: this.serializeExcelFilters(),

      page: this.page(),
      pageSize: this.pageSize(),
    };
  }

  private serializeExcelFilters(): string | undefined {
    const selections = this.excelFilterSelections();

    return Object.keys(selections).length > 0 ? JSON.stringify(selections) : undefined;
  }

  private replaceUrlState(): void {
    const query = this.buildQuery();
    const urlTree = this.router.createUrlTree([], {
      relativeTo: this.route,
      queryParams: {
        category: query.category !== 'process-start' ? query.category : null,

        sortColumn: query.sortColumn ?? null,

        sortDirection: query.sortDirection ?? null,

        search: query.search?.trim() || null,
        material: query.material?.trim() || null,
        description: query.description?.trim() || null,
        priorityDateFrom: query.priorityDateFrom || null,
        priorityDateTo: query.priorityDateTo || null,
        deliveryDateFrom: query.deliveryDateFrom || null,
        deliveryDateTo: query.deliveryDateTo || null,
        processStatus: query.processStatus || null,
        dispatchStatus: query.dispatchStatus || null,
        minAllocatedStock: query.minAllocatedStock || null,
        maxAllocatedStock: query.maxAllocatedStock || null,
        minLiveStock: query.minLiveStock || null,
        maxLiveStock: query.maxLiveStock || null,
        stockStatus: query.stockStatus || null,
        page: query.page > 1 ? query.page : null,
        pageSize: query.pageSize !== 25 ? query.pageSize : null,
      },
    });
    this.location.replaceState(this.router.serializeUrl(urlTree));
  }

  private parseSortColumn(value: string | null): AlertSortColumn | null {
    switch (value) {
      case 'material':
      case 'description':
      case 'priorityDate':
      case 'deliveryDate':
      case 'process':
      case 'dispatch':
      case 'stock':
        return value;

      default:
        return null;
    }
  }

  private parseSortDirection(value: string | null): AlertSortDirection | null {
    return value === 'asc' || value === 'desc' ? value : null;
  }

  private parseCategory(value: string | null): AlertCategory {
    return this.categoryOptions.some((option) => option.value === value)
      ? (value as AlertCategory)
      : 'process-start';
  }

  private parsePositiveInteger(value: string | null, fallback: number): number {
    const parsed = Number(value);

    return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
  }

  private parsePageSize(value: string | null): number {
    const parsed = Number(value);

    return parsed === 50 || parsed === 100 ? parsed : 25;
  }

  private describeError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    const message = error.error?.message ?? error.error?.error;

    return typeof message === 'string' ? message : 'Smart Alerts could not be loaded.';
  }
}
