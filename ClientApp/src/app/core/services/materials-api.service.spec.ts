import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MaterialListItem, PagedResult } from '../models/material.model';
import { MaterialsApiService } from './materials-api.service';

describe('MaterialsApiService', () => {
  let service: MaterialsApiService;
  let httpController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(MaterialsApiService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  it('requests a filtered, paginated material page', () => {
    const response: PagedResult<MaterialListItem> = {
      items: [],
      page: 2,
      pageSize: 25,
      totalRecords: 30,
      totalPages: 2,
      hasPreviousPage: true,
      hasNextPage: false,
    };

    service
      .getMaterials({
        page: 2,
        pageSize: 25,
        sortColumn: 'delivery',
        sortDirection: 'desc',
        search: 'shaft',
        poNumber: 'PO-100',
        handler: 'Manoj',
        poDateFrom: '2026-01-01',
        poDateTo: '2026-01-31',
        poReceivedDateFrom: '2026-02-01',
        poReceivedDateTo: '2026-02-28',
        invoiceStatus: 'Not Invoiced',
        dispatchStatus: 'Pending',
        stockStatus: 'Available',
        deliveryDateFrom: '2026-03-01',
        deliveryDateTo: '2026-03-31',
      })
      .subscribe(
        (result) =>
          expect(result).toEqual(
            response,
          ),
      );

    const request =
      httpController.expectOne(
        (candidate) =>
          candidate.url ===
          '/api/materials',
      );

    expect(request.request.method)
      .toBe('GET');

    expect(
      request.request.params.get(
        'page',
      ),
    ).toBe('2');

    expect(
      request.request.params.get(
        'pageSize',
      ),
    ).toBe('25');

    expect(
      request.request.params.get(
        'sortColumn',
      ),
    ).toBe('delivery');

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
        'poNumber',
      ),
    ).toBe('PO-100');

    expect(
      request.request.params.get(
        'handler',
      ),
    ).toBe('Manoj');

    expect(
      request.request.params.get(
        'poDateFrom',
      ),
    ).toBe('2026-01-01');

    expect(
      request.request.params.get(
        'poDateTo',
      ),
    ).toBe('2026-01-31');

    expect(
      request.request.params.get(
        'poReceivedDateFrom',
      ),
    ).toBe('2026-02-01');

    expect(
      request.request.params.get(
        'poReceivedDateTo',
      ),
    ).toBe('2026-02-28');

    expect(
      request.request.params.get(
        'invoiceStatus',
      ),
    ).toBe('Not Invoiced');

    expect(
      request.request.params.get(
        'dispatchStatus',
      ),
    ).toBe('Pending');

    expect(
      request.request.params.get(
        'stockStatus',
      ),
    ).toBe('Available');

    expect(
      request.request.params.get(
        'deliveryDateFrom',
      ),
    ).toBe('2026-03-01');

    expect(
      request.request.params.get(
        'deliveryDateTo',
      ),
    ).toBe('2026-03-31');

    expect(
      request.request.withCredentials,
    ).toBe(true);

    request.flush(response);
  });
});
