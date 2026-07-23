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
  PoBatchImportPreview,
  PoBatchImportResult,
  PoImportFilePreview,
  PoImportPreview,
  PoImportSaveResponse,
} from '../models/po-import-api.model';
import {
  AuthApiService,
} from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class PoImportApiService {
  private readonly http =
    inject(HttpClient);

  private readonly authApi =
    inject(AuthApiService);

  previewSingle(
    pdfFile: File,
  ): Observable<PoImportFilePreview> {
    const formData = new FormData();

    formData.append(
      'pdfFile',
      pdfFile,
      pdfFile.name,
    );

    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<PoImportFilePreview>(
            '/api/po-import/preview',
            formData,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  previewBatch(
    pdfFiles: File[],
  ): Observable<PoBatchImportPreview> {
    const formData = new FormData();

    for (const pdfFile of pdfFiles) {
      formData.append(
        'pdfFiles',
        pdfFile,
        pdfFile.name,
      );
    }

    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<PoBatchImportPreview>(
            '/api/po-import/preview-batch',
            formData,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  saveSingle(
    preview: PoImportPreview,
  ): Observable<PoImportSaveResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<PoImportSaveResponse>(
            '/api/po-import/save',
            preview,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  saveBatch(
    preview: PoBatchImportPreview,
  ): Observable<PoBatchImportResult> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<PoBatchImportResult>(
            '/api/po-import/save-batch',
            preview,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }
}
