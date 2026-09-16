import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  MaterialPlanningListResponse,
  MaterialPlanningQuery,
} from '../models/material-planning-api.model';

@Injectable({ providedIn: 'root' })
export class MaterialPlanningApiService {
  private readonly http = inject(HttpClient);

  getItems(query: MaterialPlanningQuery): Observable<MaterialPlanningListResponse> {
    let params = new HttpParams()
      .set('workspace', query.workspace)
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    if (query.scope?.trim()) {
      params = params.set('scope', query.scope.trim());
    }

    if (query.search?.trim()) {
      params = params.set('search', query.search.trim());
    }

    return this.http.get<MaterialPlanningListResponse>('/api/material-planning', {
      params,
      withCredentials: true,
    });
  }
}
