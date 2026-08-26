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
  ProcessQueueExcelFilterColumn,
  ProcessQueueExcelFilterValue,
  ProcessQueueQuery,
  ProcessQueueResponse,
  ProductionCalendarDashboard,
  ProductionCalendarEvent,
  ProductionCalendarQuery,
} from '../models/production-api.model';

@Injectable({
  providedIn: 'root',
})
export class ProductionApiService {
  private readonly http = inject(HttpClient);

  getProcessQueue(
    query: ProcessQueueQuery = {},
  ): Observable<ProcessQueueResponse> {
    let params =
      new HttpParams()
        .set(
          'page',
          String(query.page ?? 1),
        )
        .set(
          'pageSize',
          String(query.pageSize ?? 50),
        );

    const values:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['search', query.search],
        ['material', query.material],
        ['description', query.description],
        ['poNumber', query.poNumber],
        ['purchaser', query.purchaser],
        [
          'processStartFrom',
          query.processStartFrom,
        ],
        [
          'processStartTo',
          query.processStartTo,
        ],
        ['deliveryFrom', query.deliveryFrom],
        ['deliveryTo', query.deliveryTo],
        ['owner', query.owner],
        [
          'processStatus',
          query.processStatus,
        ],
        [
          'dispatchStatus',
          query.dispatchStatus,
        ],
        ['sortColumn', query.sortColumn],
        [
          'sortDirection',
          query.sortDirection,
        ],
        ['excelFilters', query.excelFilters],
      ];

    for (
      const [
        name,
        value,
      ] of values
    ) {
      const normalized =
        value?.trim();

      if (normalized) {
        params =
          params.set(
            name,
            normalized,
          );
      }
    }

    return this.http.get<ProcessQueueResponse>(
      '/api/production/process-queue',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getProcessQueueExcelFilterValues(
    query: ProcessQueueQuery,
    column: ProcessQueueExcelFilterColumn,
  ): Observable<
    readonly ProcessQueueExcelFilterValue[]
  > {
    let params =
      new HttpParams()
        .set(
          'column',
          column,
        );

    const values:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['search', query.search],
        ['material', query.material],
        ['description', query.description],
        ['poNumber', query.poNumber],
        ['purchaser', query.purchaser],
        [
          'processStartFrom',
          query.processStartFrom,
        ],
        [
          'processStartTo',
          query.processStartTo,
        ],
        ['deliveryFrom', query.deliveryFrom],
        ['deliveryTo', query.deliveryTo],
        ['owner', query.owner],
        [
          'processStatus',
          query.processStatus,
        ],
        [
          'dispatchStatus',
          query.dispatchStatus,
        ],
        ['excelFilters', query.excelFilters],
      ];

    for (
      const [
        name,
        value,
      ] of values
    ) {
      const normalized =
        value?.trim();

      if (normalized) {
        params =
          params.set(
            name,
            normalized,
          );
      }
    }

    return this.http.get<
      readonly ProcessQueueExcelFilterValue[]
    >(
      '/api/production/process-queue/excel-filter-values',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getCalendarEvents(
    query: ProductionCalendarQuery,
  ): Observable<ProductionCalendarEvent[]> {
    let params = new HttpParams()
      .set('start', query.start)
      .set('end', query.end);

    params = this.setOptional(
      params,
      'search',
      query.search,
    );

    params = this.setOptional(
      params,
      'handler',
      query.handler,
    );

    params = this.setOptional(
      params,
      'department',
      query.department,
    );

    params = this.setOptional(
      params,
      'poNumber',
      query.poNumber,
    );

    if (query.eventTypes?.length) {
      params = params.set(
        'eventTypes',
        query.eventTypes.join(','),
      );
    }

    if (query.statuses?.length) {
      params = params.set(
        'statuses',
        query.statuses.join(','),
      );
    }

    return this.http.get<ProductionCalendarEvent[]>(
      '/api/production/calendar-events',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getCalendarDashboard(): Observable<ProductionCalendarDashboard> {
    return this.http.get<ProductionCalendarDashboard>(
      '/api/production/calendar-dashboard',
      {
        withCredentials: true,
      },
    );
  }

  private setOptional(
    params: HttpParams,
    key: string,
    value: string | undefined,
  ): HttpParams {
    const normalized = value?.trim();

    return normalized
      ? params.set(key, normalized)
      : params;
  }
}