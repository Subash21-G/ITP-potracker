import {
  HttpClient,
} from '@angular/common/http';

import {
  Injectable,
  inject,
} from '@angular/core';

import {
  Observable,
} from 'rxjs';

import {
  DashboardResponse,
} from '../models/dashboard.model';

import {
  ApiResponseCacheService,
} from './api-response-cache.service';

@Injectable({
  providedIn: 'root',
})
export class DashboardApiService {
  private readonly http =
    inject(HttpClient);

  private readonly cache =
    inject(
      ApiResponseCacheService,
    );

  getDashboard(
    forceRefresh = false,
  ): Observable<DashboardResponse> {
    return this.cache.get(
      'dashboard:overview',
      () =>
        this.http.get<DashboardResponse>(
          '/api/dashboard',
          {
            withCredentials: true,
          },
        ),
      10_000,
      forceRefresh,
    );
  }
}