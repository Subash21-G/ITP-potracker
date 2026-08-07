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

    const optionalValues:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['search', query.search],
        ['status', query.status],
        ['material', query.material],
        ['poNumber', query.poNumber],
        [
          'drawingVersion',
          query.drawingVersion,
        ],
        [
          'updatedDateFrom',
          query.updatedDateFrom,
        ],
        [
          'updatedDateTo',
          query.updatedDateTo,
        ],
        [
          'drawingAvailability',
          query.drawingAvailability,
        ],
        [
          'pdfAvailability',
          query.pdfAvailability,
        ],
        [
          'columnStatus',
          query.columnStatus,
        ],
      ];

    for (
      const [
        name,
        value,
      ] of optionalValues
    ) {
      const cleaned =
        value?.trim();

      if (cleaned) {
        params =
          params.set(
            name,
            cleaned,
          );
      }
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