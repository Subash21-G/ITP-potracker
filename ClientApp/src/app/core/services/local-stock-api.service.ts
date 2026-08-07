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
  switchMap,
} from 'rxjs';

import {
  LocalStockDeleteResponse,
  LocalStockListQuery,
  LocalStockListResponse,
  LocalStockSaveResponse,
  LocalStockUpsertRequest,
} from '../models/local-stock-api.model';
import {
  AuthApiService,
} from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class LocalStockApiService {
  private readonly http =
    inject(HttpClient);

  private readonly authApi =
    inject(AuthApiService);

  getStocks(
    query: LocalStockListQuery,
  ): Observable<LocalStockListResponse> {
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
        ['material', query.material],
        ['rmGrade', query.rmGrade],
        ['location', query.location],
        [
          'minAvailableQty',
          query.minAvailableQty,
        ],
        [
          'maxAvailableQty',
          query.maxAvailableQty,
        ],
        [
          'minMinimumQty',
          query.minMinimumQty,
        ],
        [
          'maxMinimumQty',
          query.maxMinimumQty,
        ],
        ['status', query.status],
        ['updatedFrom', query.updatedFrom],
        ['updatedTo', query.updatedTo],
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

    return this.http.get<LocalStockListResponse>(
      '/api/local-stocks',
      {
        params,
        withCredentials: true,
      },
    );
  }

  create(
    request: LocalStockUpsertRequest,
  ): Observable<LocalStockSaveResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<LocalStockSaveResponse>(
            '/api/local-stocks',
            request,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  update(
    id: number,
    request: LocalStockUpsertRequest,
  ): Observable<LocalStockSaveResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.put<LocalStockSaveResponse>(
            `/api/local-stocks/${id}`,
            request,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  delete(
    id: number,
  ): Observable<LocalStockDeleteResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.delete<LocalStockDeleteResponse>(
            `/api/local-stocks/${id}`,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }
}