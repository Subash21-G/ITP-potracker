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
  inject,
  signal,
} from '@angular/core';

import {
  takeUntilDestroyed,
} from '@angular/core/rxjs-interop';

import {
  RouterLink,
} from '@angular/router';

import {
  finalize,
} from 'rxjs';

import {
  AdminRole,
  AdminUser,
  AdminUserExcelFilterColumn,
  AdminUserExcelFilterSelections,
  AdminUserExcelFilterValue,
  AdminUserSortDirection,
  AdminUserSummary,
} from '../../../core/models/admin-api.model';

import {
  AdminApiService,
} from '../../../core/services/admin-api.service';
import {
  ServerExcelFilter,
} from '../../../shared/server-excel-filter/server-excel-filter';

@Component({
  selector: 'app-admin-users-page',
  imports: [
    DecimalPipe,
    RouterLink,
    ServerExcelFilter,
  ],
  templateUrl: './admin-users-page.html',
  styleUrl: '../admin-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class AdminUsersPage {
  private readonly api =
    inject(AdminApiService);

  private readonly destroyRef =
    inject(DestroyRef);

  readonly users =
    signal<readonly AdminUser[]>([]);

  readonly summary =
    signal<AdminUserSummary>({
      totalUsers: 0,
      administrators: 0,
      productionUsers: 0,
      viewers: 0,
    });

  readonly allowedRoles =
    signal<readonly AdminRole[]>([
      'Viewer',
      'Production',
      'Admin',
    ]);

  readonly selectedRoles =
    signal<
      Readonly<Record<number, AdminRole>>
    >({});

  readonly loading =
    signal(false);

  readonly updatingUserId =
    signal<number | null>(
      null,
    );

  readonly errorMessage =
    signal('');

  readonly successMessage =
    signal('');

  readonly sortColumn =
    signal<
      AdminUserExcelFilterColumn | null
    >(
      null,
    );

  readonly sortDirection =
    signal<
      AdminUserSortDirection | null
    >(
      null,
    );

  readonly excelFilterSelections =
    signal<
      AdminUserExcelFilterSelections
    >(
      {},
    );

  readonly excelFilterOptions =
    signal<
      Partial<
        Record<
          AdminUserExcelFilterColumn,
          readonly AdminUserExcelFilterValue[]
        >
      >
    >({});

  readonly excelFilterLoadingColumns =
    signal<
      ReadonlySet<
        AdminUserExcelFilterColumn
      >
    >(
      new Set<
        AdminUserExcelFilterColumn
      >(),
    );

  constructor() {
    this.load();
  }

  selectedRole(
    user: AdminUser,
  ): AdminRole {
    return (
      this.selectedRoles()[
        user.id
      ] ??
      user.role
    );
  }

  changeSelectedRole(
    userId: number,
    event: Event,
  ): void {
    const value =
      (
        event.target as
          HTMLSelectElement
      ).value as AdminRole;

    if (
      !this.allowedRoles()
        .includes(value)
    ) {
      return;
    }

    this.selectedRoles.update(
      current => ({
        ...current,
        [userId]:
          value,
      }),
    );
  }

  saveRole(
    user: AdminUser,
  ): void {
    if (
      user.isCurrentUser ||
      this.updatingUserId() !== null
    ) {
      return;
    }

    const role =
      this.selectedRole(
        user,
      );

    if (
      user.role === 'Admin' &&
      role !== 'Admin'
    ) {
      const confirmed =
        window.confirm(
          `Change ${user.fullName} from Admin to ${role}?`,
        );

      if (!confirmed) {
        return;
      }
    }

    this.updatingUserId.set(
      user.id,
    );

    this.errorMessage.set('');
    this.successMessage.set('');

    this.api
      .updateRole(
        user.id,
        role,
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.updatingUserId.set(
            null,
          ),
        ),
      )
      .subscribe({
        next: response => {
          this.successMessage.set(
            response.message,
          );

          this.load(
            false,
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
              'The user role could not be updated.',
            ),
          );
        },
      });
  }

  retry(): void {
    this.load();
  }

  excelFilterOptionsFor(
    column:
      AdminUserExcelFilterColumn,
  ): readonly AdminUserExcelFilterValue[] {
    return (
      this.excelFilterOptions()[
        column
      ] ?? []
    );
  }

  excelFilterSelectedValues(
    column:
      AdminUserExcelFilterColumn,
  ): readonly string[] {
    return (
      this.excelFilterSelections()[
        column
      ] ?? []
    );
  }

  excelFilterActive(
    column:
      AdminUserExcelFilterColumn,
  ): boolean {
    return Object.prototype
      .hasOwnProperty.call(
        this.excelFilterSelections(),
        column,
      );
  }

  excelFilterLoading(
    column:
      AdminUserExcelFilterColumn,
  ): boolean {
    return this
      .excelFilterLoadingColumns()
      .has(
        column,
      );
  }

  excelSortDirection(
    column:
      AdminUserExcelFilterColumn,
  ): AdminUserSortDirection | null {
    return this.sortColumn() ===
      column
      ? this.sortDirection()
      : null;
  }

  loadExcelFilterValues(
    column:
      AdminUserExcelFilterColumn,
  ): void {
    if (
      this.excelFilterLoading(
        column,
      )
    ) {
      return;
    }

    this.excelFilterLoadingColumns
      .update(
        columns => {
          const updated =
            new Set(
              columns,
            );

          updated.add(
            column,
          );

          return updated;
        },
      );

    this.api
      .getUserExcelFilterValues(
        column,
        this.serializeExcelFilters(),
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(
          () =>
            this.excelFilterLoadingColumns
              .update(
                columns => {
                  const updated =
                    new Set(
                      columns,
                    );

                  updated.delete(
                    column,
                  );

                  return updated;
                },
              ),
        ),
      )
      .subscribe({
        next: options => {
          this.excelFilterOptions
            .update(
              current => ({
                ...current,
                [column]:
                  options,
              }),
            );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.errorMessage.set(
            this.describeError(
              error,
              'User filter values could not be loaded.',
            ),
          );
        },
      });
  }

  applyExcelSort(
    column:
      AdminUserExcelFilterColumn,
    direction:
      AdminUserSortDirection,
  ): void {
    if (this.loading()) {
      return;
    }

    this.sortColumn.set(
      column,
    );

    this.sortDirection.set(
      direction,
    );

    this.successMessage.set('');
    this.load();
  }

  applyExcelFilter(
    column:
      AdminUserExcelFilterColumn,
    values: readonly string[],
  ): void {
    const availableValues =
      this.excelFilterOptions()[
        column
      ] ?? [];

    const selectedSet =
      new Set(
        values,
      );

    const selectsEverything =
      availableValues.length >
        0 &&
      availableValues.length ===
        selectedSet.size &&
      availableValues.every(
        option =>
          selectedSet.has(
            option.value,
          ),
      );

    this.excelFilterSelections
      .update(
        current => {
          const updated = {
            ...current,
          };

          if (selectsEverything) {
            delete updated[
              column
            ];
          } else {
            updated[column] =
              [...values];
          }

          return updated;
        },
      );

    this.successMessage.set('');
    this.load();
  }

  clearExcelFilter(
    column:
      AdminUserExcelFilterColumn,
  ): void {
    this.excelFilterSelections
      .update(
        current => {
          const updated = {
            ...current,
          };

          delete updated[
            column
          ];

          return updated;
        },
      );

    this.successMessage.set('');
    this.load();
  }

  private load(
    clearMessages = true,
  ): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(
      true,
    );

    if (clearMessages) {
      this.errorMessage.set('');
    }

    this.api
      .getExcelUsers(
        this.sortColumn() ??
          undefined,
        this.sortDirection() ??
          undefined,
        this.serializeExcelFilters(),
      )
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),

        finalize(() =>
          this.loading.set(
            false,
          ),
        ),
      )
      .subscribe({
        next: response => {
          this.users.set(
            response.users,
          );

          this.summary.set(
            response.summary,
          );

          this.allowedRoles.set(
            response.allowedRoles,
          );

          this.selectedRoles.set(
            Object.fromEntries(
              response.users.map(
                user => [
                  user.id,
                  user.role,
                ],
              ),
            ),
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          this.users.set([]);

          this.errorMessage.set(
            this.describeError(
              error,
              'User records could not be loaded.',
            ),
          );
        },
      });
  }

  private serializeExcelFilters():
    string | undefined {
    const selections =
      this.excelFilterSelections();

    return Object.keys(
      selections,
    ).length > 0
      ? JSON.stringify(
          selections,
        )
      : undefined;
  }

  private describeError(
    error: HttpErrorResponse,
    fallbackMessage: string,
  ): string {
    if (error.status === 0) {
      return 'The PO Tracker API is unavailable.';
    }

    if (error.status === 401) {
      return 'Your session has expired. Sign in again.';
    }

    if (error.status === 403) {
      return 'Only administrators can manage users.';
    }

    const message =
      error.error?.message ??
      error.error?.error;

    return typeof message ===
      'string'
      ? message
      : fallbackMessage;
  }
}