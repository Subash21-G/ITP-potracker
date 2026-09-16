import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { MastersheetPage } from './mastersheet-page';

describe('MastersheetPage legacy storage migration', () => {
  const params = new BehaviorSubject(convertToParamMap({ section: 'vendors-processes' }));
  let httpController: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: ActivatedRoute, useValue: { paramMap: params } }],
    });
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpController.verify();
    localStorage.clear();
  });

  it('ignores an old lookup immediately when the material number changes or is cleared', () => {
    const page = TestBed.runInInjectionContext(() => new MastersheetPage());
    httpController.expectOne('/api/mastersheet/records').flush([]);
    httpController.expectOne('/api/mastersheet/process-options').flush([]);
    page.masterForm.controls.materialNo.setValue('MAT-OLD', { emitEvent: false });
    page.lookupMaster();
    httpController.expectOne(request => request.url === '/api/materials').flush({ items: [] });
    const oldLookup = httpController.expectOne(request => request.url === '/api/mastersheet/record');
    page.masterForm.controls.shortDescription.setValue('Old details');
    page.masterForm.controls.materialNo.setValue('MAT-NEW');
    expect(page.masterForm.controls.shortDescription.value).toBe('');
    expect(page.loading()).toBe(true);
    page.saveMaster();
    httpController.expectNone('/api/auth/xsrf-token');
    oldLookup.flush({ id: 1, materialNo: 'MAT-OLD', shortDescription: 'Old details', processSteps: [] });
    expect(page.masterForm.controls.materialNo.value).toBe('MAT-NEW');
    expect(page.loadedRecord()).toBeNull();

    page.lookupMaster();
    httpController.expectOne(request => request.url === '/api/materials').flush({ items: [] });
    const nextLookup = httpController.expectOne(request => request.url === '/api/mastersheet/record');
    page.masterForm.controls.materialNo.setValue('');
    page.lookupMaster();
    nextLookup.flush({ id: 2, materialNo: 'MAT-NEW', processSteps: [] });
    expect(page.masterForm.controls.materialNo.value).toBe('');
    expect(page.materialAssignments()).toEqual([]);
    expect(page.loadedRecord()).toBeNull();
    expect(page.loading()).toBe(false);
  });

  it('searches all records before pagination and resets each list independently', () => {
    const page = TestBed.runInInjectionContext(() => new MastersheetPage());
    httpController.expectOne('/api/mastersheet/records').flush(Array.from({ length: 26 }, (_, index) => ({
      id: index + 1, materialNo: `MAT-${index + 1}`, shortDescription: 'Shaft', grade: 'EN8',
      drawingVersion: 'V2', finishSize: '25 mm', requiredRmSize: '30 mm',
      processSteps: [{ id: index + 1, sequence: 1, processName: 'Turning', vendorName: 'Acme', source: 'Outsource' }],
    })));
    httpController.expectOne('/api/mastersheet/process-options').flush(Array.from({ length: 26 }, (_, index) => ({
      id: index + 1, processName: `Turning ${index + 1}`, vendorName: 'Acme', source: 'Outsource',
    })));
    page.processPage.set(3);
    page.materialPage.set(3);
    page.changeSearch('process', '  TURNING 26  ');
    expect(page.processPage()).toBe(1);
    expect(page.pagedProcesses().map(row => row.id)).toEqual([26]);
    expect(page.processTotalPages()).toBe(1);
    expect(page.materialPage()).toBe(3);
    expect(page.processOptions()).toHaveLength(26);

    page.changeSearch('material', 'mat-26');
    expect(page.materialPage()).toBe(1);
    expect(page.pagedRecords().map(row => row.id)).toEqual([26]);
    for (const query of ['shaft', 'en8', 'v2', '25 mm', '30 mm', 'turning', 'acme', 'outsource']) {
      page.changeSearch('material', query);
      expect(page.filteredRecords()).toHaveLength(26);
      expect(page.materialTotalPages()).toBe(3);
    }
    for (const section of ['process', 'material'] as const) {
      page.changeSearch(section, 'missing');
    }
    expect(page.pagedProcesses()).toEqual([]);
    expect(page.pagedRecords()).toEqual([]);
    expect(page.processTotalPages()).toBe(1);
    expect(page.materialTotalPages()).toBe(1);
    page.changeSearch('process', ' ACME ');
    expect(page.filteredProcesses()).toHaveLength(26);
    page.processPage.set(3);
    page.changeSearch('process', 'outsource');
    expect(page.processPage()).toBe(1);
    page.changeSearch('material', '');
    expect(page.pagedRecords()).toHaveLength(10);
    expect(page.materialTotalPages()).toBe(3);
  });

  it('moves existing process and vendor options into the database', () => {
    localStorage.setItem(
      'po-tracker-mastersheet-flow',
      JSON.stringify([
        { name: 'cutting', vendor: 'vinoth', source: 'Outsource' },
        { name: 'turner', vendor: 'perumal', source: 'Outsource' },
      ]),
    );

    const page = TestBed.runInInjectionContext(() => new MastersheetPage());
    httpController.expectOne('/api/mastersheet/records').flush([]);

    httpController.expectOne('/api/mastersheet/process-options').flush([]);

    const xsrfRequests = httpController.match('/api/auth/xsrf-token');
    expect(xsrfRequests).toHaveLength(2);
    xsrfRequests.forEach((request) => request.flush({ success: true }));

    const saveRequests = httpController.match('/api/mastersheet/process-options');
    expect(saveRequests).toHaveLength(2);
    expect(saveRequests[0].request.body).toEqual({
      processName: 'cutting',
      vendorName: 'vinoth',
      source: 'Outsource',
    });
    expect(saveRequests[1].request.body).toEqual({
      processName: 'turner',
      vendorName: 'perumal',
      source: 'Outsource',
    });

    saveRequests[0].flush({ id: 1, ...saveRequests[0].request.body });
    saveRequests[1].flush({ id: 2, ...saveRequests[1].request.body });

    expect(page.flow()).toHaveLength(2);
    expect(localStorage.getItem('po-tracker-mastersheet-flow')).toBeNull();
  });

  it('moves existing material fields and ordered processes into the database', () => {
    localStorage.setItem(
      'po-tracker-master-MAT-LEGACY',
      JSON.stringify({
        materialNo: 'MAT-LEGACY',
        shortDescription: 'Legacy shaft',
        grade: 'EN8',
        drawingVersion: 'V2',
        finishSize: '25 mm',
        requiredRmSize: '30 mm',
      }),
    );
    localStorage.setItem(
      'po-tracker-master-flow-MAT-LEGACY',
      JSON.stringify([
        { name: 'cutting', vendor: 'vinoth', source: 'Outsource' },
        { name: 'turner', vendor: 'perumal', source: 'Outsource' },
      ]),
    );

    const page = TestBed.runInInjectionContext(() => new MastersheetPage());
    httpController.expectOne('/api/mastersheet/records').flush([]);
    httpController.expectOne('/api/mastersheet/process-options').flush([]);

    page.masterForm.controls.materialNo.setValue('MAT-LEGACY', {
      emitEvent: false,
    });
    page.lookupMaster();

    httpController
      .expectOne((request) => request.url === '/api/materials')
      .flush({
        items: [],
        page: 1,
        pageSize: 100,
        totalRecords: 0,
        totalPages: 1,
      });
    httpController
      .expectOne(
        (request) =>
          request.url === '/api/mastersheet/record' &&
          request.params.get('materialNo') === 'MAT-LEGACY',
      )
      .flush(null, { status: 404, statusText: 'Not Found' });

    httpController.expectOne('/api/auth/xsrf-token').flush({ success: true });

    const saveRequest = httpController.expectOne('/api/mastersheet/record');
    expect(saveRequest.request.method).toBe('PUT');
    expect(saveRequest.request.body.grade).toBe('EN8');
    expect(saveRequest.request.body.processSteps).toEqual([
      { processName: 'cutting', vendorName: 'vinoth', source: 'Outsource' },
      { processName: 'turner', vendorName: 'perumal', source: 'Outsource' },
    ]);
    saveRequest.flush({
      id: 1,
      ...saveRequest.request.body,
      processSteps: saveRequest.request.body.processSteps.map(
        (step: Record<string, unknown>, index: number) => ({
          id: index + 1,
          sequence: index + 1,
          ...step,
        }),
      ),
      createdDate: '2026-09-05T00:00:00',
      updatedDate: null,
    });

    expect(page.loadedRecord()?.materialNo).toBe('MAT-LEGACY');
    expect(localStorage.getItem('po-tracker-master-MAT-LEGACY')).toBeNull();
    expect(localStorage.getItem('po-tracker-master-flow-MAT-LEGACY')).toBeNull();
  });
  it('paginates independently, retains all options, and clamps pages after removal', () => {
    const page = TestBed.runInInjectionContext(() => new MastersheetPage());
    httpController.expectOne('/api/mastersheet/records').flush(Array.from({ length: 11 }, (_, index) => ({
      id: index + 1, materialNo: `MAT-${index + 1}`, processSteps: [],
    })));
    httpController.expectOne('/api/mastersheet/process-options').flush(Array.from({ length: 26 }, (_, index) => ({
      id: index + 1, processName: `Process ${index + 1}`, vendorName: `Vendor ${index + 1}`, source: 'Outsource',
    })));
    expect(page.pagedProcesses()).toHaveLength(10);
    expect(page.pagedRecords()).toHaveLength(10);
    page.processPage.set(3);
    expect(page.pagedProcesses().map(row => row.id)).toEqual([21, 22, 23, 24, 25, 26]);
    expect(page.materialPage()).toBe(1);
    page.materialPage.set(2);
    expect(page.pagedRecords().map(row => row.materialNo)).toEqual(['MAT-11']);
    expect(page.processOptions()).toHaveLength(26);
    page.masterForm.controls.process.setValue('Process 1');
    expect(page.vendorsForSelectedProcess()).toEqual(['Vendor 1']);
    page.changePageSize('process', '25');
    expect(page.processPage()).toBe(1);
    expect(page.pagedProcesses()).toHaveLength(25);
    expect(page.materialPage()).toBe(2);
    page.savedRecords.update(records => records.slice(0, 10));
    expect(page.materialPage()).toBe(1);
    expect(page.pagedRecords()).toHaveLength(10);
    page.flow.set([]);
    page.savedRecords.set([]);
    expect(page.processPage()).toBe(1);
    expect(page.materialTotalPages()).toBe(1);
    expect(page.pagedProcesses()).toEqual([]);
    expect(page.pagedRecords()).toEqual([]);
  });

  it('switches pages without losing form entries or pagination', () => {
    const page = TestBed.runInInjectionContext(() => new MastersheetPage());
    httpController.expectOne('/api/mastersheet/records').flush([]);
    httpController.expectOne('/api/mastersheet/process-options').flush([]);
    page.form.controls.name.setValue('Draft process');
    page.masterForm.controls.shortDescription.setValue('Draft material');
    expect(page.isMaterialPage()).toBe(false);
    params.next(convertToParamMap({ section: 'materials' }));
    expect(page.isMaterialPage()).toBe(true);
    params.next(convertToParamMap({ section: 'vendors-processes' }));
    expect(page.isMaterialPage()).toBe(false);
    expect(page.form.controls.name.value).toBe('Draft process');
    expect(page.masterForm.controls.shortDescription.value).toBe('Draft material');
  });

});
