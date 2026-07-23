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

    params =
      this.appendText(
        params,
        'search',
        query.search,
      );

    params =
      this.appendText(
        params,
        'poNumber',
        query.poNumber,
      );

    params =
      this.appendText(
        params,
        'handler',
        query.handler,
      );

    params =
      this.appendText(
        params,
        'assignedDashboard',
        query.assignedDashboard,
      );

    params =
      this.appendText(
        params,
        'processStatus',
        query.processStatus,
      );

    params =
      this.appendText(
        params,
        'invoiceStatus',
        query.invoiceStatus,
      );

    params =
      this.appendText(
        params,
        'dispatchStatus',
        query.dispatchStatus,
      );

    params =
      this.appendText(
        params,
        'poDateFrom',
        query.poDateFrom,
      );

    params =
      this.appendText(
        params,
        'poDateTo',
        query.poDateTo,
      );

    params =
      this.appendText(
        params,
        'poReceivedDateFrom',
        query.poReceivedDateFrom,
      );

    params =
      this.appendText(
        params,
        'poReceivedDateTo',
        query.poReceivedDateTo,
      );

    params =
      this.appendText(
        params,
        'deliveryDateFrom',
        query.deliveryDateFrom,
      );

    params =
      this.appendText(
        params,
        'deliveryDateTo',
        query.deliveryDateTo,
      );

    if (
      query.overdueOnly === true
    ) {
      params =
        params.set(
          'overdueOnly',
          'true',
        );
    }

    if (
      query.dueThisWeekOnly === true
    ) {
      params =
        params.set(
          'dueThisWeekOnly',
          'true',
        );
    }

    params =
      params.set(
        'page',
        String(query.page ?? 1),
      );

    params =
      params.set(
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

    this.appendUrlText(
      params,
      'search',
      query.search,
    );

    this.appendUrlText(
      params,
      'poNumber',
      query.poNumber,
    );

    this.appendUrlText(
      params,
      'handler',
      query.handler,
    );

    this.appendUrlText(
      params,
      'assignedDashboard',
      query.assignedDashboard,
    );

    this.appendUrlText(
      params,
      'processStatus',
      query.processStatus,
    );

    this.appendUrlText(
      params,
      'invoiceStatus',
      query.invoiceStatus,
    );

    this.appendUrlText(
      params,
      'dispatchStatus',
      query.dispatchStatus,
    );

    this.appendUrlText(
      params,
      'poDateFrom',
      query.poDateFrom,
    );

    this.appendUrlText(
      params,
      'poDateTo',
      query.poDateTo,
    );

    this.appendUrlText(
      params,
      'poReceivedDateFrom',
      query.poReceivedDateFrom,
    );

    this.appendUrlText(
      params,
      'poReceivedDateTo',
      query.poReceivedDateTo,
    );

    this.appendUrlText(
      params,
      'deliveryDateFrom',
      query.deliveryDateFrom,
    );

    this.appendUrlText(
      params,
      'deliveryDateTo',
      query.deliveryDateTo,
    );

    if (
      query.overdueOnly === true
    ) {
      params.set(
        'overdueOnly',
        'true',
      );
    }

    if (
      query.dueThisWeekOnly === true
    ) {
      params.set(
        'dueThisWeekOnly',
        'true',
      );
    }

    const queryString =
      params.toString();

    return queryString
      ? `/PendingMaterial/ExportExcel?${queryString}`
      : '/PendingMaterial/ExportExcel';
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
