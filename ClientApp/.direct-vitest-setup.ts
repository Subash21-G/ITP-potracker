import {
  ɵresolveComponentResources as resolveComponentResources,
} from '@angular/core';
import {
  TestBed,
} from '@angular/core/testing';
import {
  BrowserTestingModule,
  platformBrowserTesting,
} from '@angular/platform-browser/testing';
import {
  readFile,
} from 'node:fs/promises';

TestBed.initTestEnvironment(
  BrowserTestingModule,
  platformBrowserTesting(),
);

await import(
  './src/app/shared/server-excel-filter/server-excel-filter'
);

await resolveComponentResources(
  (url) =>
    readFile(
      new URL(
        url,
        new URL(
          './src/app/shared/server-excel-filter/',
          import.meta.url,
        ),
      ),
      'utf8',
    ),
);
