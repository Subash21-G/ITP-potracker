import {
  HttpErrorResponse,
  HttpInterceptorFn,
} from '@angular/common/http';

import {
  inject,
} from '@angular/core';

import {
  Router,
} from '@angular/router';

import {
  catchError,
  throwError,
} from 'rxjs';

import {
  AuthApiService,
} from '../services/auth-api.service';

import {
  ToastService,
} from '../services/toast.service';

let lastConnectionToastTime =
  0;

export const apiErrorInterceptor:
  HttpInterceptorFn = (
    request,
    next,
  ) => {
    const router =
      inject(Router);

    const authApi =
      inject(AuthApiService);

    const toastService =
      inject(ToastService);

    return next(request).pipe(
      catchError(
        (
          error:
            HttpErrorResponse,
        ) => {
          const isAuthenticationRequest =
            request.url.includes(
              '/api/auth/login',
            ) ||
            request.url.includes(
              '/api/auth/register',
            );

          if (
            error.status === 401 &&
            !isAuthenticationRequest
          ) {
            authApi
              .clearLocalAuthentication();

            if (
              !router.url
                .startsWith(
                  '/login',
                )
            ) {
              toastService.warning(
                'Session expired',
                'Please sign in again to continue.',
              );

              void router.navigate(
                [
                  '/login',
                ],
                {
                  queryParams: {
                    returnUrl:
                      router.url,
                  },
                },
              );
            }
          }

          if (error.status === 403) {
            if (
              !router.url
                .startsWith(
                  '/access-denied',
                )
            ) {
              void router.navigate([
                '/access-denied',
              ]);
            }
          }

          if (error.status === 0) {
            const currentTime =
              Date.now();

            if (
              currentTime -
                lastConnectionToastTime >
              3000
            ) {
              lastConnectionToastTime =
                currentTime;

              toastService.error(
                'Connection problem',
                'The server could not be reached. Check your connection and try again.',
              );
            }
          }

          return throwError(
            () =>
              error,
          );
        },
      ),
    );
  };