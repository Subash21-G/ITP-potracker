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
  MaterialDeleteResponse,
  MaterialDetails,
  MaterialFileUpload,
  MaterialPurchaseOrderOption,
  RelatedMaterialPurchaseOrder,
  MaterialSaveResponse,
  MaterialUpsertRequest,
} from '../models/material-crud.model';

import {
  ApiResponseCacheService,
} from './api-response-cache.service';

import {
  AuthApiService,
} from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class MaterialCrudApiService {
  private readonly http =
    inject(HttpClient);

  private readonly authApi =
    inject(AuthApiService);

  private readonly cache =
    inject(
      ApiResponseCacheService,
    );

  getDetails(
    id: number,
    forceRefresh = false,
  ): Observable<MaterialDetails> {
    return this.cache.get(
      `materials:detail:${id}`,
      () =>
        this.http.get<MaterialDetails>(
          `/api/materials/${id}`,
          {
            withCredentials: true,
          },
        ),
      20_000,
      forceRefresh,
    );
  }

  getPurchaseOrderOptions(): Observable<
    MaterialPurchaseOrderOption[]
  > {
    return this.cache.get(
      'materials:purchase-order-options',
      () =>
        this.http.get<
          MaterialPurchaseOrderOption[]
        >(
          '/api/materials/options/purchase-orders',
          {
            withCredentials: true,
          },
        ),
      20_000,
    );
  }

  getRelatedPurchaseOrders(
    materialId: number,
  ): Observable<RelatedMaterialPurchaseOrder[]> {
    return this.http.get<RelatedMaterialPurchaseOrder[]>(
      `/api/materials/${materialId}/related-purchase-orders`,
      {
        withCredentials: true,
      },
    );
  }

  create(
    request: MaterialUpsertRequest,
  ): Observable<MaterialSaveResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<
            MaterialSaveResponse
          >(
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
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.put<
            MaterialSaveResponse
          >(
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
    const formData =
      new FormData();

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

    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<
            MaterialSaveResponse
          >(
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
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.delete<
            MaterialDeleteResponse
          >(
            `/api/materials/${id}`,
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }
}
