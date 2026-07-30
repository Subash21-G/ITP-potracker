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
  AnalyticsResponse,
  AuditResponse,
  DrawingListQuery,
  DrawingListResponse,
} from '../models/insights-api.model';

@Injectable({
  providedIn: 'root',
})
export class InsightsApiService {
  private readonly http =
    inject(HttpClient);

  getDrawings(
    query: DrawingListQuery,
  ): Observable<DrawingListResponse> {
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

    const search =
      query.search.trim();

    const status =
      query.status.trim();

    if (search) {
      params =
        params.set(
          'search',
          search,
        );
    }

    if (status) {
      params =
        params.set(
          'status',
          status,
        );
    }

    return this.http.get<DrawingListResponse>(
      '/api/drawings',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getAudit(
    limit = 25,
  ): Observable<AuditResponse> {
    return this.http.get<AuditResponse>(
      '/api/audit',
      {
        params: {
          limit,
        },
        withCredentials: true,
      },
    );
  }

  getAnalytics():
    Observable<AnalyticsResponse> {
    return this.http.get<AnalyticsResponse>(
      '/api/analytics',
      {
        withCredentials: true,
      },
    );
  }
}