import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';

import {
  MaterialHistoryExcelFilterColumn,
  MaterialHistoryExcelFilterValue,
  MaterialHistoryListQuery,
  MaterialHistoryListResponse,
  MaterialHistoryMaterialOption,
  MaterialHistorySaveResponse,
  MaterialHistoryUpsertRequest,
} from '../models/material-history-api.model';
import { AuthApiService } from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class MaterialHistoryApiService {
  private readonly http = inject(HttpClient);

  private readonly authApi = inject(AuthApiService);

  getHistory(query: MaterialHistoryListQuery): Observable<MaterialHistoryListResponse> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);

    if (query.search?.trim()) {
      params = params.set('search', query.search.trim());
    }

    if (query.outcome) {
      params = params.set('outcome', query.outcome);
    }

    if (query.materialId) {
      params = params.set('materialId', query.materialId);
    }

    if (query.sortColumn) {
      params = params.set('sortColumn', query.sortColumn);
    }

    if (query.sortDirection) {
      params = params.set('sortDirection', query.sortDirection);
    }

    if (query.excelFilters?.trim()) {
      params = params.set('excelFilters', query.excelFilters.trim());
    }

    return this.http.get<MaterialHistoryListResponse>('/api/material-history', {
      params,
      withCredentials: true,
    });
  }

  getExcelFilterValues(
    query: MaterialHistoryListQuery,
    column: MaterialHistoryExcelFilterColumn,
  ): Observable<readonly MaterialHistoryExcelFilterValue[]> {
    let params = new HttpParams().set('column', column);

    if (query.search?.trim()) {
      params = params.set('search', query.search.trim());
    }

    if (query.outcome) {
      params = params.set('outcome', query.outcome);
    }

    if (query.materialId) {
      params = params.set('materialId', query.materialId);
    }

    if (query.excelFilters?.trim()) {
      params = params.set('excelFilters', query.excelFilters.trim());
    }

    return this.http.get<readonly MaterialHistoryExcelFilterValue[]>(
      '/api/material-history/excel-filter-values',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getMaterialOptions(): Observable<readonly MaterialHistoryMaterialOption[]> {
    return this.http.get<readonly MaterialHistoryMaterialOption[]>(
      '/api/material-history/material-options',
      { withCredentials: true },
    );
  }

  create(request: MaterialHistoryUpsertRequest): Observable<MaterialHistorySaveResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<MaterialHistorySaveResponse>('/api/material-history', request, {
          withCredentials: true,
        }),
      ),
    );
  }

  update(
    id: number,
    request: MaterialHistoryUpsertRequest,
  ): Observable<MaterialHistorySaveResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.put<MaterialHistorySaveResponse>(`/api/material-history/${id}`, request, {
          withCredentials: true,
        }),
      ),
    );
  }
}
