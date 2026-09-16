import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import {
  MaterialHistoryListResponse,
  MaterialHistorySaveResponse,
  MaterialHistoryUpsertRequest,
} from '../models/material-history-api.model';
import { AuthApiService } from './auth-api.service';
import { MaterialHistoryApiService } from './material-history-api.service';

describe('MaterialHistoryApiService', () => {
  let service: MaterialHistoryApiService;
  let httpController: HttpTestingController;
  let authApi: { getXsrfToken: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    authApi = {
      getXsrfToken: vi.fn(() => of('test-token')),
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthApiService,
          useValue: authApi,
        },
      ],
    });

    service = TestBed.inject(MaterialHistoryApiService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  it('requests the selected history page and trims the search term', () => {
    const response: MaterialHistoryListResponse = {
      items: [],
      summary: {
        totalEntries: 0,
        perfectDeliveries: 0,
        reworkDeliveries: 0,
        rejected: 0,
      },
      page: 2,
      pageSize: 50,
      totalRecords: 0,
      totalPages: 1,
    };

    service
      .getHistory({
        search: '  MAT-100  ',
        outcome: 'Rejected',
        materialId: 42,
        sortColumn: 'date',
        sortDirection: 'desc',
        excelFilters: '{"remark":["Dimension outside tolerance"]}',
        page: 2,
        pageSize: 50,
      })
      .subscribe((result) => expect(result).toEqual(response));

    const request = httpController.expectOne(
      (candidate) => candidate.url === '/api/material-history',
    );

    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('search')).toBe('MAT-100');
    expect(request.request.params.get('outcome')).toBe('Rejected');
    expect(request.request.params.get('materialId')).toBe('42');
    expect(request.request.params.get('sortColumn')).toBe('date');
    expect(request.request.params.get('sortDirection')).toBe('desc');
    expect(request.request.params.get('excelFilters')).toBe(
      '{"remark":["Dimension outside tolerance"]}',
    );
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('50');
    expect(request.request.withCredentials).toBe(true);

    request.flush(response);
  });

  it('requests server filter values with the other active filters', () => {
    service
      .getExcelFilterValues(
        {
          search: '  MAT-100  ',
          outcome: 'Rejected',
          materialId: 42,
          excelFilters: '{"result":["Rejected"]}',
          page: 1,
          pageSize: 25,
        },
        'remark',
      )
      .subscribe((result) => expect(result).toEqual([]));

    const request = httpController.expectOne(
      (candidate) => candidate.url === '/api/material-history/excel-filter-values',
    );

    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('column')).toBe('remark');
    expect(request.request.params.get('search')).toBe('MAT-100');
    expect(request.request.params.get('outcome')).toBe('Rejected');
    expect(request.request.params.get('materialId')).toBe('42');
    expect(request.request.params.get('excelFilters')).toBe('{"result":["Rejected"]}');
    expect(request.request.withCredentials).toBe(true);

    request.flush([]);
  });

  it('gets an XSRF token before creating a history entry', () => {
    const requestBody: MaterialHistoryUpsertRequest = {
      materialId: 42,
      outcome: 'Rejected',
      remark: 'Dimension outside tolerance',
      occurredDate: '2026-08-28',
    };
    const response = {
      success: true,
      message: 'Material history added successfully.',
      history: {
        id: 7,
        materialId: 42,
        materialNo: 'MAT-100',
        lineItem: '10',
        poNumber: 'PO-1001',
        description: 'Machined ring',
        outcome: 'Rejected',
        remark: requestBody.remark,
        occurredDate: '2026-08-28T00:00:00',
        createdDate: '2026-08-28T10:00:00',
        updatedDate: null,
      },
    } satisfies MaterialHistorySaveResponse;

    service.create(requestBody).subscribe((result) => expect(result).toEqual(response));

    expect(authApi.getXsrfToken).toHaveBeenCalledOnce();

    const request = httpController.expectOne('/api/material-history');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(requestBody);
    expect(request.request.withCredentials).toBe(true);

    request.flush(response);
  });

  it('updates the selected history entry through its id route', () => {
    const requestBody: MaterialHistoryUpsertRequest = {
      materialId: 42,
      outcome: 'ReworkAndDelivery',
      remark: 'Edge reworked before delivery',
      occurredDate: '2026-08-29',
    };

    service.update(7, requestBody).subscribe();

    expect(authApi.getXsrfToken).toHaveBeenCalledOnce();

    const request = httpController.expectOne('/api/material-history/7');

    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(requestBody);
    expect(request.request.withCredentials).toBe(true);

    request.flush({
      success: true,
      message: 'Material history updated successfully.',
      history: {
        id: 7,
        materialId: 42,
        materialNo: 'MAT-100',
        lineItem: '10',
        poNumber: 'PO-1001',
        description: 'Machined ring',
        outcome: requestBody.outcome,
        remark: requestBody.remark,
        occurredDate: '2026-08-29T00:00:00',
        createdDate: '2026-08-28T10:00:00',
        updatedDate: '2026-08-29T10:00:00',
      },
    } satisfies MaterialHistorySaveResponse);
  });
});
