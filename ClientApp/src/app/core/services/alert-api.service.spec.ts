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
  AlertsPagedResponse,
} from '../models/alert-api.model';

import {
  AlertApiService,
} from './alert-api.service';

describe(
  'AlertApiService',
  () => {
    let service:
      AlertApiService;

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
          AlertApiService,
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
      'requests a sorted filtered paginated alert page',
      () => {
        const response:
          AlertsPagedResponse = {
            generatedAt:
              '2026-08-07T12:00:00',

            summary: {
              processStartAlerts: 0,
              overdueMaterials: 0,
              deliveryWithinSevenDays: 0,
              pendingProcess: 0,
              dispatchPending: 0,
              dispatchDateAlerts: 0,
              lowStock: 0,
              totalAlerts: 0,
            },

            category:
              'low-stock',

            items: [],

            page: 2,
            pageSize: 50,
            totalCount: 0,
            totalPages: 1,
            hasPreviousPage: false,
            hasNextPage: false,
          };

        service
          .getAlerts({
            category:
              'low-stock',

            sortColumn:
              'stock',

            sortDirection:
              'desc',

            search:
              'shaft',

            material:
              'MAT-100',

            stockStatus:
              'Shortage',

            page: 2,
            pageSize: 50,
          })
          .subscribe(
            result =>
              expect(result)
                .toEqual(response),
          );

        const request =
          httpController.expectOne(
            candidate =>
              candidate.url ===
              '/api/alerts',
          );

        expect(
          request.request.method,
        ).toBe('GET');

        expect(
          request.request.params.get(
            'category',
          ),
        ).toBe('low-stock');

        expect(
          request.request.params.get(
            'page',
          ),
        ).toBe('2');

        expect(
          request.request.params.get(
            'pageSize',
          ),
        ).toBe('50');

        expect(
          request.request.params.get(
            'sortColumn',
          ),
        ).toBe('stock');

        expect(
          request.request.params.get(
            'sortDirection',
          ),
        ).toBe('desc');

        expect(
          request.request.params.get(
            'search',
          ),
        ).toBe('shaft');

        expect(
          request.request.params.get(
            'material',
          ),
        ).toBe('MAT-100');

        expect(
          request.request.params.get(
            'stockStatus',
          ),
        ).toBe('Shortage');

        expect(
          request.request
            .withCredentials,
        ).toBe(true);

        request.flush(
          response,
        );
      },
    );
  },
);