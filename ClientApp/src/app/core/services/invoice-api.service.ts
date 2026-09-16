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
  switchMap,
} from 'rxjs';

import {
  InvoiceBulkDeleteRequest,
  InvoiceBulkDeleteResponse,
  InvoiceDeleteResponse,
  InvoiceDetails,
  InvoiceImportBatch,
  InvoiceImportBatchDeleteResponse,
  InvoiceExcelFilterColumn,
  InvoiceExcelFilterValue,
  InvoiceListQuery,
  InvoiceListResponse,
  PendingInvoiceExcelFilterColumn,
  PendingInvoiceExcelFilterValue,
  PendingInvoiceItem,
  PendingInvoiceManualMatchRequest,
  PendingInvoiceSortColumn,
  PendingInvoiceSortDirection,
  PendingInvoiceManualMatchResponse,
  PendingInvoiceMatchCandidatesResponse,
  PendingInvoiceRetryResponse,
} from '../models/invoice-api.model';
import { AuthApiService } from './auth-api.service';
import {
  InvoiceImportHistoryBulkDeleteRequest,
  InvoiceImportHistoryBulkDeleteResponse,
  InvoiceImportReportQuery,
  InvoiceImportReportResponse,
} from '../models/invoice-import-history-api.model';
@Injectable({
  providedIn: 'root',
})
export class InvoiceApiService {
  private readonly http = inject(HttpClient);
  private readonly authApi = inject(AuthApiService);

