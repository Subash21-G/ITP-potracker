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
        search: 'shaft',
        poNumber: 'PO-100',
      })
      .subscribe((result) => expect(result).toEqual(response));

    const request = httpController.expectOne((candidate) => candidate.url === '/api/materials');

    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('search')).toBe('shaft');
    expect(request.request.params.get('poNumber')).toBe('PO-100');
    expect(request.request.withCredentials).toBe(true);

    request.flush(response);
  });
});
