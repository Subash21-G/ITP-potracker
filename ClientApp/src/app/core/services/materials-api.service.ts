import {
  HttpClient,
  HttpParams,
} from '@angular/common/http';

import {
  inject,
  Injectable,
} from '@angular/core';

import {
  Observable,
  switchMap,
} from 'rxjs';

import {
  MaterialBulkDeleteRequest,
  MaterialBulkDeleteResponse,
  MaterialListItem,
  MaterialQuery,
  PagedResult,
} from '../models/material.model';

import {
  ApiResponseCacheService,
} from './api-response-cache.service';

import {
  AuthApiService,
} from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class MaterialsApiService {
  private readonly http =
    inject(HttpClient);

  private readonly authApi =
    inject(AuthApiService);

  private readonly cache =
    inject(
      ApiResponseCacheService,
    );

  getMaterials(
    query: MaterialQuery,
    forceRefresh = false,
  ): Observable<PagedResult<MaterialListItem>> {
    const params =
      this.buildFilterParams(query)
        .set(
          'page',
          query.page,
        )
        .set(
          'pageSize',
          query.pageSize,
        );

    const cacheKey =
      `materials:list:${params.toString()}`;

    return this.cache.get(
      cacheKey,
      () =>
        this.http.get<
          PagedResult<MaterialListItem>
        >(
          '/api/materials',
          {
            params,
            withCredentials: true,
          },
        ),
      10_000,
      forceRefresh,
    );
  }

  downloadExcel(
    query: MaterialQuery,
  ): Observable<Blob> {
    return this.http.get(
      '/api/materials/export-excel',
      {
        params:
          this.buildFilterParams(
            query,
          ),

        responseType: 'blob',
        withCredentials: true,
      },
    );
  }

  bulkDelete(
    request: MaterialBulkDeleteRequest,
  ): Observable<MaterialBulkDeleteResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<
            MaterialBulkDeleteResponse
          >(
            '/api/materials/bulk-delete',
            request,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  private buildFilterParams(
    query: MaterialQuery,
  ): HttpParams {
    let params =
      new HttpParams();

    params =
      this.setOptional(
        params,
        'search',
        query.search,
      );

    params =
      this.setOptional(
        params,
        'poNumber',
        query.poNumber,
      );

    params =
      this.setOptional(
        params,
        'handler',
        query.handler,
      );

    params =
      this.setOptional(
        params,
        'poDateFrom',
        query.poDateFrom,
      );

    params =
      this.setOptional(
        params,
        'poDateTo',
        query.poDateTo,
      );

    params =
      this.setOptional(
        params,
        'poReceivedDateFrom',
        query.poReceivedDateFrom,
      );

    params =
      this.setOptional(
        params,
        'poReceivedDateTo',
        query.poReceivedDateTo,
      );

    params =
      this.setOptional(
        params,
        'invoiceStatus',
        query.invoiceStatus,
      );

    params =
      this.setOptional(
        params,
        'dispatchStatus',
        query.dispatchStatus,
      );

    params =
      this.setOptional(
        params,
        'stockStatus',
        query.stockStatus,
      );

    params =
      this.setOptional(
        params,
        'deliveryDateFrom',
        query.deliveryDateFrom,
      );

    params =
      this.setOptional(
        params,
        'deliveryDateTo',
        query.deliveryDateTo,
      );

    return params;
  }

  private setOptional(
    params: HttpParams,
    name: string,
    value: string | undefined,
  ): HttpParams {
    const normalizedValue =
      value?.trim();

    return normalizedValue
      ? params.set(
          name,
          normalizedValue,
        )
      : params;
  }
}