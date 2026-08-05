import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import {
  InvoicePreviewDiscardResponse,
} from '../models/invoice-import-api.model';
import {
  InvoiceImportApiService,
} from './invoice-import-api.service';

describe('InvoiceImportApiService', () => {
  let service: InvoiceImportApiService;
  let httpController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service =
      TestBed.inject(
        InvoiceImportApiService,
      );

    httpController =
      TestBed.inject(
        HttpTestingController,
      );
  });

  afterEach(() => {
    httpController.verify();
  });

  it(
    'gets an XSRF token before discarding temporary invoice previews',
    () => {
      const uploadedFilePaths = [
        '/api/private-documents/invoices/preview-a.pdf',
        '/api/private-documents/invoices/preview-b.pdf',
      ];

      const response:
        InvoicePreviewDiscardResponse = {
          submittedPathCount: 2,
          uniqueRegisteredPathCount: 2,
          deletedPreviewCount: 2,
          protectedPreviewCount: 0,
          missingPreviewCount: 0,
          invalidPathCount: 0,
          unregisteredPathCount: 0,
        };

      let receivedResponse:
        InvoicePreviewDiscardResponse |
        undefined;

      service
        .discardPreviews(
          uploadedFilePaths,
        )
        .subscribe(result => {
          receivedResponse =
            result;
        });

      httpController.expectNone(
        '/api/invoice-import/discard-previews',
      );

      const xsrfRequest =
        httpController.expectOne(
          '/api/auth/xsrf-token',
        );

      expect(
        xsrfRequest.request.method,
      ).toBe('GET');

      expect(
        xsrfRequest.request.withCredentials,
      ).toBe(true);

      xsrfRequest.flush({
        success: true,
      });

      const discardRequest =
        httpController.expectOne(
          '/api/invoice-import/discard-previews',
        );

      expect(
        discardRequest.request.method,
      ).toBe('POST');

      expect(
        discardRequest.request.withCredentials,
      ).toBe(true);

      expect(
        discardRequest.request.body,
      ).toEqual({
        uploadedFilePaths,
      });

      discardRequest.flush(
        response,
      );

      expect(
        receivedResponse,
      ).toEqual(
        response,
      );
    },
  );
});