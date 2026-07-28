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
  MaterialReportFilter,
  MaterialReportResponse,
  ReportOptions,
} from '../models/report-api.model';

@Injectable({
  providedIn: 'root',
})
export class ReportApiService {
  private readonly http =
    inject(HttpClient);

  getOptions():
    Observable<ReportOptions> {
    return this.http.get<ReportOptions>(
      '/api/reports/options',
      {
        withCredentials: true,
      },
    );
  }

  preview(
    filter: MaterialReportFilter,
  ): Observable<MaterialReportResponse> {
    return this.http.get<MaterialReportResponse>(
      '/api/reports/preview',
      {
        params:
          this.createParams(filter),

        withCredentials: true,
      },
    );
  }

  downloadExcel(
    filter: MaterialReportFilter,
  ): Observable<Blob> {
    return this.http.get(
      '/api/reports/materials-excel',
      {
        params:
          this.createParams(filter),

        responseType: 'blob',

        withCredentials: true,
      },
    );
  }

  downloadPdf(
    filter: MaterialReportFilter,
  ): Observable<Blob> {
    return this.http.get(
      '/api/reports/materials-pdf',
      {
        params:
          this.createParams(filter),

        responseType: 'blob',

        withCredentials: true,
      },
    );
  }

  private createParams(
    filter: MaterialReportFilter,
  ): HttpParams {
    let params =
      new HttpParams();

    if (filter.fromDate) {
      params =
        params.set(
          'fromDate',
          filter.fromDate,
        );
    }

    if (filter.toDate) {
      params =
        params.set(
          'toDate',
          filter.toDate,
        );
    }

    if (filter.processStatus) {
      params =
        params.set(
          'processStatus',
          filter.processStatus,
        );
    }

    if (filter.dispatchStatus) {
      params =
        params.set(
          'dispatchStatus',
          filter.dispatchStatus,
        );
    }

    if (
      filter.purchaserId !== null &&
      filter.purchaserId > 0
    ) {
      params =
        params.set(
          'purchaserId',
          filter.purchaserId
            .toString(),
        );
    }

    return params;
  }
}
