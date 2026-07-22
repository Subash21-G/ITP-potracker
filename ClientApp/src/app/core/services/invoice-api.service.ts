import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import {
  InvoiceDeleteResponse,
  InvoiceDetails,
  InvoiceImportBatch,
  InvoiceImportBatchDeleteResponse,
  InvoiceListQuery,
  InvoiceListResponse,
  PendingInvoiceItem,
  PendingInvoiceManualMatchRequest,
  PendingInvoiceManualMatchResponse,
  PendingInvoiceMatchCandidatesResponse,
  PendingInvoiceRetryResponse,
} from '../models/invoice-api.model';
import { AuthApiService } from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class InvoiceApiService {
  private readonly http = inject(HttpClient);
  private readonly authApi = inject(AuthApiService);

  getInvoices(
    query: InvoiceListQuery = {},
  ): Observable<InvoiceListResponse> {
    let params = new HttpParams();

    if (query.search?.trim()) {
      params = params.set('search', query.search.trim());
    }

    if (query.dateFrom) {
      params = params.set('dateFrom', query.dateFrom);
    }

    if (query.dateTo) {
      params = params.set('dateTo', query.dateTo);
    }

    params = params
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 25));

    return this.http.get<InvoiceListResponse>(
      '/api/invoices',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getDetails(id: number): Observable<InvoiceDetails> {
    return this.http.get<InvoiceDetails>(
      `/api/invoices/${id}`,
      {
        withCredentials: true,
      },
    );
  }

  getImportHistory(
    search = '',
    take = 200,
  ): Observable<InvoiceImportBatch[]> {
    let params = new HttpParams().set(
      'take',
      String(take),
    );

    if (search.trim()) {
      params = params.set('search', search.trim());
    }

    return this.http.get<InvoiceImportBatch[]>(
      '/api/invoices/import-history',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getPendingItems(
    search = '',
    take = 500,
  ): Observable<PendingInvoiceItem[]> {
    let params = new HttpParams().set(
      'take',
      String(take),
    );

    if (search.trim()) {
      params = params.set('search', search.trim());
    }

    return this.http.get<PendingInvoiceItem[]>(
      '/api/invoices/pending',
      {
        params,
        withCredentials: true,
      },
    );
  }


  getPendingMatchCandidates(
    pendingItemId: number,
    search = '',
    take = 50,
  ): Observable<PendingInvoiceMatchCandidatesResponse> {
    let params = new HttpParams().set(
      'take',
      String(take),
    );

    if (search.trim()) {
      params = params.set('search', search.trim());
    }

    return this.http.get<PendingInvoiceMatchCandidatesResponse>(
      `/api/invoices/pending/${pendingItemId}/candidates`,
      {
        params,
        withCredentials: true,
      },
    );
  }

  matchPendingItemManually(
    pendingItemId: number,
    request: PendingInvoiceManualMatchRequest,
  ): Observable<PendingInvoiceManualMatchResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<PendingInvoiceManualMatchResponse>(
          `/api/invoices/pending/${pendingItemId}/match`,
          request,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }

  retryPendingItems(): Observable<PendingInvoiceRetryResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<PendingInvoiceRetryResponse>(
          '/api/invoices/pending/retry',
          {},
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }

  deleteInvoice(
    id: number,
  ): Observable<InvoiceDeleteResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.delete<InvoiceDeleteResponse>(
          `/api/invoices/${id}`,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }

  deleteImportBatch(
    id: number,
  ): Observable<InvoiceImportBatchDeleteResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.delete<InvoiceImportBatchDeleteResponse>(
          `/api/invoices/import-history/${id}`,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }
}
