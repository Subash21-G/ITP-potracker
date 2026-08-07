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
  StockListQuery,
  StockListResponse,
} from '../models/stock-api.model';

@Injectable({
  providedIn: 'root',
})
export class StockApiService {
  private readonly http =
    inject(HttpClient);

  getStocks(
    query: StockListQuery,
  ): Observable<StockListResponse> {
    let params =
      new HttpParams()
        .set(
          'page',
          query.page.toString(),
        )
        .set(
          'pageSize',
          query.pageSize.toString(),
        )
        .set(
          'refresh',
          query.refresh
            ? 'true'
            : 'false',
        );

    const values:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['search', query.search],
        ['material', query.material],
        ['description', query.description],
        ['supplier', query.supplier],
        ['location', query.location],
        [
          'minCurrentStock',
          query.minCurrentStock,
        ],
        [
          'maxCurrentStock',
          query.maxCurrentStock,
        ],
        [
          'minMinimumStock',
          query.minMinimumStock,
        ],
        [
          'maxMinimumStock',
          query.maxMinimumStock,
        ],
        ['minUnitPrice', query.minUnitPrice],
        ['maxUnitPrice', query.maxUnitPrice],
        ['minTotalValue', query.minTotalValue],
        ['maxTotalValue', query.maxTotalValue],
        ['status', query.status],
        ['updatedFrom', query.updatedFrom],
        ['updatedTo', query.updatedTo],
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

    return this.http.get<StockListResponse>(
      '/api/stocks',
      {
        params,
        withCredentials: true,
      },
    );
  }
}
