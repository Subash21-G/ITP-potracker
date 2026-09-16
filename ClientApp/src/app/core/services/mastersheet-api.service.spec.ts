import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MasterSheetSaveRequest } from '../models/mastersheet-api.model';
import { MasterSheetApiService } from './mastersheet-api.service';

describe('MasterSheetApiService', () => {
  let service: MasterSheetApiService;
  let httpController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(MasterSheetApiService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  it('saves a master record and all ordered process rows', () => {
    const request: MasterSheetSaveRequest = {
      materialNo: 'MAT/100',
      shortDescription: 'Machined shaft',
      grade: 'EN8',
      drawingNo: 'DWG-100',
      drawingVersion: 'V3',
      finishSize: '25 mm',
      requiredRmSize: '30 mm',
      processSteps: [
        {
          processName: 'Turning',
          vendorName: 'Vendor A',
          source: 'Outsource',
        },
        {
          processName: 'Inspection',
          vendorName: '',
          source: 'In-house',
        },
      ],
    };

    service.saveRecord(request).subscribe();

    httpController.expectOne('/api/auth/xsrf-token').flush({ success: true });

    const saveRequest = httpController.expectOne('/api/mastersheet/record');
    expect(saveRequest.request.method).toBe('PUT');
    expect(saveRequest.request.body).toEqual(request);
    expect(saveRequest.request.body.processSteps).toHaveLength(2);
    saveRequest.flush({ ...request, id: 1, createdDate: '', updatedDate: null });
  });

  it('loads material numbers through query parameters without changing the value', () => {
    service.getRecord('MAT/100 A').subscribe();

    const loadRequest = httpController.expectOne(
      (request) => request.url === '/api/mastersheet/record',
    );

    expect(loadRequest.request.method).toBe('GET');
    expect(loadRequest.request.params.get('materialNo')).toBe('MAT/100 A');
    loadRequest.flush({
      id: 1,
      materialNo: 'MAT/100 A',
      shortDescription: '',
      grade: '',
      drawingVersion: '',
      finishSize: '',
      requiredRmSize: '',
      processSteps: [],
      createdDate: '',
      updatedDate: null,
    });
  });
});
