import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import {
  MasterSheetDeleteResponse,
  MasterSheetProcessOption,
  MasterSheetProcessOptionRequest,
  MasterSheetRecord,
  MasterSheetSaveRequest,
} from '../models/mastersheet-api.model';
import { AuthApiService } from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class MasterSheetApiService {
  private readonly http = inject(HttpClient);
  private readonly authApi = inject(AuthApiService);

  getProcessOptions(): Observable<readonly MasterSheetProcessOption[]> {
    return this.http.get<readonly MasterSheetProcessOption[]>('/api/mastersheet/process-options', {
      withCredentials: true,
    });
  }

  addProcessOption(request: MasterSheetProcessOptionRequest): Observable<MasterSheetProcessOption> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.post<MasterSheetProcessOption>('/api/mastersheet/process-options', request, {
            withCredentials: true,
          }),
        ),
      );
  }

  deleteProcessOption(id: number): Observable<void> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.delete<void>(`/api/mastersheet/process-options/${id}`, {
          withCredentials: true,
        }),
      ),
    );
  }

  getRecord(materialNo: string): Observable<MasterSheetRecord> {
    return this.http.get<MasterSheetRecord>('/api/mastersheet/record', {
      params: new HttpParams().set('materialNo', materialNo),
      withCredentials: true,
    });
  }

  getRecords(): Observable<readonly MasterSheetRecord[]> {
    return this.http.get<readonly MasterSheetRecord[]>('/api/mastersheet/records', {
      withCredentials: true,
    });
  }

  saveRecord(request: MasterSheetSaveRequest): Observable<MasterSheetRecord> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.put<MasterSheetRecord>('/api/mastersheet/record', request, {
          withCredentials: true,
        }),
      ),
    );
  }

  deleteRecord(materialNo: string): Observable<MasterSheetDeleteResponse> {
    return this.authApi.getXsrfToken().pipe(
      switchMap(() =>
        this.http.delete<MasterSheetDeleteResponse>('/api/mastersheet/record', {
          params: new HttpParams().set('materialNo', materialNo),
          withCredentials: true,
        }),
      ),
    );
  }
}
