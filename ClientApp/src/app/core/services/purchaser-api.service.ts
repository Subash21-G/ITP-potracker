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
  PurchaserDeleteResponse,
  PurchaserDetails,
  PurchaserListResponse,
  PurchaserSaveRequest,
  PurchaserSaveResponse,
} from '../models/purchaser-api.model';
import {
  AuthApiService,
} from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class PurchaserApiService {
  private readonly http = inject(HttpClient);
  private readonly authApi = inject(AuthApiService);

  getPurchasers(
    search = '',
  ): Observable<PurchaserListResponse> {
    let params = new HttpParams();

    const cleanedSearch = search.trim();

    if (cleanedSearch) {
      params = params.set(
        'search',
        cleanedSearch,
      );
    }

    return this.http.get<PurchaserListResponse>(
      '/api/purchasers',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getDetails(
    id: number,
  ): Observable<PurchaserDetails> {
    return this.http.get<PurchaserDetails>(
      `/api/purchasers/${id}`,
      {
        withCredentials: true,
      },
    );
  }

  create(
    request: PurchaserSaveRequest,
  ): Observable<PurchaserSaveResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<PurchaserSaveResponse>(
            '/api/purchasers',
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
    request: PurchaserSaveRequest,
  ): Observable<PurchaserSaveResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.put<PurchaserSaveResponse>(
            `/api/purchasers/${id}`,
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
  ): Observable<PurchaserDeleteResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.delete<PurchaserDeleteResponse>(
            `/api/purchasers/${id}`,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }
}
