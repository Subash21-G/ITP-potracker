import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MaterialUpsertRequest } from '../models/material-crud.model';
import { MaterialCrudApiService } from './material-crud-api.service';

describe('MaterialCrudApiService', () => {
  let service: MaterialCrudApiService;
  let httpController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(MaterialCrudApiService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  it('sends planning values and refreshes cached details after an update', () => {
    service.getDetails(42).subscribe();

    httpController
      .expectOne('/api/materials/42')
      .flush({ id: 42 });

    const request = {
      purchaseOrderId: 7,
      sourcingType: 'Outsourcing',
      outsourcedTo: 'Anita Fabrication',
      hasCostVariation: true,
      variationCost: 1250.75,
      priorityLevel: 'Normal',
    } as MaterialUpsertRequest;

    service.update(42, request).subscribe();

    const xsrfRequest =
      httpController.expectOne('/api/auth/xsrf-token');

    expect(xsrfRequest.request.method).toBe('GET');
    xsrfRequest.flush({ success: true });

    const updateRequest =
      httpController.expectOne('/api/materials/42');

    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual(request);
    expect(updateRequest.request.body.outsourcedTo).toBe(
      'Anita Fabrication',
    );
    expect(updateRequest.request.body.variationCost).toBe(1250.75);

    updateRequest.flush({
      success: true,
      message: 'Material updated successfully.',
      materialId: 42,
      matchedPendingInvoiceRows: 0,
    });

    service.getDetails(42).subscribe();

    const refreshedDetailsRequest =
      httpController.expectOne('/api/materials/42');

    expect(refreshedDetailsRequest.request.method).toBe('GET');
    refreshedDetailsRequest.flush({ id: 42 });
  });
});
