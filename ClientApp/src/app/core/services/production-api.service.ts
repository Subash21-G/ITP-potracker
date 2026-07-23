import {
  HttpClient,
  HttpParams,
} from '@angular/common/http';
import {
  Injectable,
  inject,
} from '@angular/core';
import { Observable } from 'rxjs';

import {
  ProcessQueueQuery,
  ProcessQueueResponse,
  ProductionCalendarEvent,
} from '../models/production-api.model';

@Injectable({
  providedIn: 'root',
})
export class ProductionApiService {
  private readonly http = inject(HttpClient);

  getProcessQueue(
    query: ProcessQueueQuery = {},
  ): Observable<ProcessQueueResponse> {
    let params = new HttpParams()
      .set(
        'page',
        String(query.page ?? 1),
      )
      .set(
        'pageSize',
        String(query.pageSize ?? 50),
      );

    if (query.search?.trim()) {
      params = params.set(
        'search',
        query.search.trim(),
      );
    }

    return this.http.get<ProcessQueueResponse>(
      '/api/production/process-queue',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getCalendarEvents(
    start: string,
    end: string,
  ): Observable<ProductionCalendarEvent[]> {
    const params = new HttpParams()
      .set('start', start)
      .set('end', end);

    return this.http.get<ProductionCalendarEvent[]>(
      '/api/production/calendar-events',
      {
        params,
        withCredentials: true,
      },
    );
  }
}
