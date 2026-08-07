import {
  HttpInterceptorFn,
  HttpResponse,
} from '@angular/common/http';

import {
  inject,
} from '@angular/core';

import {
  tap,
} from 'rxjs';

import {
  ApiResponseCacheService,
} from '../services/api-response-cache.service';

const readOnlyMethods =
  new Set([
    'GET',
    'HEAD',
    'OPTIONS',
  ]);

export const cacheInvalidationInterceptor:
  HttpInterceptorFn =
    (
      request,
      next,
    ) => {
      const cache =
        inject(
          ApiResponseCacheService,
        );

      const mutatesData =
        !readOnlyMethods.has(
          request.method.toUpperCase(),
        );

      return next(request).pipe(
        tap(event => {
          if (
            mutatesData &&
            event instanceof HttpResponse
          ) {
            cache.invalidate();
          }
        }),
      );
    };