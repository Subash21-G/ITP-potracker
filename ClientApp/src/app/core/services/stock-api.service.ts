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

    const search =
      query.search.trim();

    if (search) {
      params =
        params.set(
          'search',
          search,
        );
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
