import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';

import {
  provideHttpClient,
  withFetch,
  withInterceptors,
} from '@angular/common/http';

import { provideRouter } from '@angular/router';

import { routes } from './app.routes';

import {
  apiErrorInterceptor,
} from './core/interceptors/api-error.interceptor';


export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    provideHttpClient(
      withFetch(),
      withInterceptors([

        apiErrorInterceptor,
      ]),
    ),

    provideRouter(routes),
  ],
};
