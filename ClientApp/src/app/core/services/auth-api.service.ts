import {
  HttpClient,
} from '@angular/common/http';

import {
  Injectable,
  computed,
  inject,
  signal,
} from '@angular/core';

import {
  Observable,
  tap,
} from 'rxjs';

import {
  AuthenticationResponse,
  CurrentUser,
  LoginRequest,
  RegisterRequest,
  RegistrationResponse,
} from '../models/auth.model';

@Injectable({
  providedIn: 'root',
})
export class AuthApiService {
  private readonly http =
    inject(HttpClient);

  private readonly currentUserState =
    signal<CurrentUser | null>(
      null,
    );

  private readonly authenticationCheckedState =
    signal(false);

  readonly currentUser =
    this.currentUserState.asReadonly();

  readonly authenticationChecked =
    this.authenticationCheckedState
      .asReadonly();

  readonly isAuthenticated =
    computed(() =>
      this.currentUserState()
        ?.isAuthenticated === true,
    );

  readonly isAdmin =
    computed(() =>
      this.currentUserState()
        ?.isAdmin === true,
    );

  readonly canOperate =
    computed(() =>
      this.currentUserState()
        ?.canOperate === true,
    );

  getXsrfToken():
    Observable<{
      success: boolean;
    }> {
    return this.http.get<{
      success: boolean;
    }>(
      '/api/auth/xsrf-token',
      {
        withCredentials: true,
      },
    );
  }

  getCurrentUser():
    Observable<CurrentUser> {
    return this.http
      .get<CurrentUser>(
        '/api/auth/me',
        {
          withCredentials: true,
        },
      )
      .pipe(
        tap(user => {
          this.currentUserState.set(
            user,
          );

          this.authenticationCheckedState
            .set(true);
        }),
      );
  }

  login(
    request: LoginRequest,
  ): Observable<AuthenticationResponse> {
    return this.http
      .post<AuthenticationResponse>(
        '/api/auth/login',
        request,
        {
          withCredentials: true,
        },
      )
      .pipe(
        tap(response => {
          if (
            response.success &&
            response.user
          ) {
            this.currentUserState.set(
              response.user,
            );
          }

          this.authenticationCheckedState
            .set(true);
        }),
      );
  }

  register(
    request: RegisterRequest,
  ): Observable<RegistrationResponse> {
    return this.http
      .post<RegistrationResponse>(
        '/api/auth/register',
        request,
        {
          withCredentials: true,
        },
      );
  }

  logout():
    Observable<AuthenticationResponse> {
    return this.http
      .post<AuthenticationResponse>(
        '/api/auth/logout',
        {},
        {
          withCredentials: true,
        },
      )
      .pipe(
        tap(() => {
          this.currentUserState.set(
            null,
          );

          this.authenticationCheckedState
            .set(true);
        }),
      );
  }

  clearLocalAuthentication(): void {
    this.currentUserState.set(
      null,
    );

    this.authenticationCheckedState
      .set(true);
  }
}