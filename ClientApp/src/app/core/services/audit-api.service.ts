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

    const optionalValues:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['search', query.search],
        ['owner', query.owner],
        ['processStatus', query.processStatus],
        ['dispatchStatus', query.dispatchStatus],
        ['dateFrom', query.dateFrom],
        ['dateTo', query.dateTo],
        ['materialNo', query.materialNo],
        ['poNumber', query.poNumber],
        ['action', query.action],
        [
          'columnProcessStatus',
          query.columnProcessStatus,
        ],
        [
          'columnDispatchStatus',
          query.columnDispatchStatus,
        ],
        ['columnOwner', query.columnOwner],
        [
          'createdDateFrom',
          query.createdDateFrom,
        ],
        [
          'createdDateTo',
          query.createdDateTo,
        ],
      ];

    for (
      const [
        name,
        value,
      ] of optionalValues
    ) {
      params =
        this.addOptional(
          params,
          name,
          value,
        );
    }

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