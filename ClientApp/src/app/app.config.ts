import {
  registerLocaleData,
} from '@angular/common';

import localeEnIn from
  '@angular/common/locales/en-IN';

import {
  ApplicationConfig,
  LOCALE_ID,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';

import {
  provideHttpClient,
  withFetch,
  withInterceptors,
} from '@angular/common/http';

import {
  PreloadAllModules,
  provideRouter,
  withInMemoryScrolling,
  withPreloading,
} from '@angular/router';

import {
  routes,
} from './app.routes';

registerLocaleData(
  localeEnIn,
);

import {
  apiErrorInterceptor,
} from './core/interceptors/api-error.interceptor';

import {
  cacheInvalidationInterceptor,
} from './core/interceptors/cache-invalidation.interceptor';

export const appConfig:
  ApplicationConfig = {
    providers: [
      {
        provide: LOCALE_ID,
        useValue: 'en-IN',
      },

      provideBrowserGlobalErrorListeners(),

      provideHttpClient(
        withFetch(),
        withInterceptors([
          cacheInvalidationInterceptor,
          apiErrorInterceptor,
        ]),
      ),

      provideRouter(
        routes,

        withInMemoryScrolling({
          anchorScrolling:
            'enabled',

          scrollPositionRestoration:
            'top',
        }),

        withPreloading(
          PreloadAllModules,
        ),
      ),
    ],
  };
