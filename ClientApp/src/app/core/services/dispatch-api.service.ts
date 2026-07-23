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
  CreateDispatchRequest,
  DispatchBulkDeleteRequest,
  DispatchDetailsResponse,
  DispatchListQuery,
  DispatchListResponse,
  DispatchMutationResponse,
} from '../models/dispatch-api.model';
import { AuthApiService } from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class DispatchApiService {
  private readonly http = inject(HttpClient);
  private readonly authApi = inject(AuthApiService);

  getDispatches(
    query: DispatchListQuery = {},
  ): Observable<DispatchListResponse> {
    let params = new HttpParams().set(
      'take',
      String(query.take ?? 500),
    );

    if (query.search?.trim()) {
      params = params.set(
        'search',
        query.search.trim(),
      );
    }

    if (
      query.status?.trim() &&
      query.status !== 'All'
    ) {
      params = params.set(
        'status',
        query.status.trim(),
      );
    }

    return this.http.get<DispatchListResponse>(
      '/api/dispatches',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getDetails(
    materialId: number,
  ): Observable<DispatchDetailsResponse> {
    return this.http.get<DispatchDetailsResponse>(
      `/api/dispatches/${materialId}`,
      {
        withCredentials: true,
      },
    );
  }

  createDispatch(
    request: CreateDispatchRequest,
  ): Observable<DispatchMutationResponse> {
    const formData = new FormData();

    formData.append(
      'materialId',
      String(request.materialId),
    );

    formData.append(
      'dispatchNo',
      request.dispatchNo.trim(),
    );

    formData.append(
      'dispatchDate',
      request.dispatchDate,
    );

    formData.append(
      'dispatchQuantity',
      String(request.dispatchQuantity),
    );

    if (request.remarks?.trim()) {
      formData.append(
        'remarks',
        request.remarks.trim(),
      );
    }

    if (request.dispatchFile) {
      formData.append(
        'dispatchFile',
        request.dispatchFile,
        request.dispatchFile.name,
      );
    }

    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<DispatchMutationResponse>(
          '/api/dispatches',
          formData,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }

  deleteDispatch(
    dispatchId: number,
  ): Observable<DispatchMutationResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.delete<DispatchMutationResponse>(
          `/api/dispatches/${dispatchId}`,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }

  bulkDelete(
    request: DispatchBulkDeleteRequest,
  ): Observable<DispatchMutationResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<DispatchMutationResponse>(
          '/api/dispatches/bulk-delete',
          request,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }
}
