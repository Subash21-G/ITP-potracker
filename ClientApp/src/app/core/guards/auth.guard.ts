import { inject } from '@angular/core';
import {
  CanActivateFn,
  Router,
  UrlTree,
} from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';
import { AuthApiService } from '../services/auth-api.service';

function createLoginRedirect(
  router: Router,
  returnUrl: string,
): UrlTree {
  return router.createUrlTree(['/login'], {
    queryParams: {
      returnUrl,
    },
  });
}

function ensureCurrentUser(
  returnUrl: string,
): Observable<boolean | UrlTree> | boolean | UrlTree {
  const authApi = inject(AuthApiService);
  const router = inject(Router);

  if (authApi.authenticationChecked()) {
    return authApi.isAuthenticated()
      ? true
      : createLoginRedirect(router, returnUrl);
  }

  return authApi.getCurrentUser().pipe(
    map((user) =>
      user.isAuthenticated
        ? true
        : createLoginRedirect(router, returnUrl),
    ),
    catchError(() => {
      authApi.clearLocalAuthentication();

      return of(
        createLoginRedirect(router, returnUrl),
      );
    }),
  );
}

/**
 * Allows any authenticated Admin, Production or Viewer user.
 */
export const authGuard: CanActivateFn = (
  _route,
  state,
) => ensureCurrentUser(state.url);

/**
 * Allows only Admin users.
 */
export const adminGuard: CanActivateFn = (
  _route,
  state,
) => {
  const authApi = inject(AuthApiService);
  const router = inject(Router);

  const verifyRole = (): boolean | UrlTree =>
    authApi.isAdmin()
      ? true
      : router.createUrlTree(['/access-denied']);

  if (authApi.authenticationChecked()) {
    return authApi.isAuthenticated()
      ? verifyRole()
      : createLoginRedirect(router, state.url);
  }

  return authApi.getCurrentUser().pipe(
    map((user) =>
      user.isAuthenticated
        ? verifyRole()
        : createLoginRedirect(router, state.url),
    ),
    catchError(() => {
      authApi.clearLocalAuthentication();

      return of(
        createLoginRedirect(router, state.url),
      );
    }),
  );
};

/**
 * Allows Admin and Production users.
 * Viewer accounts remain read-only.
 */
export const operatorGuard: CanActivateFn = (
  _route,
  state,
) => {
  const authApi = inject(AuthApiService);
  const router = inject(Router);

  const verifyRole = (): boolean | UrlTree =>
    authApi.canOperate()
      ? true
      : router.createUrlTree(['/access-denied']);

  if (authApi.authenticationChecked()) {
    return authApi.isAuthenticated()
      ? verifyRole()
      : createLoginRedirect(router, state.url);
  }

  return authApi.getCurrentUser().pipe(
    map((user) =>
      user.isAuthenticated
        ? verifyRole()
        : createLoginRedirect(router, state.url),
    ),
    catchError(() => {
      authApi.clearLocalAuthentication();

      return of(
        createLoginRedirect(router, state.url),
      );
    }),
  );
};
