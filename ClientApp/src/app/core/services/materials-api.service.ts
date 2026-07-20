import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { MaterialListItem, MaterialQuery, PagedResult } from '../models/material.model';

@Injectable({ providedIn: 'root' })
export class MaterialsApiService {
  private readonly http = inject(HttpClient);

  getMaterials(query: MaterialQuery): Observable<PagedResult<MaterialListItem>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);

    params = this.setOptional(params, 'search', query.search);
    params = this.setOptional(params, 'poNumber', query.poNumber);
    params = this.setOptional(params, 'handler', query.handler);
    params = this.setOptional(params, 'invoiceStatus', query.invoiceStatus);
    params = this.setOptional(params, 'dispatchStatus', query.dispatchStatus);
    params = this.setOptional(params, 'deliveryDateFrom', query.deliveryDateFrom);
    params = this.setOptional(params, 'deliveryDateTo', query.deliveryDateTo);

    return this.http.get<PagedResult<MaterialListItem>>('/api/materials', {
      params,
      withCredentials: true,
    });
  }

  private setOptional(params: HttpParams, name: string, value: string | undefined): HttpParams {
    const normalizedValue = value?.trim();

    return normalizedValue ? params.set(name, normalizedValue) : params;
  }
}
