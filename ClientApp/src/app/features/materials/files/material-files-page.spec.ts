import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { MaterialDetails } from '../../../core/models/material-crud.model';
import { MaterialCrudApiService } from '../../../core/services/material-crud-api.service';
import { MaterialFilesPage } from './material-files-page';

describe('MaterialFilesPage', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('uploads the selected drawing when the form is submitted', async () => {
    const material = {
      id: 42,
      materialNo: 'MAT-42',
      poNumber: 'PO-42',
      lineItem: '10',
      pdfFilePath: '',
      drawingFilePath: '',
    } as MaterialDetails;

    const materialApi = {
      getDetails: vi.fn(() => of(material)),
      uploadFiles: vi.fn(() =>
        of({
          success: true,
          message: 'Material files uploaded successfully.',
          materialId: material.id,
          matchedPendingInvoiceRows: 0,
          material,
        }),
      ),
    };

    await TestBed.configureTestingModule({
      imports: [MaterialFilesPage],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: () => String(material.id),
              },
            },
          },
        },
        {
          provide: MaterialCrudApiService,
          useValue: materialApi,
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(MaterialFilesPage);
    fixture.detectChanges();

    const drawing = new File(
      ['drawing content'],
      'assembly.pdf',
      { type: 'application/pdf' },
    );

    fixture.componentInstance.onDrawingSelected({
      target: {
        files: {
          item: () => drawing,
        },
      },
    } as unknown as Event);

    expect(
      fixture.componentInstance.selectedDrawing(),
    ).toBe(drawing);

    fixture.detectChanges();

    const form =
      fixture.nativeElement.querySelector('form') as HTMLFormElement;

    form.dispatchEvent(
      new Event('submit', {
        bubbles: true,
        cancelable: true,
      }),
    );

    expect(materialApi.uploadFiles).toHaveBeenCalledWith(
      material.id,
      {
        pdfFile: null,
        drawingFile: drawing,
        handSketchFile: null,
        outsourceDrawingFile: null,
        webDrawingFile: null,
      },
    );
  });

  it('uploads hand-sketch and outsourced-drawing selections', async () => {
    const material = {
      id: 43,
      materialNo: 'MAT-43',
      poNumber: 'PO-43',
      lineItem: '20',
      pdfFilePath: '',
      drawingFilePath: '',
      handSketchFilePath: '',
      outsourceDrawingFilePath: '',
    } as MaterialDetails;

    const materialApi = {
      getDetails: vi.fn(() => of(material)),
      uploadFiles: vi.fn(() =>
        of({
          success: true,
          message: 'Material files uploaded successfully.',
          materialId: material.id,
          matchedPendingInvoiceRows: 0,
          material,
        }),
      ),
    };

    await TestBed.configureTestingModule({
      imports: [MaterialFilesPage],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: () => String(material.id),
              },
            },
          },
        },
        {
          provide: MaterialCrudApiService,
          useValue: materialApi,
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(MaterialFilesPage);
    fixture.detectChanges();

    const handSketch = new File(
      ['sketch'],
      'hand-sketch.jpg',
      { type: 'image/jpeg' },
    );

    const outsourcedDrawing = new File(
      ['cad'],
      'vendor-part.step',
      { type: 'application/step' },
    );

    fixture.componentInstance.onHandSketchSelected({
      target: {
        files: {
          item: () => handSketch,
        },
      },
    } as unknown as Event);

    fixture.componentInstance.onOutsourceDrawingSelected({
      target: {
        files: {
          item: () => outsourcedDrawing,
        },
      },
    } as unknown as Event);

    fixture.componentInstance.upload();

    expect(materialApi.uploadFiles).toHaveBeenCalledWith(
      material.id,
      {
        pdfFile: null,
        drawingFile: null,
        handSketchFile: handSketch,
        outsourceDrawingFile: outsourcedDrawing,
        webDrawingFile: null,
      },
    );
  });

  it('deletes the current shared drawing after confirmation', async () => {
    const material = {
      id: 84,
      materialNo: 'MAT-84',
      poNumber: 'PO-84',
      lineItem: '20',
      pdfFilePath:
        '/api/private-documents/pdfs/material.pdf',
      drawingFilePath:
        '/api/private-documents/drawings/assembly.pdf',
    } as MaterialDetails;

    const updatedMaterial = {
      ...material,
      drawingFilePath: '',
      drawingUpdatedDate: null,
    };

    const materialApi = {
      getDetails: vi.fn(() => of(material)),
      uploadFiles: vi.fn(),
      deleteDrawing: vi.fn(() =>
        of({
          success: true,
          message:
            'Drawing deleted successfully from 2 records with material number MAT-84.',
          materialId: material.id,
          matchedPendingInvoiceRows: 0,
          material: updatedMaterial,
        }),
      ),
    };

    vi.spyOn(
      window,
      'confirm',
    ).mockReturnValue(true);

    await TestBed.configureTestingModule({
      imports: [MaterialFilesPage],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: () => String(material.id),
              },
            },
          },
        },
        {
          provide: MaterialCrudApiService,
          useValue: materialApi,
        },
      ],
    }).compileComponents();

    const fixture =
      TestBed.createComponent(
        MaterialFilesPage,
      );

    fixture.detectChanges();

    const deleteButton =
      fixture.nativeElement.querySelector(
        '.delete-drawing-button',
      ) as HTMLButtonElement;

    deleteButton.click();
    fixture.detectChanges();

    expect(window.confirm).toHaveBeenCalledWith(
      'Delete the drawing for material MAT-84? It will be removed from every record with the same material number.',
    );

    expect(
      materialApi.deleteDrawing,
    ).toHaveBeenCalledWith(
      material.id,
      'drawing',
    );

    expect(
      fixture.componentInstance.material()
        ?.drawingFilePath,
    ).toBe('');

    expect(
      fixture.componentInstance.successMessage(),
    ).toContain(
      'Drawing deleted successfully',
    );
  });
});
