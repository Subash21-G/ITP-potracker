import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

const benignResizeObserverMessages =
  new Set([
    'ResizeObserver loop completed with undelivered notifications.',
    'ResizeObserver loop limit exceeded',
  ]);

window.addEventListener(
  'error',
  (event: ErrorEvent) => {
    if (
      benignResizeObserverMessages.has(
        event.message,
      )
    ) {
      event.preventDefault();
      event.stopImmediatePropagation();
    }
  },
  true,
);

bootstrapApplication(
  App,
  appConfig,
).catch(
  (error: unknown) =>
    console.error(error),
);