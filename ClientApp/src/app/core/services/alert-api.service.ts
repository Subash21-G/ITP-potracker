import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AlertsPagedResponse, AlertsQuery } from '../models/alert-api.model';

@Injectable({ providedIn: 'root' })
export class AlertApiService {
  private readonly http = inject(HttpClient);

  getAlerts(query: AlertsQuery): Observable<AlertsPagedResponse> {
    let params = new HttpParams()
      .set('category', query.category)
      .set('page', query.page.toString())
      .set('pageSize', query.pageSize.toString());

    const values: readonly [string, string | undefined][] = [
      ['sortColumn', query.sortColumn],
      ['sortDirection', query.sortDirection],
      ['search', query.search],
      ['material', query.material],
      ['description', query.description],
      ['priorityDateFrom', query.priorityDateFrom],
      ['priorityDateTo', query.priorityDateTo],
      ['deliveryDateFrom', query.deliveryDateFrom],
      ['deliveryDateTo', query.deliveryDateTo],
      ['processStatus', query.processStatus],
      ['dispatchStatus', query.dispatchStatus],
      ['minAllocatedStock', query.minAllocatedStock],
      ['maxAllocatedStock', query.maxAllocatedStock],
      ['minLiveStock', query.minLiveStock],
      ['maxLiveStock', query.maxLiveStock],
      ['stockStatus', query.stockStatus],
    ];

    for (const [name, value] of values) {
      const cleaned = value?.trim();
      if (cleaned) {
        params = params.set(name, cleaned);
      }
    }

    return this.http.get<AlertsPagedResponse>('/api/alerts', {
      params,
      withCredentials: true,
    });
  }
}