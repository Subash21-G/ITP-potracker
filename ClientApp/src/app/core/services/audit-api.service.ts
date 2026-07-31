import {
  HttpClient,
  HttpParams,
} from '@angular/common/http';

import {
  inject,
  Injectable,
} from '@angular/core';

import {
  Observable,
} from 'rxjs';

import {
  AuditPagedResponse,
  AuditQuery,
} from '../models/audit-api.model';

@Injectable({
  providedIn: 'root',
})
export class AuditApiService {
  private readonly http =
    inject(HttpClient);

  getAudit(
    query: AuditQuery,
  ): Observable<AuditPagedResponse> {
    let params =
      new HttpParams()
        .set(
          'page',
          query.page.toString(),
        )
        .set(
          'pageSize',
          query.pageSize.toString(),
        );

    params =
      this.addOptional(
        params,
        'search',
        query.search,
      );

    params =
      this.addOptional(
        params,
        'owner',
        query.owner,
      );

    params =
      this.addOptional(
        params,
        'processStatus',
        query.processStatus,
      );

    params =
      this.addOptional(
        params,
        'dispatchStatus',
        query.dispatchStatus,
      );

    params =
      this.addOptional(
        params,
        'dateFrom',
        query.dateFrom,
      );

    params =
      this.addOptional(
        params,
        'dateTo',
        query.dateTo,
      );

    return this.http.get<
      AuditPagedResponse
    >(
      '/api/audit',
      {
        params,
      },
    );
  }

  private addOptional(
    params: HttpParams,
    name: string,
    value:
      | string
      | undefined,
  ): HttpParams {
    const cleaned =
      value?.trim();

    return cleaned
      ? params.set(
          name,
          cleaned,
        )
      : params;
  }
}
