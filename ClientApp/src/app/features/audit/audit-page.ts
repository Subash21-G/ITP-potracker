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

import { AuditItem } from '../../core/models/insights-api.model';

import {
  AuditExcelFilterColumn,
  AuditExcelFilterSelections,
  AuditExcelFilterValue,
  AuditQuery,
  AuditSortColumn,
  AuditSortDirection,
} from '../../core/models/audit-api.model';

import { AuditApiService } from '../../core/services/audit-api.service';

import { ServerExcelFilter } from '../../shared/server-excel-filter/server-excel-filter';

@Component({
  selector: 'app-audit-page',
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule, RouterLink, ServerExcelFilter],
  templateUrl: './audit-page.html',
  styleUrls: ['../insights-page.css', './audit-page.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditPage {
  private readonly api = inject(AuditApiService);

  private readonly destroyRef = inject(DestroyRef);

  private readonly route = inject(ActivatedRoute);

  private readonly router = inject(Router);

  private readonly location = inject(Location);

  private readonly requestAudit = new Subject<void>();

  private readonly initialParams = this.route.snapshot.queryParamMap;

  readonly filters = new FormGroup({
    search: new FormControl(this.initialParams.get('search') ?? '', {
      nonNullable: true,
    }),

    owner: new FormControl(this.initialParams.get('owner') ?? '', {
      nonNullable: true,
    }),

    processStatus: new FormControl(this.initialParams.get('processStatus') ?? '', {
      nonNullable: true,
    }),

    dispatchStatus: new FormControl(this.initialParams.get('dispatchStatus') ?? '', {
      nonNullable: true,
    }),

    dateFrom: new FormControl(this.initialParams.get('dateFrom') ?? '', {
      nonNullable: true,
    }),

    dateTo: new FormControl(this.initialParams.get('dateTo') ?? '', {
      nonNullable: true,
    }),
  });

  // Preserve old deep-link query parameters even though the
  // visible header menus now use the shared Excel component.
  readonly columnFilters = new FormGroup({
    createdDateFrom: new FormControl(this.initialParams.get('createdDateFrom') ?? '', {
      nonNullable: true,
    }),

    createdDateTo: new FormControl(this.initialParams.get('createdDateTo') ?? '', {
      nonNullable: true,
    }),

    materialNo: new FormControl(this.initialParams.get('materialNo') ?? '', {
      nonNullable: true,
    }),

    poNumber: new FormControl(this.initialParams.get('poNumber') ?? '', {
      nonNullable: true,
    }),

    action: new FormControl(this.initialParams.get('action') ?? '', {
      nonNullable: true,
    }),

    columnProcessStatus: new FormControl(this.initialParams.get('columnProcessStatus') ?? '', {
      nonNullable: true,
    }),

    columnDispatchStatus: new FormControl(this.initialParams.get('columnDispatchStatus') ?? '', {
      nonNullable: true,
    }),

    columnOwner: new FormControl(this.initialParams.get('columnOwner') ?? '', {
      nonNullable: true,
    }),
  });

  readonly items = signal<readonly AuditItem[]>([]);

  readonly generatedAt = signal<string | null>(null);

  readonly page = signal(this.parsePositiveInteger(this.initialParams.get('page'), 1));

  readonly pageSize = signal(this.parsePageSize(this.initialParams.get('pageSize')));

  readonly sortColumn = signal<AuditSortColumn | null>(
    this.parseSortColumn(this.initialParams.get('sortColumn')),
  );

  readonly sortDirection = signal<AuditSortDirection | null>(
    this.parseSortDirection(this.initialParams.get('sortDirection')),
  );

  readonly excelFilterSelections = signal<AuditExcelFilterSelections>(
    this.parseExcelFilters(this.initialParams.get('excelFilters')),
  );

  readonly excelFilterOptions = signal<
    Partial<Record<AuditExcelFilterColumn, readonly AuditExcelFilterValue[]>>
  >({});

  readonly excelFilterLoadingColumns = signal<ReadonlySet<AuditExcelFilterColumn>>(
    new Set<AuditExcelFilterColumn>(),
  );

  readonly totalCount = signal(0);

  readonly totalPages = signal(1);

  readonly loading = signal(false);

  readonly errorMessage = signal('');

  readonly startRecord = computed(() =>
    this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );

  readonly endRecord = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));

  readonly hasPreviousPage = computed(() => this.page() > 1);

  readonly hasNextPage = computed(() => this.page() < this.totalPages());

  readonly visiblePages = computed(() => {
    const current = this.page();

    const total = this.totalPages();

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
    this.requestAudit
      .pipe(
        switchMap(() =>
          defer(() => {
            this.loading.set(true);

            this.errorMessage.set('');

            this.replaceUrlState();

            return this.api.getAudit(this.buildQuery()).pipe(
              tap((response) => {
                this.items.set(response.items);

                this.page.set(response.page);

                this.pageSize.set(response.pageSize);

                this.totalCount.set(response.totalCount);

                this.totalPages.set(response.totalPages);

                this.generatedAt.set(response.generatedAt);
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

    this.filters.valueChanges
      .pipe(
        debounceTime(320),

        distinctUntilChanged(
          (previous, current) => JSON.stringify(previous) === JSON.stringify(current),
        ),

        tap(() => this.page.set(1)),

        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.load());

    this.load();
  }

  excelFilterOptionsFor(column: AuditExcelFilterColumn): readonly AuditExcelFilterValue[] {
    return this.excelFilterOptions()[column] ?? [];
  }

  excelFilterSelectedValues(column: AuditExcelFilterColumn): readonly string[] {
    return this.excelFilterSelections()[column] ?? [];
  }

  excelFilterActive(column: AuditExcelFilterColumn): boolean {
    return Object.prototype.hasOwnProperty.call(this.excelFilterSelections(), column);
  }

  excelFilterLoading(column: AuditExcelFilterColumn): boolean {
    return this.excelFilterLoadingColumns().has(column);
  }

  excelSortDirection(column: AuditExcelFilterColumn): AuditSortDirection | null {
    return this.sortColumn() === column ? this.sortDirection() : null;
  }

  loadExcelFilterValues(column: AuditExcelFilterColumn): void {
    if (this.excelFilterLoading(column)) {
      return;
    }

    this.excelFilterLoadingColumns.update((columns) => {
      const updated = new Set(columns);

      updated.add(column);

      return updated;
    });

    this.api
      .getAuditExcelFilterValues(this.buildQuery(), column)
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

  applyExcelSort(column: AuditExcelFilterColumn, direction: AuditSortDirection | null): void {
    this.sortColumn.set(direction === null ? null : column);

    this.sortDirection.set(direction);

    this.page.set(1);
    this.load();
  }

  applyExcelFilter(column: AuditExcelFilterColumn, values: readonly string[]): void {
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

  clearExcelFilter(column: AuditExcelFilterColumn): void {
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

  refresh(): void {
    this.load();
  }

  applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  resetFilters(): void {
    this.filters.setValue(
      {
        search: '',
        owner: '',
        processStatus: '',
        dispatchStatus: '',
        dateFrom: '',
        dateTo: '',
      },
      {
        emitEvent: false,
      },
    );

    this.columnFilters.reset(undefined, {
      emitEvent: false,
    });

    this.sortColumn.set(null);

    this.sortDirection.set(null);

    this.excelFilterSelections.set({});

    this.page.set(1);
    this.load();
  }

  goToPage(page: number): void {
    const normalizedPage = Math.min(Math.max(page, 1), this.totalPages());

    if (normalizedPage === this.page()) {
      return;
    }

    this.page.set(normalizedPage);

    this.load();
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

  private load(): void {
    this.requestAudit.next();
  }

  private buildQuery(): AuditQuery {
    const value = this.filters.getRawValue();

    const columnValue = this.columnFilters.getRawValue();

    return {
      search: value.search,
      owner: value.owner,
      processStatus: value.processStatus,
      dispatchStatus: value.dispatchStatus,
      dateFrom: value.dateFrom,
      dateTo: value.dateTo,

      ...columnValue,

      sortColumn: this.sortColumn() ?? undefined,

      sortDirection: this.sortDirection() ?? undefined,

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
        search: query.search || null,

        owner: query.owner || null,

        processStatus: query.processStatus || null,

        dispatchStatus: query.dispatchStatus || null,

        dateFrom: query.dateFrom || null,

        dateTo: query.dateTo || null,

        materialNo: query.materialNo || null,

        poNumber: query.poNumber || null,

        action: query.action || null,

        columnProcessStatus: query.columnProcessStatus || null,

        columnDispatchStatus: query.columnDispatchStatus || null,

        columnOwner: query.columnOwner || null,

        createdDateFrom: query.createdDateFrom || null,

        createdDateTo: query.createdDateTo || null,

        sortColumn: query.sortColumn || null,

        sortDirection: query.sortDirection || null,

        excelFilters: query.excelFilters || null,

        page: query.page > 1 ? query.page : null,

        pageSize: query.pageSize !== 25 ? query.pageSize : null,
      },
    });

    this.location.replaceState(this.router.serializeUrl(urlTree));
  }

  private parsePositiveInteger(value: string | null, fallback: number): number {
    const parsed = Number(value);

    return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
  }

  private parsePageSize(value: string | null): number {
    const parsed = Number(value);

    return parsed === 50 || parsed === 100 ? parsed : 25;
  }

  private parseSortColumn(value: string | null): AuditSortColumn | null {
    return this.isExcelColumn(value) ? value : null;
  }

  private parseSortDirection(value: string | null): AuditSortDirection | null {
    return value === 'asc' || value === 'desc' ? value : null;
  }

  private parseExcelFilters(value: string | null): AuditExcelFilterSelections {
    if (!value) {
      return {};
    }

    try {
      const parsed: unknown = JSON.parse(value);

      if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
        return {};
      }

      const selections: AuditExcelFilterSelections = {};

      for (const [key, rawValues] of Object.entries(parsed)) {
        if (!this.isExcelColumn(key) || !Array.isArray(rawValues)) {
          continue;
        }

        selections[key] = rawValues.filter((item): item is string => typeof item === 'string');
      }

      return selections;
    } catch {
      return {};
    }
  }

  private isExcelColumn(value: string | null): value is AuditExcelFilterColumn {
    return (
      value === 'date' ||
      value === 'material' ||
      value === 'po' ||
      value === 'action' ||
      value === 'process' ||
      value === 'dispatch' ||
      value === 'owner'
    );
  }

  private describeError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to view the audit page.';
    }

    const message = error.error?.message ?? error.error?.error;

    return typeof message === 'string' ? message : 'Material activity could not be loaded.';
  }
}
