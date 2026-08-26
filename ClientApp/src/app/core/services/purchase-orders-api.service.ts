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
  PurchaseOrderDetails,
  PurchaseOrderMaterialQuery,
} from '../models/purchase-order-details.model';

import {
  PurchaseOrderBulkDeleteRequest,
  PurchaseOrderBulkDeleteResponse,
  PurchaseOrderExcelFilterColumn,
  PurchaseOrderExcelFilterValue,
  PurchaseOrderListResponse,
  PurchaseOrderQuery,
} from '../models/purchase-order.model';

import {
  ApiResponseCacheService,
} from './api-response-cache.service';

import {
  AuthApiService,
} from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class PurchaseOrdersApiService {
  private readonly http =
    inject(HttpClient);

  private readonly authApi =
    inject(AuthApiService);

  private readonly cache =
    inject(
      ApiResponseCacheService,
    );

  getPurchaseOrders(
    query: PurchaseOrderQuery,
    forceRefresh = false,
  ): Observable<PurchaseOrderListResponse> {
    let params =
      new HttpParams()
        .set(
          'page',
          query.page,
        )
        .set(
          'pageSize',
          query.pageSize,
        );

    params =
      this.setOptional(
        params,
        'sortColumn',
        query.sortColumn,
      );

    params =
      this.setOptional(
        params,
        'sortDirection',
        query.sortDirection,
      );

    params =
      this.setOptional(
        params,
        'excelFilters',
        query.excelFilters,
      );

    params =
      this.setOptional(
        params,
        'search',
        query.search,
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
        'status',
        query.status,
      );

    params =
      this.setOptional(
        params,
        'materialStatus',
        query.materialStatus,
      );

    params =
      this.setOptional(
        params,
        'minMaterials',
        query.minMaterials,
      );

    params =
      this.setOptional(
        params,
        'maxMaterials',
        query.maxMaterials,
      );

    params =
      this.setOptional(
        params,
        'minPoQuantity',
        query.minPoQuantity,
      );

    params =
      this.setOptional(
        params,
        'maxPoQuantity',
        query.maxPoQuantity,
      );

    params =
      this.setOptional(
        params,
        'minPoValue',
        query.minPoValue,
      );

    params =
      this.setOptional(
        params,
        'maxPoValue',
        query.maxPoValue,
      );

    params =
      this.setOptional(
        params,
        'minDispatchBalance',
        query.minDispatchBalance,
      );

    params =
      this.setOptional(
        params,
        'maxDispatchBalance',
        query.maxDispatchBalance,
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

    params =
      this.setOptional(
        params,
        'pdfAvailability',
        query.pdfAvailability,
      );

    const cacheKey =
      `purchase-orders:list:${params.toString()}`;

    return this.cache.get(
      cacheKey,
      () =>
        this.http.get<
          PurchaseOrderListResponse
        >(
          '/api/purchase-orders',
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
    query: PurchaseOrderQuery,
    column: PurchaseOrderExcelFilterColumn,
  ): Observable<
    readonly PurchaseOrderExcelFilterValue[]
  > {
    let params =
      new HttpParams()
        .set(
          'page',
          1,
        )
        .set(
          'pageSize',
          query.pageSize,
        )
        .set(
          'column',
          column,
        );

    const values:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['sortColumn', query.sortColumn],
        ['sortDirection', query.sortDirection],
        ['excelFilters', query.excelFilters],
        ['search', query.search],
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
        ['status', query.status],
        [
          'materialStatus',
          query.materialStatus,
        ],
        ['minMaterials', query.minMaterials],
        ['maxMaterials', query.maxMaterials],
        ['minPoQuantity', query.minPoQuantity],
        ['maxPoQuantity', query.maxPoQuantity],
        ['minPoValue', query.minPoValue],
        ['maxPoValue', query.maxPoValue],
        [
          'minDispatchBalance',
          query.minDispatchBalance,
        ],
        [
          'maxDispatchBalance',
          query.maxDispatchBalance,
        ],
        [
          'deliveryDateFrom',
          query.deliveryDateFrom,
        ],
        [
          'deliveryDateTo',
          query.deliveryDateTo,
        ],
        [
          'pdfAvailability',
          query.pdfAvailability,
        ],
      ];

    for (
      const [
        name,
        value,
      ] of values
    ) {
      params =
        this.setOptional(
          params,
          name,
          value,
        );
    }

    return this.http.get<
      readonly PurchaseOrderExcelFilterValue[]
    >(
      '/api/purchase-orders/excel-filter-values',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getPurchaseOrderDetails(
    id: number,
    materialPage: number,
    materialPageSize: number,
    forceRefresh = false,
  ): Observable<PurchaseOrderDetails> {
    return this.getPurchaseOrderDetailsFiltered(
      id,
      {
        materialPage,
        materialPageSize,
      },
      forceRefresh,
    );
  }

  getPurchaseOrderDetailsFiltered(
    id: number,
    query: PurchaseOrderMaterialQuery,
    forceRefresh = false,
  ): Observable<PurchaseOrderDetails> {
    let params =
      new HttpParams()
        .set(
          'materialPage',
          query.materialPage,
        )
        .set(
          'materialPageSize',
          query.materialPageSize,
        );

    const values:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['lineItem', query.lineItem],
        ['material', query.material],
        ['description', query.description],
        ['minQuantity', query.minQuantity],
        ['maxQuantity', query.maxQuantity],
        ['minUnitPrice', query.minUnitPrice],
        ['maxUnitPrice', query.maxUnitPrice],
        [
          'minInvoiceBalance',
          query.minInvoiceBalance,
        ],
        [
          'maxInvoiceBalance',
          query.maxInvoiceBalance,
        ],
        [
          'minDispatchBalance',
          query.minDispatchBalance,
        ],
        [
          'maxDispatchBalance',
          query.maxDispatchBalance,
        ],
        [
          'deliveryDateFrom',
          query.deliveryDateFrom,
        ],
        [
          'deliveryDateTo',
          query.deliveryDateTo,
        ],
        ['processStatus', query.processStatus],
        ['invoiceStatus', query.invoiceStatus],
        ['dispatchStatus', query.dispatchStatus],
      ];

    for (
      const [
        name,
        value,
      ] of values
    ) {
      params =
        this.setOptional(
          params,
          name,
          value,
        );
    }

    if (
      query.overdueStatus &&
      query.overdueStatus !== 'all'
    ) {
      params =
        params.set(
          'overdueStatus',
          query.overdueStatus,
        );
    }

    const cacheKey =
      (
        `purchase-orders:detail:${id}:` +
        params.toString()
      );

    return this.cache.get(
      cacheKey,
      () =>
        this.http.get<
          PurchaseOrderDetails
        >(
          `/api/purchase-orders/${id}`,
          {
            params,
            withCredentials: true,
          },
        ),
      20_000,
      forceRefresh,
    );
  }

  bulkDelete(
    request: PurchaseOrderBulkDeleteRequest,
  ): Observable<PurchaseOrderBulkDeleteResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<
            PurchaseOrderBulkDeleteResponse
          >(
            '/api/purchase-orders/bulk-delete',
            request,
            {
              withCredentials: true,
            },
          ),
        ),
      );
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