import {
  HttpClient,
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
  InvoiceBatchImportPreview,
  InvoiceBatchImportResult,
  InvoiceImportFilePreview,
  InvoiceImportPreview,
  InvoiceImportSaveResponse,
  InvoicePreviewDiscardResponse,
} from '../models/invoice-import-api.model';
import {
  AuthApiService,
} from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class InvoiceImportApiService {
  private readonly http =
    inject(HttpClient);

  private readonly authApi =
    inject(AuthApiService);

  previewSingle(
    invoiceFile: File,
  ): Observable<InvoiceImportFilePreview> {
    const formData =
      new FormData();

    formData.append(
      'file',
      invoiceFile,
      invoiceFile.name,
    );

    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<InvoiceImportFilePreview>(
            '/api/invoice-import/preview',
            formData,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  previewBatch(
    invoiceFiles: File[],
  ): Observable<InvoiceBatchImportPreview> {
    const formData =
      new FormData();

    for (const invoiceFile of invoiceFiles) {
      formData.append(
        'invoiceFiles',
        invoiceFile,
        invoiceFile.name,
      );
    }

    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<InvoiceBatchImportPreview>(
            '/api/invoice-import/preview-batch',
            formData,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  discardPreviews(
    uploadedFilePaths: string[],
  ): Observable<InvoicePreviewDiscardResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<InvoicePreviewDiscardResponse>(
            '/api/invoice-import/discard-previews',
            {
              uploadedFilePaths,
            },
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  saveSingle(
    invoice:
      InvoiceImportPreview,
  ): Observable<InvoiceImportSaveResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<InvoiceImportSaveResponse>(
            '/api/invoice-import/save',
            invoice,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  saveBatch(
    preview:
      InvoiceBatchImportPreview,
  ): Observable<InvoiceBatchImportResult> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<InvoiceBatchImportResult>(
            '/api/invoice-import/save-batch',
            preview,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }
}
