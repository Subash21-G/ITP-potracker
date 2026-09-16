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
import {
  isAbsolute,
} from 'node:path';

TestBed.initTestEnvironment(
  BrowserTestingModule,
  platformBrowserTesting(),
);

const canvasApi = await import('@napi-rs/canvas');

globalThis.DOMMatrix ??=
  canvasApi.DOMMatrix as typeof DOMMatrix;

globalThis.ImageData ??=
  canvasApi.ImageData as typeof ImageData;

globalThis.Path2D ??=
  canvasApi.Path2D as typeof Path2D;

await import(
  './src/app/app'
);

await import(
  './src/app/shared/server-excel-filter/server-excel-filter'
);

await import(
  './src/app/features/materials/files/material-files-page'
);

await import(
  './src/app/features/materials/details/material-details-page'
);

await resolveComponentResources(
  (url) => {
    const resourceBase =
      url.includes('toast-container')
        ? './src/app/shared/toast-container/'
        : url.includes('app.html') || url.includes('app.css')
        ? './src/app/'
        : url.includes('pdf-redaction-editor')
        ? './src/app/shared/pdf-redaction-editor/'
        : url.includes('material-details-page')
        ? './src/app/features/materials/details/'
        : url.includes('material-files-page')
          ? './src/app/features/materials/files/'
          : './src/app/shared/server-excel-filter/';

    return readFile(
      isAbsolute(url)
        ? url
        : new URL(
            url,
            new URL(
              resourceBase,
              import.meta.url,
            ),
          ),
      'utf8',
    );
  },
);
