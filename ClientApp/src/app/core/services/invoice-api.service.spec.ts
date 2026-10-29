import {
  provideHttpClient,
} from '@angular/common/http';

import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';

import {
  TestBed,
} from '@angular/core/testing';

import {
  InvoiceListResponse,
} from '../models/invoice-api.model';

import {
  InvoiceApiService,
} from './invoice-api.service';

describe(
  'InvoiceApiService',
  () => {
    let service:
      InvoiceApiService;

    let httpController:
      HttpTestingController;

    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
        ],
      });

      service =
        TestBed.inject(
          InvoiceApiService,
        );

      httpController =
        TestBed.inject(
          HttpTestingController,
        );
    });

    afterEach(() => {
      httpController.verify();
    });

    it('exports all pending rows with the active search, sort, and column filters', () => {
      const filters = JSON.stringify({ material: ['Spool'] });
      service.downloadPendingItemsExcel('INV-001', 'amount', 'desc', filters)
        .subscribe();

      const request = httpController.expectOne(
        candidate => candidate.url === '/api/invoices/pending/export-excel',
      );
      expect(request.request.method).toBe('GET');
      expect(request.request.responseType).toBe('blob');
      expect(request.request.params.get('search')).toBe('INV-001');
      expect(request.request.params.get('sortColumn')).toBe('amount');
      expect(request.request.params.get('sortDirection')).toBe('desc');
      expect(request.request.params.get('excelFilters')).toBe(filters);
      expect(request.request.params.has('page')).toBe(false);
      request.flush(new Blob());
    });

    it(
      'requests a sorted filtered paginated invoice page',
      () => {
        const response:
          InvoiceListResponse = {
            items: [],
            summary: {
              totalInvoices: 0,
              totalItems: 0,
              totalQuantity: 0,
              totalGstAmount: 0,
              totalInvoiceAmount: 0,
              pendingItemCount: 0,
            },
            page: 2,
            pageSize: 25,
            totalCount: 0,
            totalPages: 1,
          };

        service
          .getInvoices({
            page: 2,
            pageSize: 25,

            sortColumn:
              'amount',

            sortDirection:
              'desc',

            search:
              'INV',

            invoiceNumber:
              'INV-001',

            customerName:
              'WEG',

            minItemCount:
              '1',

            maxItemCount:
              '20',
          })
          .subscribe(
            (result) =>
              expect(result)
                .toEqual(response),
          );

        const request =
          httpController.expectOne(
            (candidate) =>
              candidate.url ===
              '/api/invoices',
          );

        expect(
          request.request.method,
        ).toBe('GET');

        expect(
          request.request.params.get('page'),
        ).toBe('2');

        expect(
          request.request.params.get('pageSize'),
        ).toBe('25');

        expect(
          request.request.params.get('sortColumn'),
        ).toBe('amount');

        expect(
          request.request.params.get('sortDirection'),
        ).toBe('desc');

        expect(
          request.request.params.get('search'),
        ).toBe('INV');

        expect(
          request.request.params.get('invoiceNumber'),
        ).toBe('INV-001');

        expect(
          request.request.params.get('customerName'),
        ).toBe('WEG');

        expect(
          request.request.params.get('minItemCount'),
        ).toBe('1');

        expect(
          request.request.params.get('maxItemCount'),
        ).toBe('20');

        expect(
          request.request.withCredentials,
        ).toBe(true);

        request.flush(response);
      },
    );
  },
);
