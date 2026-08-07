import {
  firstValueFrom,
  of,
} from 'rxjs';

import {
  ApiResponseCacheService,
} from './api-response-cache.service';

describe(
  'ApiResponseCacheService',
  () => {
    it(
      'reuses a response during its TTL',
      async () => {
        const service =
          new ApiResponseCacheService();

        let calls =
          0;

        const factory = () => {
          calls++;

          return of(calls);
        };

        const first =
          await firstValueFrom(
            service.get(
              'materials:list',
              factory,
              10_000,
            ),
          );

        const second =
          await firstValueFrom(
            service.get(
              'materials:list',
              factory,
              10_000,
            ),
          );

        expect(first).toBe(1);
        expect(second).toBe(1);
        expect(calls).toBe(1);
      },
    );

    it(
      'supports forced refresh',
      async () => {
        const service =
          new ApiResponseCacheService();

        let calls =
          0;

        const factory = () => {
          calls++;

          return of(calls);
        };

        await firstValueFrom(
          service.get(
            'dashboard',
            factory,
          ),
        );

        const refreshed =
          await firstValueFrom(
            service.get(
              'dashboard',
              factory,
              10_000,
              true,
            ),
          );

        expect(refreshed).toBe(2);
        expect(calls).toBe(2);
      },
    );

    it(
      'invalidates matching prefixes',
      async () => {
        const service =
          new ApiResponseCacheService();

        let calls =
          0;

        const factory = () => {
          calls++;

          return of(calls);
        };

        await firstValueFrom(
          service.get(
            'materials:detail:10',
            factory,
          ),
        );

        service.invalidate(
          'materials:',
        );

        const reloaded =
          await firstValueFrom(
            service.get(
              'materials:detail:10',
              factory,
            ),
          );

        expect(reloaded).toBe(2);
        expect(calls).toBe(2);
      },
    );
  },
);