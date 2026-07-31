import {
  DatePipe,
  DecimalPipe,
  Location,
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
  FormGroup,
  ReactiveFormsModule,
} from '@angular/forms';

import {
  ActivatedRoute,
  Router,
  RouterLink,
} from '@angular/router';

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
  AuditItem,
} from '../../core/models/insights-api.model';

import {
  AuditQuery,
} from '../../core/models/audit-api.model';

import {
  AuditApiService,
} from '../../core/services/audit-api.service';

@Component({
  selector: 'app-audit-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl:
    './audit-page.html',
  styleUrls: [
    '../insights-page.css',
    './audit-page.css',
  ],
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class AuditPage {
  private readonly api =
    inject(AuditApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  private readonly route =
    inject(ActivatedRoute);

  private readonly router =
    inject(Router);

  private readonly location =
    inject(Location);

  private readonly requestAudit =
    new Subject<void>();

  private readonly initialParams =
    this.route.snapshot
      .queryParamMap;

  readonly filters =
    new FormGroup({
      search:
        new FormControl(
          this.initialParams.get(
            'search',
          ) ?? '',
          {
            nonNullable: true,
          },
        ),

      owner:
        new FormControl(
          this.initialParams.get(
            'owner',
          ) ?? '',
          {
            nonNullable: true,
          },
        ),

      processStatus:
        new FormControl(
          this.initialParams.get(
            'processStatus',
          ) ?? '',
          {
            nonNullable: true,
          },
        ),

      dispatchStatus:
        new FormControl(
          this.initialParams.get(
            'dispatchStatus',
          ) ?? '',
          {
            nonNullable: true,
          },
        ),

      dateFrom:
        new FormControl(
          this.initialParams.get(
            'dateFrom',
          ) ?? '',
          {
            nonNullable: true,
          },
        ),

      dateTo:
        new FormControl(
          this.initialParams.get(
            'dateTo',
          ) ?? '',
          {
            nonNullable: true,
          },
        ),
    });

  readonly items =
    signal<readonly AuditItem[]>(
      [],
    );

  readonly generatedAt =
    signal<string | null>(
      null,
    );

  readonly page =
    signal(
      this.parsePositiveInteger(
        this.initialParams.get(
          'page',
        ),
        1,
      ),
    );

  readonly pageSize =
    signal(
      this.parsePageSize(
        this.initialParams.get(
          'pageSize',
        ),
      ),
    );

  readonly totalCount =
    signal(0);

  readonly totalPages =
    signal(1);

  readonly loading =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly startRecord =
    computed(() =>
      this.totalCount() === 0
        ? 0
        : (
            this.page() - 1
          ) *
            this.pageSize() +
          1,
    );

  readonly endRecord =
    computed(() =>
      Math.min(
        this.page() *
          this.pageSize(),
        this.totalCount(),
      ),
    );

  readonly hasPreviousPage =
    computed(() =>
      this.page() > 1,
    );

  readonly hasNextPage =
    computed(() =>
      this.page() <
      this.totalPages(),
    );

  readonly visiblePages =
    computed(() => {
      const current =
        this.page();

      const total =
        this.totalPages();

      const first =
        Math.max(
          1,
          current - 2,
        );

      const last =
        Math.min(
          total,
          first + 4,
        );

      const adjustedFirst =
        Math.max(
          1,
          last - 4,
        );

      return Array.from(
        {
          length:
            last -
            adjustedFirst +
            1,
        },
        (
          _,
          index,
        ) =>
          adjustedFirst +
          index,
      );
    });

  constructor() {
    this.requestAudit
      .pipe(
        switchMap(() =>
          defer(() => {
            this.loading.set(
              true,
            );

            this.errorMessage.set(
              '',
            );

            this.replaceUrlState();

            return this.api
              .getAudit(
                this.buildQuery(),
              )
              .pipe(
                tap(
                  (response) => {
                    this.items.set(
                      response.items,
                    );

                    this.page.set(
                      response.page,
                    );

                    this.pageSize.set(
                      response.pageSize,
                    );

                    this.totalCount.set(
                      response.totalCount,
                    );

                    this.totalPages.set(
                      response.totalPages,
                    );

                    this.generatedAt.set(
                      response.generatedAt,
                    );
                  },
                ),

                catchError(
                  (
                    error:
                      HttpErrorResponse,
                  ) => {
                    this.errorMessage.set(
                      this.describeError(
                        error,
                      ),
                    );

                    return EMPTY;
                  },
                ),

                finalize(() =>
                  this.loading.set(
                    false,
                  ),
                ),
              );
          }),
        ),

        takeUntilDestroyed(
          this.destroyRef,
        ),
      )
      .subscribe();

    this.filters.valueChanges
      .pipe(
        debounceTime(320),

        distinctUntilChanged(
          (
            previous,
            current,
          ) =>
            JSON.stringify(
              previous,
            ) ===
            JSON.stringify(
              current,
            ),
        ),

        tap(() =>
          this.page.set(1),
        ),

        takeUntilDestroyed(
          this.destroyRef,
        ),
      )
      .subscribe(() =>
        this.load(),
      );

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
    this.filters.setValue({
      search: '',
      owner: '',
      processStatus: '',
      dispatchStatus: '',
      dateFrom: '',
      dateTo: '',
    });

    this.page.set(1);
  }

  goToPage(
    page: number,
  ): void {
    const normalizedPage =
      Math.min(
        Math.max(
          page,
          1,
        ),
        this.totalPages(),
      );

    if (
      normalizedPage ===
      this.page()
    ) {
      return;
    }

    this.page.set(
      normalizedPage,
    );

    this.load();
  }

  changePageSize(
    value: string,
  ): void {
    const pageSize =
      this.parsePageSize(
        value,
      );

    if (
      pageSize ===
      this.pageSize()
    ) {
      return;
    }

    this.pageSize.set(
      pageSize,
    );

    this.page.set(1);
    this.load();
  }

  private load(): void {
    this.requestAudit.next();
  }

  private buildQuery():
    AuditQuery {
    const value =
      this.filters
        .getRawValue();

    return {
      search:
        value.search,
      owner:
        value.owner,
      processStatus:
        value.processStatus,
      dispatchStatus:
        value.dispatchStatus,
      dateFrom:
        value.dateFrom,
      dateTo:
        value.dateTo,
      page:
        this.page(),
      pageSize:
        this.pageSize(),
    };
  }

  private replaceUrlState(): void {
    const query =
      this.buildQuery();

    const urlTree =
      this.router.createUrlTree(
        [],
        {
          relativeTo:
            this.route,

          queryParams: {
            search:
              query.search ||
              null,

            owner:
              query.owner ||
              null,

            processStatus:
              query.processStatus ||
              null,

            dispatchStatus:
              query.dispatchStatus ||
              null,

            dateFrom:
              query.dateFrom ||
              null,

            dateTo:
              query.dateTo ||
              null,

            page:
              query.page > 1
                ? query.page
                : null,

            pageSize:
              query.pageSize !== 25
                ? query.pageSize
                : null,
          },
        },
      );

    this.location.replaceState(
      this.router.serializeUrl(
        urlTree,
      ),
    );
  }

  private parsePositiveInteger(
    value:
      | string
      | null,
    fallback: number,
  ): number {
    const parsed =
      Number(value);

    return Number.isInteger(
      parsed,
    ) &&
      parsed > 0
      ? parsed
      : fallback;
  }

  private parsePageSize(
    value:
      | string
      | null,
  ): number {
    const parsed =
      Number(value);

    return parsed === 50 ||
      parsed === 100
      ? parsed
      : 25;
  }

  private describeError(
    error: HttpErrorResponse,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your PO Tracker session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'You do not have permission to view the audit page.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message ===
      'string'
      ? message
      : 'Material activity could not be loaded.';
  }
}
