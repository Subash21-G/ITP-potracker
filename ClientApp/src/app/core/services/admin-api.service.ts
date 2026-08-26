import {
  HttpClient,
  HttpParams,
} from '@angular/common/http';

import {
  Injectable,
  inject,
} from '@angular/core';

import {
  Observable,
  switchMap,
} from 'rxjs';

import {
  AdminRole,
  AdminUserExcelFilterColumn,
  AdminUserExcelFilterValue,
  AdminUserMutationResponse,
  AdminUserSortDirection,
  AdminUsersResponse,
} from '../models/admin-api.model';

import {
  AuthApiService,
} from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class AdminApiService {
  private readonly http =
    inject(HttpClient);

  private readonly authApi =
    inject(AuthApiService);

  getUsers():
    Observable<AdminUsersResponse> {
    return this.http.get<AdminUsersResponse>(
      '/api/admin/users',
      {
        withCredentials: true,
      },
    );
  }

  getExcelUsers(
    sortColumn?: AdminUserExcelFilterColumn,
    sortDirection?: AdminUserSortDirection,
    excelFilters?: string,
  ): Observable<AdminUsersResponse> {
    let params =
      new HttpParams();

    const optionalValues:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['sortColumn', sortColumn],
        ['sortDirection', sortDirection],
        ['excelFilters', excelFilters],
      ];

    for (
      const [
        name,
        value,
      ] of optionalValues
    ) {
      const cleaned =
        value?.trim();

      if (cleaned) {
        params =
          params.set(
            name,
            cleaned,
          );
      }
    }

    return this.http.get<AdminUsersResponse>(
      '/api/admin/users-excel',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getUserExcelFilterValues(
    column: AdminUserExcelFilterColumn,
    excelFilters?: string,
  ): Observable<
    readonly AdminUserExcelFilterValue[]
  > {
    let params =
      new HttpParams()
        .set(
          'column',
          column,
        );

    const cleanedExcelFilters =
      excelFilters?.trim();

    if (cleanedExcelFilters) {
      params =
        params.set(
          'excelFilters',
          cleanedExcelFilters,
        );
    }

    return this.http.get<
      readonly AdminUserExcelFilterValue[]
    >(
      '/api/admin/users-excel/excel-filter-values',
      {
        params,
        withCredentials: true,
      },
    );
  }

  updateRole(
    userId: number,
    role: AdminRole,
  ): Observable<AdminUserMutationResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.put<AdminUserMutationResponse>(
            `/api/admin/users/${userId}/role`,
            {
              role,
            },
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }
}