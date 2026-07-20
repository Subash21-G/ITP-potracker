import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import {
  MaterialDeleteResponse,
  MaterialDetails,
  MaterialFileUpload,
  MaterialPurchaseOrderOption,
  MaterialSaveResponse,
  MaterialUpsertRequest,
} from '../models/material-crud.model';
import { AuthApiService } from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class MaterialCrudApiService {
  private readonly http = inject(HttpClient);
  private readonly authApi = inject(AuthApiService);

  getDetails(id: number): Observable<MaterialDetails> {
    return this.http.get<MaterialDetails>(
      `/api/materials/${id}`,
      {
        withCredentials: true,
      },
    );
  }

  getPurchaseOrderOptions(): Observable<
    MaterialPurchaseOrderOption[]
  > {
    return this.http.get<MaterialPurchaseOrderOption[]>(
      '/api/materials/options/purchase-orders',
      {
        withCredentials: true,
      },
    );
  }

  create(
    request: MaterialUpsertRequest,
  ): Observable<MaterialSaveResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<MaterialSaveResponse>(
          '/api/materials',
          request,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }

  update(
    id: number,
    request: MaterialUpsertRequest,
  ): Observable<MaterialSaveResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.put<MaterialSaveResponse>(
          `/api/materials/${id}`,
          request,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }

  uploadFiles(
    id: number,
    files: MaterialFileUpload,
  ): Observable<MaterialSaveResponse> {
    const formData = new FormData();

    if (files.pdfFile) {
      formData.append(
        'pdfFile',
        files.pdfFile,
        files.pdfFile.name,
      );
    }

    if (files.drawingFile) {
      formData.append(
        'drawingFile',
        files.drawingFile,
        files.drawingFile.name,
      );
    }

    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<MaterialSaveResponse>(
          `/api/materials/${id}/files`,
          formData,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }

  delete(
    id: number,
  ): Observable<MaterialDeleteResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.delete<MaterialDeleteResponse>(
          `/api/materials/${id}`,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }
}
