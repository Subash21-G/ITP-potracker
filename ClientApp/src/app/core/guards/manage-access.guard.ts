import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import {
  CanActivateFn,
  Router,
} from '@angular/router';
import {
  catchError,
  map,
  of,
} from 'rxjs';

type JsonObject = Record<string, unknown>;

const allowedManageRoles = new Set([
  'admin',
  'production',
]);

function findRole(
  value: unknown,
  depth = 0,
): string {
  if (
    value === null ||
    value === undefined ||
    depth > 4
  ) {
    return '';
  }

  if (Array.isArray(value)) {
    for (const item of value) {
      if (typeof item === 'string') {
        const normalized = item
          .trim()
          .toLowerCase();

        if (allowedManageRoles.has(normalized)) {
          return item;
        }
      }

      const nestedRole = findRole(
        item,
        depth + 1,
      );

      if (nestedRole) {
        return nestedRole;
      }
    }

    return '';
  }

  if (typeof value !== 'object') {
    return '';
  }

  const objectValue = value as JsonObject;

  for (const [key, candidate] of Object.entries(
    objectValue,
  )) {
    const normalizedKey = key
      .replace(/[_-]/g, '')
      .toLowerCase();

    if (
      [
        'role',
        'rolename',
        'userrole',
      ].includes(normalizedKey) &&
      typeof candidate === 'string'
    ) {
      return candidate;
    }

    if (
      normalizedKey === 'roles' &&
      Array.isArray(candidate)
    ) {
      const firstRole = candidate.find(
        (item) => typeof item === 'string',
      );

      if (typeof firstRole === 'string') {
        return firstRole;
      }
    }
  }

  for (const candidate of Object.values(
    objectValue,
  )) {
    const nestedRole = findRole(
      candidate,
      depth + 1,
    );

    if (nestedRole) {
      return nestedRole;
    }
  }

  return '';
}

export const manageAccessGuard: CanActivateFn = (
  _route,
  state,
) => {
  const http = inject(HttpClient);
  const router = inject(Router);

  return http
    .get<unknown>(
      '/api/auth/current-user',
      {
        withCredentials: true,
      },
    )
    .pipe(
      map((response) => {
        const role = findRole(response)
          .trim()
          .toLowerCase();

        if (allowedManageRoles.has(role)) {
          return true;
        }

        const fallbackRoute = state.url.startsWith(
          '/materials',
        )
          ? '/materials'
          : state.url.startsWith(
                '/purchase-orders',
              )
            ? '/purchase-orders'
            : '/dashboard';

        return router.createUrlTree(
          [fallbackRoute],
          {
            queryParams: {
              accessDenied: '1',
            },
          },
        );
      }),
      catchError(() =>
        of(
          router.createUrlTree(
            ['/login'],
            {
              queryParams: {
                returnUrl: state.url,
              },
            },
          ),
        ),
      ),
    );
};