  getInvoices(
    query: InvoiceListQuery = {},
  ): Observable<InvoiceListResponse> {
    let params =
      new HttpParams()
        .set(
          'page',
          String(query.page ?? 1),
        )
        .set(
          'pageSize',
          String(query.pageSize ?? 25),
        );

    const optionalValues:
      readonly [
        string,
        string | null | undefined,
      ][] = [
        ['sortColumn', query.sortColumn],
        ['sortDirection', query.sortDirection],
        ['excelFilters', query.excelFilters],
        ['search', query.search],
        ['dateFrom', query.dateFrom],
        ['dateTo', query.dateTo],
        ['invoiceNumber', query.invoiceNumber],
        [
          'columnInvoiceDateFrom',
          query.columnInvoiceDateFrom,
        ],
        [
          'columnInvoiceDateTo',
          query.columnInvoiceDateTo,
        ],
        ['customerName', query.customerName],
        ['minItemCount', query.minItemCount],
        ['maxItemCount', query.maxItemCount],
        [
          'minTotalQuantity',
          query.minTotalQuantity,
        ],
        [
          'maxTotalQuantity',
          query.maxTotalQuantity,
        ],
        [
          'minTotalGstAmount',
          query.minTotalGstAmount,
        ],
        [
          'maxTotalGstAmount',
          query.maxTotalGstAmount,
        ],
        [
          'minTotalAmount',
          query.minTotalAmount,
        ],
        [
          'maxTotalAmount',
          query.maxTotalAmount,
        ],
        [
          'uploadedFileName',
          query.uploadedFileName,
        ],
        [
          'importedDateFrom',
          query.importedDateFrom,
        ],
        [
          'importedDateTo',
          query.importedDateTo,
        ],
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

    return this.http.get<InvoiceListResponse>(
      '/api/invoices',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getExcelFilterValues(
    query: InvoiceListQuery,
    column: InvoiceExcelFilterColumn,
  ): Observable<
    readonly InvoiceExcelFilterValue[]
  > {
    let params =
      new HttpParams()
        .set(
          'column',
          column,
        );

    const optionalValues:
      readonly [
        string,
        string | null | undefined,
      ][] = [
        ['excelFilters', query.excelFilters],
        ['search', query.search],
        ['dateFrom', query.dateFrom],
        ['dateTo', query.dateTo],
        ['invoiceNumber', query.invoiceNumber],
        [
          'columnInvoiceDateFrom',
          query.columnInvoiceDateFrom,
        ],
        [
          'columnInvoiceDateTo',
          query.columnInvoiceDateTo,
        ],
        ['customerName', query.customerName],
        ['minItemCount', query.minItemCount],
        ['maxItemCount', query.maxItemCount],
        [
          'minTotalQuantity',
          query.minTotalQuantity,
        ],
        [
          'maxTotalQuantity',
          query.maxTotalQuantity,
        ],
        [
          'minTotalGstAmount',
          query.minTotalGstAmount,
        ],
        [
          'maxTotalGstAmount',
          query.maxTotalGstAmount,
        ],
        [
          'minTotalAmount',
          query.minTotalAmount,
        ],
        [
          'maxTotalAmount',
          query.maxTotalAmount,
        ],
        [
          'uploadedFileName',
          query.uploadedFileName,
        ],
        [
          'importedDateFrom',
          query.importedDateFrom,
        ],
        [
          'importedDateTo',
          query.importedDateTo,
        ],
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

    return this.http.get<
      readonly InvoiceExcelFilterValue[]
    >(
      '/api/invoices/excel-filter-values',
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
    sortColumn?: PendingInvoiceSortColumn,
    sortDirection?: PendingInvoiceSortDirection,
    excelFilters?: string,
  ): Observable<PendingInvoiceItem[]> {
    let params =
      new HttpParams()
        .set(
          'take',
          String(take),
        );

    const optionalValues:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['search', search],
        ['sortColumn', sortColumn],
        ['sortDirection', sortDirection],
        ['excelFilters', excelFilters],
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

    return this.http.get<PendingInvoiceItem[]>(
      '/api/invoices/pending',
      {
        params,
        withCredentials: true,
      },
    );
  }

  getPendingExcelFilterValues(
    search: string,
    column: PendingInvoiceExcelFilterColumn,
    excelFilters?: string,
  ): Observable<
    readonly PendingInvoiceExcelFilterValue[]
  > {
    let params =
      new HttpParams()
        .set(
          'column',
          column,
        );

    const cleanedSearch =
      search.trim();

    if (cleanedSearch) {
      params =
        params.set(
          'search',
          cleanedSearch,
        );
    }

    const cleanedExcelFilters =
      excelFilters?.trim();

    if (cleanedExcelFilters) {
      params =
        params.set(
          'excelFilters',
          cleanedExcelFilters,
        );
    }

    return this.http.get<
      readonly PendingInvoiceExcelFilterValue[]
    >(
      '/api/invoices/pending/excel-filter-values',
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

  deletePendingItem(id: number): Observable<void> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() => this.http.delete<void>(`/api/invoices/pending/${id}`, { withCredentials: true })),
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

  downloadInvoicesExcel(
    query: InvoiceListQuery = {},
  ): Observable<Blob> {
    let params =
      new HttpParams();

    const optionalValues:
      readonly [
        string,
        string | null | undefined,
      ][] = [
        ['excelFilters', query.excelFilters],
        ['search', query.search],
        ['dateFrom', query.dateFrom],
        ['dateTo', query.dateTo],
        ['invoiceNumber', query.invoiceNumber],
        [
          'columnInvoiceDateFrom',
          query.columnInvoiceDateFrom,
        ],
        [
          'columnInvoiceDateTo',
          query.columnInvoiceDateTo,
        ],
        ['customerName', query.customerName],
        ['minItemCount', query.minItemCount],
        ['maxItemCount', query.maxItemCount],
        [
          'minTotalQuantity',
          query.minTotalQuantity,
        ],
        [
          'maxTotalQuantity',
          query.maxTotalQuantity,
        ],
        [
          'minTotalGstAmount',
          query.minTotalGstAmount,
        ],
        [
          'maxTotalGstAmount',
          query.maxTotalGstAmount,
        ],
        [
          'minTotalAmount',
          query.minTotalAmount,
        ],
        [
          'maxTotalAmount',
          query.maxTotalAmount,
        ],
        [
          'uploadedFileName',
          query.uploadedFileName,
        ],
        [
          'importedDateFrom',
          query.importedDateFrom,
        ],
        [
          'importedDateTo',
          query.importedDateTo,
        ],
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

    return this.http.get(
      '/api/invoices/export-excel',
      {
        params,
        responseType: 'blob',
        withCredentials: true,
      },
    );
  }
  bulkDeleteInvoices(
    request: InvoiceBulkDeleteRequest,
  ): Observable<InvoiceBulkDeleteResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<InvoiceBulkDeleteResponse>(
          '/api/invoices/bulk-delete',
          request,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }
  getImportHistoryReport(
    batchId: number,
    query: InvoiceImportReportQuery,
  ): Observable<InvoiceImportReportResponse> {
    let params =
      new HttpParams()
        .set(
          'page',
          String(query.page),
        )
        .set(
          'pageSize',
          String(query.pageSize),
        );

    const optionalValues:
      readonly [
        string,
        string | undefined,
      ][] = [
        ['search', query.search],
        ['status', query.status],
        ['poNumber', query.poNumber],
        ['lineItem', query.lineItem],
        ['materialNumber', query.materialNumber],
        ['invoiceNumber', query.invoiceNumber],
        [
          'minInvoiceQuantity',
          query.minInvoiceQuantity,
        ],
        [
          'maxInvoiceQuantity',
          query.maxInvoiceQuantity,
        ],
        [
          'columnStatus',
          query.columnStatus,
        ],
        ['message', query.message],
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

    return this.http.get<InvoiceImportReportResponse>(
      `/api/invoices/import-history/${batchId}/report`,
      {
        params,
        withCredentials: true,
      },
    );
  }

  bulkDeleteImportHistory(
    request: InvoiceImportHistoryBulkDeleteRequest,
  ): Observable<InvoiceImportHistoryBulkDeleteResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<InvoiceImportHistoryBulkDeleteResponse>(
          '/api/invoices/import-history/bulk-delete',
          request,
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
