import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { PurchaseOrderDetails } from '../models/purchase-order-details.model';
import { PurchaseOrderListResponse, PurchaseOrderQuery } from '../models/purchase-order.model';

@Injectable({ providedIn: 'root' })
export class PurchaseOrdersApiService {
  private readonly http = inject(HttpClient);

  getPurchaseOrders(query: PurchaseOrderQuery): Observable<PurchaseOrderListResponse> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);

    params = this.setOptional(params, 'search', query.search);
    params = this.setOptional(params, 'handler', query.handler);
    params = this.setOptional(params, 'poDateFrom', query.poDateFrom);
    params = this.setOptional(params, 'poDateTo', query.poDateTo);
    params = this.setOptional(params, 'poReceivedDateFrom', query.poReceivedDateFrom);
    params = this.setOptional(params, 'poReceivedDateTo', query.poReceivedDateTo);
    params = this.setOptional(params, 'status', query.status);
    params = this.setOptional(params, 'materialStatus', query.materialStatus);

    return this.http.get<PurchaseOrderListResponse>('/api/purchase-orders', {
      params,
      withCredentials: true,
    });
  }

  getPurchaseOrderDetails(
    id: number,
    materialPage: number,
    materialPageSize: number,
  ): Observable<PurchaseOrderDetails> {
    const params = new HttpParams()
      .set('materialPage', materialPage)
      .set('materialPageSize', materialPageSize);

    return this.http.get<PurchaseOrderDetails>(`/api/purchase-orders/${id}`, {
      params,
      withCredentials: true,
    });
  }

  private setOptional(params: HttpParams, name: string, value: string | undefined): HttpParams {
    const normalizedValue = value?.trim();

    return normalizedValue ? params.set(name, normalizedValue) : params;
  }
}
