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
  AlertsResponse,
} from '../models/alert-api.model';

@Injectable({
  providedIn: 'root',
})
export class AlertApiService {
  private readonly http =
    inject(HttpClient);

  getAlerts():
    Observable<AlertsResponse> {
    return this.http.get<AlertsResponse>(
      '/api/alerts',
      {
        withCredentials: true,
      },
    );
  }
}
