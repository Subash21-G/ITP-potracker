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
  MaterialImportCancelResponse,
  MaterialImportCommitResponse,
  MaterialImportPreview,
} from '../models/material-import-api.model';
import {
  AuthApiService,
} from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class MaterialImportApiService {
  private readonly http =
    inject(HttpClient);

  private readonly authApi =
    inject(AuthApiService);

  preview(
    excelFile: File,
  ): Observable<MaterialImportPreview> {
    const formData =
      new FormData();

    formData.append(
      'excelFile',
      excelFile,
      excelFile.name,
    );

    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<MaterialImportPreview>(
            '/api/material-import/preview',
            formData,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  commit(
    batchId: string,
  ): Observable<MaterialImportCommitResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<MaterialImportCommitResponse>(
            '/api/material-import/commit',
            {
              batchId,
            },
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }

  cancel(
    batchId: string,
  ): Observable<MaterialImportCancelResponse> {
    const encodedBatchId =
      encodeURIComponent(batchId);

    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.delete<MaterialImportCancelResponse>(
            `/api/material-import/${encodedBatchId}`,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }
}
