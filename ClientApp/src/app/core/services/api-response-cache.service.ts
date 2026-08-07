import {
  Injectable,
} from '@angular/core';

import {
  Observable,
  catchError,
  shareReplay,
  throwError,
} from 'rxjs';

interface ApiCacheEntry {
  readonly expiresAt: number;
  readonly response$: Observable<unknown>;
}

@Injectable({
  providedIn: 'root',
})
export class ApiResponseCacheService {
  private readonly entries =
    new Map<string, ApiCacheEntry>();

  get<T>(
    key: string,
    factory: () => Observable<T>,
    ttlMilliseconds = 10_000,
    forceRefresh = false,
  ): Observable<T> {
    const now =
      Date.now();

    const existing =
      this.entries.get(key);

    if (
      !forceRefresh &&
      existing &&
      existing.expiresAt > now
    ) {
      return existing.response$ as Observable<T>;
    }

    if (existing) {
      this.entries.delete(key);
    }

    const response$ =
      factory().pipe(
        catchError((error: unknown) => {
          const current =
            this.entries.get(key);

          if (
            current?.response$ ===
            response$
          ) {
            this.entries.delete(key);
          }

          return throwError(
            () => error,
          );
        }),

        shareReplay({
          bufferSize: 1,
          refCount: false,
        }),
      );

    this.entries.set(
      key,
      {
        expiresAt:
          now +
          Math.max(
            0,
            ttlMilliseconds,
          ),

        response$,
      },
    );

    return response$;
  }

  invalidate(
    prefix = '',
  ): void {
    if (!prefix) {
      this.entries.clear();
      return;
    }

    for (const key of this.entries.keys()) {
      if (key.startsWith(prefix)) {
        this.entries.delete(key);
      }
    }
  }
}