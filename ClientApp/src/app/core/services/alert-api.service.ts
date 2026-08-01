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
  AlertsPagedResponse,
  AlertsQuery,
} from '../models/alert-api.model';

@Injectable({
  providedIn: 'root',
})
export class AlertApiService {
  private readonly http =
    inject(HttpClient);

  getAlerts(
    query: AlertsQuery,
  ): Observable<AlertsPagedResponse> {
    let params =
      new HttpParams()
        .set(
          'category',
          query.category,
        )
        .set(
          'page',
          query.page.toString(),
        )
        .set(
          'pageSize',
          query.pageSize.toString(),
        );

    const search =
      query.search?.trim();

    if (search) {
      params =
        params.set(
          'search',
          search,
        );
    }

    return this.http.get<
      AlertsPagedResponse
    >(
      '/api/alerts',
      {
        params,
        withCredentials: true,
      },
    );
  }
}
