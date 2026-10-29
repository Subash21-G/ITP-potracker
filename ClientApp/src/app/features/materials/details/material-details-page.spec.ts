import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRoute,
  convertToParamMap,
  provideRouter,
} from '@angular/router';
import { of, throwError } from 'rxjs';

import { MaterialDetails } from '../../../core/models/material-crud.model';
import { AuthApiService } from '../../../core/services/auth-api.service';
import { MaterialCrudApiService } from '../../../core/services/material-crud-api.service';
import { MaterialHistoryApiService } from '../../../core/services/material-history-api.service';
import { MaterialsApiService } from '../../../core/services/materials-api.service';
import { MaterialDetailsPage } from './material-details-page';
import { PdfRedactionEditor } from '../../../shared/pdf-redaction-editor/pdf-redaction-editor';
import { MasterSheetApiService } from '../../../core/services/mastersheet-api.service';
import * as drawingPdf from './drawing-front-back-pdf';

describe('MaterialDetailsPage document preview', () => {
  afterEach(() => TestBed.resetTestingModule());
  it('switches the embedded preview when a document tile is clicked', async () => {
    const material = {
      id: 42,
      purchaseOrderId: 7,
      materialNo: 'MAT-42',
      processStatus: 'Pending',
      invoiceStatus: 'Not Invoiced',
      dispatchStatus: 'Pending',
      pdfFilePath: '/api/private-documents/pdfs/material.pdf',
      drawingFilePath: '/api/private-documents/drawings/drawing.pdf',
      handSketchFilePath:
        '/api/private-documents/hand-sketches/sketch.jpg',
      outsourceDrawingFilePath:
        '/api/private-documents/outsource-drawings/vendor.step',
      createdDate: '2026-08-28T00:00:00Z',
      canDelete: false,
    } as MaterialDetails;

    const materialApi = {
      getDetails: vi.fn(() => of(material)),
      getRelatedPurchaseOrders: vi.fn(() => of([])),
    };
    const historyApi = {
      getHistory: vi.fn(() =>
        of({
          items: [
            {
              id: 9,
              materialId: material.id,
              materialNo: material.materialNo,
              lineItem: '10',
              poNumber: 'PO-42',
              description: 'Machined ring',
              outcome: 'Rejected' as const,
              remark: 'Dimension outside tolerance',
              occurredDate: '2026-08-29T00:00:00',
              createdDate: '2026-08-29T10:00:00',
              updatedDate: null,
            },
          ],
          summary: {
            totalEntries: 1,
            perfectDeliveries: 0,
            reworkDeliveries: 0,
            rejected: 1,
          },
          page: 1,
          pageSize: 10,
          totalRecords: 1,
          totalPages: 1,
        }),
      ),
    };

    await TestBed.configureTestingModule({
      imports: [MaterialDetailsPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(
              convertToParamMap({
                id: String(material.id),
              }),
            ),
          },
        },
        {
          provide: MaterialCrudApiService,
          useValue: materialApi,
        },
        {
          provide: MaterialsApiService,
          useValue: {},
        },
        {
          provide: MaterialHistoryApiService,
          useValue: historyApi,
        },
        {
          provide: AuthApiService,
          useValue: {
            canOperate: signal(true),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(MaterialDetailsPage);
    fixture.detectChanges();

    expect(historyApi.getHistory).toHaveBeenCalledWith({
      materialId: material.id,
      page: 1,
      pageSize: 10,
    });

    expect(
      fixture.nativeElement.querySelector(
        '.remark-history-entry--rejected',
      )?.textContent,
    ).toContain('Dimension outside tolerance');

    expect(fixture.componentInstance.documentPreview()?.path)
      .toBe(material.drawingFilePath);

    const documentButtons = Array.from(
      fixture.nativeElement.querySelectorAll(
        'button.file-link',
      ) as NodeListOf<HTMLButtonElement>,
    );

    const materialPdfButton =
      documentButtons.find((button) =>
        button.textContent?.includes('Material PDF'),
      );

    expect(materialPdfButton).toBeTruthy();

    materialPdfButton?.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.documentPreview()?.path)
      .toBe(material.pdfFilePath);

    expect(
      fixture.nativeElement.querySelector(
        '.file-link--active',
      )?.textContent,
    ).toContain('Material PDF');

    const handSketchButton =
      documentButtons.find((button) =>
        button.textContent?.includes('Hand sketch'),
      );

    expect(handSketchButton).toBeTruthy();

    handSketchButton?.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.documentPreview()?.path)
      .toBe(material.handSketchFilePath);

    expect(fixture.componentInstance.documentPreview()?.kind)
      .toBe('image');

    expect(
      fixture.nativeElement.querySelector(
        '.document-preview__image',
      ),
    ).toBeTruthy();
  });
});

describe('MaterialDetailsPage edited PDFs', () => {
  const original = {
    id: 42,
    drawingFilePath: '/drawings/cad.pdf',
    handSketchFilePath: '/sketches/sketch.pdf',
    outsourceDrawingFilePath: '/outsourced/vendor.pdf',
    webDrawingFilePath: '/web/web.pdf',
    pdfFilePath: '/pdfs/material.pdf',
  } as MaterialDetails;
  const api = { uploadFiles: vi.fn(), getDetails: vi.fn() };

  beforeEach(async () => {
    vi.clearAllMocks();
    await TestBed.configureTestingModule({
      imports: [MaterialDetailsPage],
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: '42' })) } },
        { provide: MaterialCrudApiService, useValue: api },
        { provide: MaterialsApiService, useValue: {} },
        { provide: MaterialHistoryApiService, useValue: {} },
        { provide: AuthApiService, useValue: { canOperate: signal(true) } },
      ],
    }).compileComponents();
  });
  afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); vi.useRealTimers(); TestBed.resetTestingModule(); });

  it('passes saved master drawing code and finish size to the backside PDF', async () => {
    const page = TestBed.createComponent(MaterialDetailsPage).componentInstance;
    page.material.set({ ...original, materialNo: '11753667', drawingNo: 'OLD CODE' });
    vi.spyOn(TestBed.inject(MasterSheetApiService), 'getRecord').mockReturnValue(of({
      drawingNo: 'SWD 10001238633 000 01', finishSize: 'M 325X253X20',
      processSteps: [{ sequence: 1, processName: 'RM Procurement', vendorName: 'KG engg' }],
    } as never));
    vi.spyOn(page as unknown as { loadDrawingFrontSource: (path: string) => Promise<drawingPdf.DrawingFrontSource> }, 'loadDrawingFrontSource').mockResolvedValue({ kind: 'pdf', bytes: new Uint8Array() });
    const createPdf = vi.spyOn(drawingPdf, 'createDrawingFrontBackPdf').mockResolvedValue(new Uint8Array([1]));
    vi.spyOn(window, 'open').mockReturnValue({ document: { body: {} }, location: { replace: vi.fn() }, close: vi.fn() } as unknown as Window);
    vi.stubGlobal('URL', { createObjectURL: () => 'blob:preview', revokeObjectURL: vi.fn() });
    vi.useFakeTimers();
    await page.printOutsourceDetails();
    expect(page.drawingPrintError()).toBe('');
    expect(createPdf).toHaveBeenCalledWith(expect.anything(), expect.objectContaining({
      drawingCode: 'SWD 10001238633 000 01', finishSize: 'M 325X253X20',
      processSteps: [{ processName: 'RM Procurement', vendorName: 'KG engg' }],
    }));
  });

  it.each([
    ['drawingFilePath', 'drawingFile'],
    ['handSketchFilePath', 'handSketchFile'],
    ['outsourceDrawingFilePath', 'outsourceDrawingFile'],
    ['webDrawingFilePath', 'webDrawingFile'],
    ['pdfFilePath', 'pdfFile'],
  ] as const)('saves %s to its own attachment and selects the saved version', async (pathField, uploadField) => {
    const page = TestBed.createComponent(MaterialDetailsPage).componentInstance;
    page.material.set(original);
    const updated = { ...original, [pathField]: '/saved/edited.pdf' };
    api.uploadFiles.mockReturnValue(of({ success: true, material: updated }));
    const file = new File(['edited'], 'edited.pdf', { type: 'application/pdf' });
    await page.saveEditedDocument(file, original[pathField]!);
    expect(api.uploadFiles).toHaveBeenCalledWith(42, { [uploadField]: file });
    expect(page.material()).toEqual(updated);
    expect(page.documentPreview()?.path).toBe('/saved/edited.pdf');
  });

  it('uses live preview edits for front + backside printing without fetching the original', async () => {
    const page = TestBed.createComponent(MaterialDetailsPage).componentInstance;
    const editedBytes = new Uint8Array([1, 2, 3]);
    const editor = { sourceUrl: original.outsourceDrawingFilePath, createEditedPdf: vi.fn().mockResolvedValue(editedBytes) };
    vi.spyOn(page, 'pdfEditor').mockReturnValue(editor as unknown as PdfRedactionEditor);
    const fetchSpy = vi.spyOn(globalThis, 'fetch');
    const source = await page['loadDrawingFrontSource'](original.outsourceDrawingFilePath!);
    expect(source).toEqual({ kind: 'pdf', bytes: editedBytes });
    expect(editor.createEditedPdf).toHaveBeenCalledOnce();
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('keeps the current attachment when saving fails', async () => {
    const page = TestBed.createComponent(MaterialDetailsPage).componentInstance;
    page.material.set(original);
    api.uploadFiles.mockReturnValue(throwError(() => new Error('Upload failed')));
    await expect(page.saveEditedDocument(new File(['x'], 'edited.pdf'), original.pdfFilePath!)).rejects.toThrow('Upload failed');
    expect(page.material()).toBe(original);
  });
});
