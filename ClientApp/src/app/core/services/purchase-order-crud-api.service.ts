import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import {
  PurchaseOrderCrudDetails,
  PurchaseOrderDeleteResponse,
  PurchaseOrderPurchaserOption,
  PurchaseOrderSaveResponse,
  PurchaseOrderUpsertRequest,
} from '../models/purchase-order-crud.model';
import { AuthApiService } from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class PurchaseOrderCrudApiService {
  private readonly http = inject(HttpClient);
  private readonly authApi = inject(AuthApiService);

  getDetails(
    id: number,
  ): Observable<PurchaseOrderCrudDetails> {
    return this.http.get<PurchaseOrderCrudDetails>(
      `/api/purchase-order-crud/${id}`,
      {
        withCredentials: true,
      },
    );
  }

  getPurchaserOptions(): Observable<
    PurchaseOrderPurchaserOption[]
  > {
    return this.http.get<
      PurchaseOrderPurchaserOption[]
    >(
      '/api/purchase-order-crud/options/purchasers',
      {
        withCredentials: true,
      },
    );
  }

  create(
    request: PurchaseOrderUpsertRequest,
  ): Observable<PurchaseOrderSaveResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<PurchaseOrderSaveResponse>(
          '/api/purchase-order-crud',
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
    request: PurchaseOrderUpsertRequest,
  ): Observable<PurchaseOrderSaveResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.put<PurchaseOrderSaveResponse>(
          `/api/purchase-order-crud/${id}`,
          request,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }

  uploadPdf(
    id: number,
    pdfFile: File,
  ): Observable<PurchaseOrderSaveResponse> {
    const formData = new FormData();

    formData.append(
      'pdfFile',
      pdfFile,
      pdfFile.name,
    );

    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.post<PurchaseOrderSaveResponse>(
          `/api/purchase-order-crud/${id}/pdf`,
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
  ): Observable<PurchaseOrderDeleteResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.delete<PurchaseOrderDeleteResponse>(
          `/api/purchase-order-crud/${id}`,
          {
            withCredentials: true,
          },
        ),
      ),
    );
  }
}
