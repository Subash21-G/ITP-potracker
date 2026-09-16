import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PDFArray, PDFDocument, PDFRawStream, decodePDFRawStream } from 'pdf-lib';
import type { PageViewport } from 'pdfjs-dist';

import { PdfRedactionEditor } from './pdf-redaction-editor';

describe('PdfRedactionEditor', () => {
  afterEach(() => TestBed.resetTestingModule());

  async function loadedEditor() {
    await TestBed.configureTestingModule({
      imports: [PdfRedactionEditor],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const editor = TestBed.createComponent(PdfRedactionEditor).componentInstance;
    const original = await PDFDocument.create();
    original.addPage([200, 100]);
    original.addPage([200, 100]);
    editor.sourceUrl = '/api/private-documents/drawings/test.pdf';
    editor.fileName = 'test.pdf';
    editor['sourcePdfBytes'] = await original.save();
    editor['pageViewports'].set(1, {
      width: 200, height: 100,
      convertToPdfPoint: (x: number, y: number) => [x, 100 - y],
    } as PageViewport);
    editor.isLoading.set(false);
    editor.masks.set([{ id: 1, pageNumber: 1, x: 0.1, y: 0.1, width: 0.5, height: 0.5, fill: 'white' }]);
    return editor;
  }

  it('exports the visible mask into the PDF and preserves every source page', async () => {
    const editor = await loadedEditor();
    const edited = await PDFDocument.load(await editor.createEditedPdf());
    expect(edited.getPageCount()).toBe(2);
    const streams = edited.getPage(0).node.Contents() as PDFArray;
    const operators = Array.from({ length: streams.size() }, (_, index) => {
      const stream = streams.lookup(index, PDFRawStream);
      return new TextDecoder().decode(decodePDFRawStream(stream).decode());
    }).join('\n');
    expect(operators).toContain('1 1 1 rg');
    expect(operators).toContain('1 0 0 1 20 40 cm');
    expect(operators).toContain('100 50 l');
    editor.undoMask();
    const undone = await PDFDocument.load(await editor.createEditedPdf());
    expect(undone.getPage(0).node.Contents()).toBeUndefined();
  });

  it('saves a PDF attachment and retains edits when a save fails so it can be retried', async () => {
    const editor = await loadedEditor();
    const save = vi.fn().mockRejectedValueOnce(new Error('Upload unavailable')).mockResolvedValueOnce(undefined);
    editor.savePdf = save;
    await editor.saveEditedPdf();
    expect(editor.errorMessage()).toBe('Upload unavailable');
    expect(editor.masks()).toHaveLength(1);
    expect(editor.isSaving()).toBe(false);
    await editor.saveEditedPdf();
    expect(save).toHaveBeenLastCalledWith(expect.any(File), editor.sourceUrl);
    const file = save.mock.calls[1][0] as File;
    expect(file.name).toBe('test-edited.pdf');
    expect(file.type).toBe('application/pdf');
    expect(file.size).toBeGreaterThan(0);
    expect(editor.errorMessage()).toBe('');
    expect(editor.saveMessage()).toBe('PDF edits saved.');
  });

  it('rejects printing a document that has not finished loading', async () => {
    const editor = await loadedEditor();
    editor.isLoading.set(true);
    await expect(editor.createEditedPdf()).rejects.toThrow('Wait for the PDF preview');
  });

  it('adds a normalized erase box and supports undo', async () => {
    await TestBed.configureTestingModule({
      imports: [PdfRedactionEditor],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(PdfRedactionEditor);
    const editor = fixture.componentInstance;
    editor.isLoading.set(false);
    const layer = document.createElement('div');

    Object.defineProperty(layer, 'getBoundingClientRect', {
      value: () => ({
        left: 10,
        top: 20,
        width: 200,
        height: 100,
        right: 210,
        bottom: 120,
        x: 10,
        y: 20,
        toJSON: () => ({}),
      }),
    });

    layer.setPointerCapture = vi.fn();
    layer.releasePointerCapture = vi.fn();
    layer.hasPointerCapture = vi.fn(() => true);

    const pointerEvent = (
      clientX: number,
      clientY: number,
    ) => ({
      clientX,
      clientY,
      currentTarget: layer,
      pointerId: 1,
      preventDefault: vi.fn(),
    }) as unknown as PointerEvent;

    editor.setTool('erase');
    editor.startMask(pointerEvent(30, 30), 1);
    editor.moveMask(pointerEvent(130, 80), 1);
    editor.finishMask(pointerEvent(130, 80), 1);

    expect(editor.masks()).toEqual([
      expect.objectContaining({
        pageNumber: 1,
        x: 0.1,
        y: 0.1,
        width: 0.5,
        height: 0.5,
        fill: 'white',
      }),
    ]);

    editor.undoMask();

    expect(editor.masks()).toEqual([]);
  });
});
