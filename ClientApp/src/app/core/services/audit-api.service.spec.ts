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
  AuditPagedResponse,
} from '../models/audit-api.model';

import {
  AuditApiService,
} from './audit-api.service';

describe(
  'AuditApiService',
  () => {
    let service:
      AuditApiService;

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
          AuditApiService,
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
      'requests a sorted filtered paginated audit page',
      () => {
        const response:
          AuditPagedResponse = {
            items: [],
            page: 2,
            pageSize: 50,
            totalCount: 0,
            totalPages: 1,
            hasPreviousPage: false,
            hasNextPage: false,
            generatedAt:
              '2026-08-07T12:00:00',
          };

        service
          .getAudit({
            sortColumn:
              'owner',

            sortDirection:
              'desc',

            search:
              'MAT',

            owner:
              'Manoj',

            processStatus:
              'Pending',

            materialNo:
              '100',

            poNumber:
              '4500',

            columnDispatchStatus:
              'Pending',

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
              '/api/audit',
          );

        expect(
          request.request.method,
        ).toBe('GET');

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
        ).toBe('owner');

        expect(
          request.request.params.get(
            'sortDirection',
          ),
        ).toBe('desc');

        expect(
          request.request.params.get(
            'search',
          ),
        ).toBe('MAT');

        expect(
          request.request.params.get(
            'owner',
          ),
        ).toBe('Manoj');

        expect(
          request.request.params.get(
            'processStatus',
          ),
        ).toBe('Pending');

        expect(
          request.request.params.get(
            'materialNo',
          ),
        ).toBe('100');

        expect(
          request.request.params.get(
            'poNumber',
          ),
        ).toBe('4500');

        expect(
          request.request.params.get(
            'columnDispatchStatus',
          ),
        ).toBe('Pending');

        request.flush(
          response,
        );
      },
    );
  },
);