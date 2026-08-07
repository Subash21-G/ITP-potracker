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
} from 'rxjs';

import {
  PendingMaterialQuery,
  PendingMaterialsResponse,
} from '../models/pending-material-api.model';

@Injectable({
  providedIn: 'root',
})
export class PendingMaterialApiService {
  private readonly http =
    inject(HttpClient);

  getPendingMaterials(
    query: PendingMaterialQuery = {},
  ): Observable<PendingMaterialsResponse> {
    let params =
      new HttpParams();

    const textValues:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['search', query.search],
        ['poNumber', query.poNumber],
        ['handler', query.handler],
        [
          'assignedDashboard',
          query.assignedDashboard,
        ],
        ['processStatus', query.processStatus],
        ['invoiceStatus', query.invoiceStatus],
        ['dispatchStatus', query.dispatchStatus],
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
        [
          'deliveryDateFrom',
          query.deliveryDateFrom,
        ],
        [
          'deliveryDateTo',
          query.deliveryDateTo,
        ],

        ['dueStatus', query.dueStatus],
        [
          'columnPoNumber',
          query.columnPoNumber,
        ],
        [
          'columnHandler',
          query.columnHandler,
        ],
        [
          'columnMaterialNo',
          query.columnMaterialNo,
        ],
        [
          'columnLineItem',
          query.columnLineItem,
        ],
        [
          'columnDescription',
          query.columnDescription,
        ],
        ['minPoQuantity', query.minPoQuantity],
        ['maxPoQuantity', query.maxPoQuantity],
        [
          'minInvoiceQuantity',
          query.minInvoiceQuantity,
        ],
        [
          'maxInvoiceQuantity',
          query.maxInvoiceQuantity,
        ],
        [
          'minInvoiceBalanceQuantity',
          query.minInvoiceBalanceQuantity,
        ],
        [
          'maxInvoiceBalanceQuantity',
          query.maxInvoiceBalanceQuantity,
        ],
        [
          'minDispatchQuantity',
          query.minDispatchQuantity,
        ],
        [
          'maxDispatchQuantity',
          query.maxDispatchQuantity,
        ],
        [
          'minPendingDispatchQuantity',
          query.minPendingDispatchQuantity,
        ],
        [
          'maxPendingDispatchQuantity',
          query.maxPendingDispatchQuantity,
        ],
        [
          'minCompletionPercentage',
          query.minCompletionPercentage,
        ],
        [
          'maxCompletionPercentage',
          query.maxCompletionPercentage,
        ],
        [
          'columnProcessStatus',
          query.columnProcessStatus,
        ],
        [
          'columnInvoiceStatus',
          query.columnInvoiceStatus,
        ],
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
        [
          'columnAssignedDashboard',
          query.columnAssignedDashboard,
        ],
      ];

    for (
      const [
        name,
        value,
      ] of textValues
    ) {
      params =
        this.appendText(
          params,
          name,
          value,
        );
    }

    if (query.overdueOnly === true) {
      params =
        params.set(
          'overdueOnly',
          'true',
        );
    }

    if (query.dueThisWeekOnly === true) {
      params =
        params.set(
          'dueThisWeekOnly',
          'true',
        );
    }

    params =
      params
        .set(
          'page',
          String(query.page ?? 1),
        )
        .set(
          'pageSize',
          String(query.pageSize ?? 25),
        );

    return this.http.get<PendingMaterialsResponse>(
      '/api/pending-materials',
      {
        params,
        withCredentials: true,
      },
    );
  }

  buildExcelExportUrl(
    query: PendingMaterialQuery,
  ): string {
    const params =
      new URLSearchParams();

    const textValues:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['search', query.search],
        ['poNumber', query.poNumber],
        ['handler', query.handler],
        [
          'assignedDashboard',
          query.assignedDashboard,
        ],
        ['processStatus', query.processStatus],
        ['invoiceStatus', query.invoiceStatus],
        ['dispatchStatus', query.dispatchStatus],
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
        [
          'deliveryDateFrom',
          query.deliveryDateFrom,
        ],
        [
          'deliveryDateTo',
          query.deliveryDateTo,
        ],

        ['dueStatus', query.dueStatus],
        [
          'columnPoNumber',
          query.columnPoNumber,
        ],
        [
          'columnHandler',
          query.columnHandler,
        ],
        [
          'columnMaterialNo',
          query.columnMaterialNo,
        ],
        [
          'columnLineItem',
          query.columnLineItem,
        ],
        [
          'columnDescription',
          query.columnDescription,
        ],
        ['minPoQuantity', query.minPoQuantity],
        ['maxPoQuantity', query.maxPoQuantity],
        [
          'minInvoiceQuantity',
          query.minInvoiceQuantity,
        ],
        [
          'maxInvoiceQuantity',
          query.maxInvoiceQuantity,
        ],
        [
          'minInvoiceBalanceQuantity',
          query.minInvoiceBalanceQuantity,
        ],
        [
          'maxInvoiceBalanceQuantity',
          query.maxInvoiceBalanceQuantity,
        ],
        [
          'minDispatchQuantity',
          query.minDispatchQuantity,
        ],
        [
          'maxDispatchQuantity',
          query.maxDispatchQuantity,
        ],
        [
          'minPendingDispatchQuantity',
          query.minPendingDispatchQuantity,
        ],
        [
          'maxPendingDispatchQuantity',
          query.maxPendingDispatchQuantity,
        ],
        [
          'minCompletionPercentage',
          query.minCompletionPercentage,
        ],
        [
          'maxCompletionPercentage',
          query.maxCompletionPercentage,
        ],
        [
          'columnProcessStatus',
          query.columnProcessStatus,
        ],
        [
          'columnInvoiceStatus',
          query.columnInvoiceStatus,
        ],
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
        [
          'columnAssignedDashboard',
          query.columnAssignedDashboard,
        ],
      ];

    for (
      const [
        name,
        value,
      ] of textValues
    ) {
      this.appendUrlText(
        params,
        name,
        value,
      );
    }

    if (query.overdueOnly === true) {
      params.set(
        'overdueOnly',
        'true',
      );
    }

    if (query.dueThisWeekOnly === true) {
      params.set(
        'dueThisWeekOnly',
        'true',
      );
    }

    const queryString =
      params.toString();

    return queryString
      ? `/api/pending-materials/export-excel?${queryString}`
      : '/api/pending-materials/export-excel';
  }

  private appendText(
    params: HttpParams,
    key: string,
    value: string | undefined,
  ): HttpParams {
    const normalized =
      value?.trim();

    return normalized
      ? params.set(
          key,
          normalized,
        )
      : params;
  }

  private appendUrlText(
    params: URLSearchParams,
    key: string,
    value: string | undefined,
  ): void {
    const normalized =
      value?.trim();

    if (normalized) {
      params.set(
        key,
        normalized,
      );
    }
  }
}
