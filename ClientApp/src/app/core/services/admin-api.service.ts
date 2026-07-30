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
  AdminRole,
  AdminUserMutationResponse,
  AdminUsersResponse,
} from '../models/admin-api.model';

import {
  AuthApiService,
} from './auth-api.service';

@Injectable({
  providedIn: 'root',
})
export class AdminApiService {
  private readonly http =
    inject(HttpClient);

  private readonly authApi =
    inject(AuthApiService);

  getUsers():
    Observable<AdminUsersResponse> {
    return this.http.get<AdminUsersResponse>(
      '/api/admin/users',
      {
        withCredentials: true,
      },
    );
  }

  updateRole(
    userId: number,
    role: AdminRole,
  ): Observable<AdminUserMutationResponse> {
    return this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.http.put<AdminUserMutationResponse>(
            `/api/admin/users/${userId}/role`,
            {
              role,
            },
            {
              withCredentials: true,
            },
          ),
        ),
      );
  }
}