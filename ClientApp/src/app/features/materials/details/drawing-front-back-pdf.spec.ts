import {
  PDFDocument,
  StandardFonts,
  PDFArray,
  PDFRawStream,
  PDFHexString,
  decodePDFRawStream,
} from 'pdf-lib';

import {
  createDrawingFrontBackPdf,
} from './drawing-front-back-pdf';

describe('createDrawingFrontBackPdf', () => {
  it.each([[595, 842], [842, 595]])('prints drawing code, finish size and aligned process checks on a %s x %s page', async (width, height) => {
    const source = await PDFDocument.create();
    source.addPage([width, height]);
    const bytes = await createDrawingFrontBackPdf({ kind: 'pdf', bytes: await source.save() }, {
      materialNo: '11753667', shortDescription: 'INTERM BEAR CAP M 325X253X20',
      fullDescription: 'INTERMEDIATE BEARING CAP M CARBON STEEL SAE 1010/20',
      drawingCode: 'SWD 10001238633 000 01', finishSize: 'M 325X253X20',
      grade: 'CARBON STEEL SAE 1010/20', requiredQty: 1, rmSize: '330x245x25',
      processSteps: [{ processName: 'RM Procurement', vendorName: 'KG engg' }, { processName: 'turning', vendorName: 'vinoth' }],
    });
    const output = await PDFDocument.load(bytes);
    expect(output.getPageCount()).toBe(2);
    const contents = output.getPage(1).node.Contents() as PDFArray;
    const operators = Array.from({ length: contents.size() }, (_, index) =>
      new TextDecoder().decode(decodePDFRawStream(contents.lookup(index, PDFRawStream)).decode()),
    ).join('\n');
    const text = Array.from(operators.matchAll(/<([0-9A-Fa-f]+)> Tj/g), match => PDFHexString.of(match[1]).decodeText()).join('\n');
    for (const expected of ['DRAWING CODE', 'SWD 10001238633 000 01', 'FINISH SIZE', 'M 325X253X20', 'RM Procurement', 'KG engg', 'turning', 'vinoth', 'QTY', 'CHECK', 'REMARK', 'SIGN']) {
      expect(text).toContain(expected);
    }
    expect(text).not.toContain('ENTRY');
    expect(text).toContain('PROCESS\nVENDOR\nQTY\nCHECK\nREMARK\nSIGN');
  });
  it('keeps the drawing first and inserts its backside as page two', async () => {
    const source = await PDFDocument.create();
    const font = await source.embedFont(
      StandardFonts.Helvetica,
    );
    const firstPage = source.addPage([842, 595]);

    firstPage.drawText('ORIGINAL FRONT DRAWING', {
      x: 40,
      y: 540,
      size: 20,
      font,
    });
    source.addPage([842, 595]);

    const result = await createDrawingFrontBackPdf(
      {
        kind: 'pdf',
        bytes: await source.save(),
      },
      {
        materialNo: '11576700',
        fullDescription:
          'PLAIN WASHER 8X25X3 SS316L',
        drawingCode: 'SWD 10007585436 000 01',
        grade: 'STAINLESS STEEL AISI 316L',
      },
    );

    const combined = await PDFDocument.load(result);

    expect(combined.getPageCount()).toBe(2);
    expect(combined.getPage(0).getSize()).toEqual({
      width: 842,
      height: 595,
    });
    expect(combined.getPage(1).getSize()).toEqual({
      width: 842,
      height: 595,
    });
  });

  it('keeps unavailable values blank', async () => {
    const source = await PDFDocument.create();

    source.addPage([595, 842]);

    const result = await createDrawingFrontBackPdf(
      {
        kind: 'pdf',
        bytes: await source.save(),
      },
      {
        materialNo: '',
        fullDescription: '',
        drawingCode: '',
        grade: '',
      },
    );

    const combined = await PDFDocument.load(result);

    expect(combined.getPageCount()).toBe(2);
  });
});
