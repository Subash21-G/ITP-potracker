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
  tap,
} from 'rxjs';

import {
  MaterialBulkDeleteRequest,
  MaterialBulkDeleteResponse,
  MaterialExcelFilterColumn,
  MaterialExcelFilterValue,
  MaterialListItem,
  MaterialProcessStatus,
  MaterialProcessStatusUpdateResponse,
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

  getExcelFilterValues(
    query: MaterialQuery,
    column: MaterialExcelFilterColumn,
  ): Observable<
    readonly MaterialExcelFilterValue[]
  > {
    const params =
      this.buildFilterParams(
        query,
      ).set(
        'column',
        column,
      );

    return this.http.get<
      readonly MaterialExcelFilterValue[]
    >(
      '/api/materials/excel-filter-values',
      {
        params,
        withCredentials: true,
      },
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

  updateProcessStatus(
    materialId: number,
    processStatus: MaterialProcessStatus,
  ): Observable<MaterialProcessStatusUpdateResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.patch<MaterialProcessStatusUpdateResponse>(
            `/api/materials/${materialId}/process-status`,
            {
              processStatus,
            },
            {
              withCredentials: true,
            },
          ),
        ),
        tap(() => {
          this.cache.invalidate('materials:');
        }),
      );
  }

  private buildFilterParams(
    query: MaterialQuery,
  ): HttpParams {
    let params =
      new HttpParams();

    const optionalValues:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['sortColumn', query.sortColumn],
        ['sortDirection', query.sortDirection],
      ['excelFilters', query.excelFilters],
      ['removeDuplicates', query.removeDuplicates ? 'true' : undefined],
      ['pendingOnly', query.pendingOnly ? 'true' : undefined],
        ['search', query.search],
        ['poNumber', query.poNumber],
        ['handler', query.handler],
        ['poDateFrom', query.poDateFrom],
        ['poDateTo', query.poDateTo],
        [
          'poReceivedDateFrom',
          query.poReceivedDateFrom,
        ],
        [
          'poReceivedDateTo',
          query.poReceivedDateTo,
        ],
        ['invoiceStatus', query.invoiceStatus],
        ['dispatchStatus', query.dispatchStatus],
        ['stockStatus', query.stockStatus],
        [
          'deliveryDateFrom',
          query.deliveryDateFrom,
        ],
        [
          'deliveryDateTo',
          query.deliveryDateTo,
        ],

        [
          'columnPoNumber',
          query.columnPoNumber,
        ],
        [
          'columnPurchaser',
          query.columnPurchaser,
        ],
        [
          'columnHandler',
          query.columnHandler,
        ],
        ['lineItem', query.lineItem],
        ['materialNo', query.materialNo],
        ['description', query.description],
        ['minOrderedQty', query.minOrderedQty],
        ['maxOrderedQty', query.maxOrderedQty],
        ['minUnitPrice', query.minUnitPrice],
        ['maxUnitPrice', query.maxUnitPrice],
        [
          'minMaterialValue',
          query.minMaterialValue,
        ],
        [
          'maxMaterialValue',
          query.maxMaterialValue,
        ],
        [
          'minDispatchQty',
          query.minDispatchQty,
        ],
        [
          'maxDispatchQty',
          query.maxDispatchQty,
        ],
        ['minPendingQty', query.minPendingQty],
        ['maxPendingQty', query.maxPendingQty],
        [
          'minAllocatedQty',
          query.minAllocatedQty,
        ],
        [
          'maxAllocatedQty',
          query.maxAllocatedQty,
        ],
        [
          'minLiveStockQty',
          query.minLiveStockQty,
        ],
        [
          'maxLiveStockQty',
          query.maxLiveStockQty,
        ],
        [
          'minShortageQty',
          query.minShortageQty,
        ],
        [
          'maxShortageQty',
          query.maxShortageQty,
        ],
        [
          'columnStockStatus',
          query.columnStockStatus,
        ],
        ['processStatus', query.processStatus],
        [
          'columnDispatchStatus',
          query.columnDispatchStatus,
        ],
        [
          'columnDeliveryDateFrom',
          query.columnDeliveryDateFrom,
        ],
        [
          'columnDeliveryDateTo',
          query.columnDeliveryDateTo,
        ],
      ];

    for (
      const [
        name,
        value,
      ] of optionalValues
    ) {
      params =
        this.setOptional(
          params,
          name,
          value,
        );
    }

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
