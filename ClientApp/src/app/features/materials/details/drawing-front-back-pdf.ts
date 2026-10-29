import {
  PDFDocument,
  PDFFont,
  PDFPage,
  StandardFonts,
  rgb,
} from 'pdf-lib';

export interface DrawingBacksideDetails {
  readonly materialNo: string;
  readonly shortDescription?: string;
  readonly fullDescription: string;
  readonly finishSize?: string;
  readonly drawingCode?: string;
  readonly grade: string;
  readonly processQty?: number;
  readonly requiredQty?: number;
  readonly stockQty?: number;
  readonly rmSize?: string;
  readonly rmProcess?: string;
  readonly rmSource?: string;
  readonly vendor?: string;
  readonly drawingVersion?: string;
  readonly processSteps?: readonly { readonly processName: string; readonly vendorName?: string }[];
}

export type DrawingFrontSource =
  | {
      readonly kind: 'pdf';
      readonly bytes: Uint8Array;
    }
  | {
      readonly kind: 'png' | 'jpeg';
      readonly bytes: Uint8Array;
    };

const a4Portrait = {
  width: 595.28,
  height: 841.89,
};

const a4Landscape = {
  width: a4Portrait.height,
  height: a4Portrait.width,
};

export async function createDrawingFrontBackPdf(
  source: DrawingFrontSource,
  details: DrawingBacksideDetails,
): Promise<Uint8Array> {
  const output = await PDFDocument.create();
  let frontSize: { width: number; height: number };

  if (source.kind === 'pdf') {
    const sourcePdf = await PDFDocument.load(source.bytes);
    const sourcePages = sourcePdf.getPages();

    if (sourcePages.length === 0) {
      throw new Error('The drawing PDF does not contain any pages.');
    }

    const copiedPages = await output.copyPages(
      sourcePdf,
      sourcePages.map((_, index) => index),
    );

    const frontPage = copiedPages[0];

    output.addPage(frontPage);
    frontSize = frontPage.getSize();
  } else {
    const image =
      source.kind === 'png'
        ? await output.embedPng(source.bytes)
        : await output.embedJpg(source.bytes);

    frontSize =
      image.width >= image.height
        ? a4Landscape
        : a4Portrait;

    const frontPage = output.addPage([
      frontSize.width,
      frontSize.height,
    ]);

    const margin = 24;
    const scaled = image.scaleToFit(
      frontSize.width - margin * 2,
      frontSize.height - margin * 2,
    );

    frontPage.drawImage(image, {
      x: (frontSize.width - scaled.width) / 2,
      y: (frontSize.height - scaled.height) / 2,
      width: scaled.width,
      height: scaled.height,
    });
  }

  await addBacksidePage(
    output,
    frontSize,
    details,
  );

  // The metadata is deliberately page 2 so duplex printing places it on
  // the reverse of the first drawing sheet. Only the first source page is
  // retained so the exported file is always exactly a front/back pair.
  output.setTitle(
    cleanPdfText(
      `${details.materialNo || 'Material'} drawing - front and backside`,
    ),
  );
  output.setSubject('Drawing with material identification backside');
  output.setCreator('PO Tracker');

  return output.save();
}

async function addBacksidePage(
  document: PDFDocument,
  size: {
    readonly width: number;
    readonly height: number;
  },
  details: DrawingBacksideDetails,
): Promise<void> {
  const page = document.addPage([
    size.width,
    size.height,
  ]);
  const regularFont = await document.embedFont(
    StandardFonts.Helvetica,
  );
  const boldFont = await document.embedFont(
    StandardFonts.HelveticaBold,
  );
  const minimumSide = Math.min(size.width, size.height);
  const margin = Math.min(
    42,
    Math.max(24, minimumSide * 0.045),
  );
  const gap = Math.min(
    8,
    Math.max(5, minimumSide * 0.01),
  );
  const headerHeight = Math.min(
    36,
    Math.max(28, size.height * 0.045),
  );
  const contentWidth = size.width - margin * 2;
  const borderColor = rgb(0.80, 0.84, 0.89);
  const headerColor = rgb(0.12, 0.22, 0.34);
  const labelColor = rgb(0.34, 0.43, 0.54);
  const valueColor = rgb(0.06, 0.1, 0.16);
  const panelColor = rgb(0.975, 0.982, 0.99);

  const headerY = size.height - margin - headerHeight;

  page.drawRectangle({
    x: margin,
    y: headerY,
    width: contentWidth,
    height: headerHeight,
    color: headerColor,
  });

  page.drawText('DRAWING BACKSIDE', {
    x: margin + 18,
    y: headerY + headerHeight / 2 - 7,
    size: Math.min(18, Math.max(13, minimumSide * 0.027)),
    font: boldFont,
    color: rgb(1, 1, 1),
  });

  let top = headerY - gap;
  const fields = [
    ['1. MATERIAL NO.', details.materialNo],
    ['2. SHORT DESCRIPTION', details.shortDescription ?? ''],
    ['DRAWING CODE', details.drawingCode ?? ''],
    ['4. GRADE', details.grade],
    ['5. FINISH SIZE', details.finishSize ?? ''],
    ['6. REQUIRED RM SIZE', details.rmSize ?? ''],
    ['7. REQUIRED QTY', formatQuantity(details.requiredQty)],
    ['8. PROCESS QTY', formatQuantity(details.processQty)],
    ['9. STOCK QTY', ''],
  ] as const;

  const rows = [
    fields.slice(0, 2),
    fields.slice(2, 4),
    fields.slice(4, 6),
    fields.slice(6, 9),
  ];
  for (const row of rows) {
    const width = (contentWidth - gap * (row.length + 1)) / row.length;
    const rowHeight = Math.max(...row.map(field => fieldHeight(field[1], regularFont, width)));
    row.forEach((field, column) => {
      drawField(page, regularFont, boldFont, {
        label: field[0], value: field[1],
        x: margin + gap + column * (width + gap),
        top, width, height: rowHeight, panelColor,
        borderColor, labelColor, valueColor, gapAfter: 0,
      });
    });
    top -= rowHeight + gap;
  }

  // Keep each vendor and its handwritten checks on the same process row.
  const steps = details.processSteps?.length ? details.processSteps : [{
    processName: details.rmProcess || '',
    vendorName: details.vendor || details.rmSource || '',
  }];
  const tableX = margin + gap;
  const tableWidth = contentWidth - gap * 2;
  const columns = [
    { label: 'PROCESS', fraction: 0.27 },
    { label: 'VENDOR', fraction: 0.19 },
    { label: 'QTY', fraction: 0.09 },
    { label: 'CHECK', fraction: 0.10 },
    { label: 'REMARK', fraction: 0.20 },
    { label: 'SIGN', fraction: 0.15 },
  ];
  let columnX = tableX;
  for (const column of columns) {
    const width = tableWidth * column.fraction;
    page.drawRectangle({ x: columnX, y: top - 23, width, height: 23, color: panelColor, borderColor, borderWidth: 0.55 });
    page.drawText(column.label, { x: columnX + 6, y: top - 15, size: 8, font: boldFont, color: labelColor });
    columnX += width;
  }
  top -= 23;
  for (const [index, step] of steps.entries()) {
    const values = [step.processName ? `${index + 1}. ${step.processName}` : '', step.vendorName || ''];
    const lines = values.map((value, column) => wrapText(cleanPdfText(value), regularFont, 10, tableWidth * columns[column].fraction - 12));
    const rowHeight = Math.max(42, 16 + Math.max(...lines.map(value => value.length)) * 13);
    columnX = tableX;
    columns.forEach((column, columnIndex) => {
      const width = tableWidth * column.fraction;
      page.drawRectangle({ x: columnX, y: top - rowHeight, width, height: rowHeight, borderColor, borderWidth: 0.55 });
      (lines[columnIndex] || []).forEach((line, lineIndex) => {
        page.drawText(line, { x: columnX + 6, y: top - 17 - lineIndex * 13, size: 10, font: regularFont, color: valueColor });
      });
      if (columnIndex === 3) {
        page.drawRectangle({ x: columnX + 8, y: top - 22, width: 9, height: 9, borderColor: labelColor, borderWidth: 0.7 });
      }
      if (columnIndex >= 2) {
        page.drawLine({ start: { x: columnX + 7, y: top - rowHeight + 10 }, end: { x: columnX + width - 7, y: top - rowHeight + 10 }, color: borderColor, thickness: 0.5 });
      }
      columnX += width;
    });
    top -= rowHeight;
  }
  top -= gap;

  const descriptionHeight = fieldHeight(details.fullDescription, regularFont, contentWidth - gap * 2);
  if (top - descriptionHeight - gap < margin) {
    throw new Error('The process checklist and description do not fit on one drawing backside. Use a larger drawing page or shorten the process names or description.');
  }
  drawField(page, regularFont, boldFont, {
    label: '12. FULL DESCRIPTION', value: details.fullDescription,
    x: margin + gap, top,
    width: contentWidth - gap * 2,
    height: descriptionHeight,
    panelColor, borderColor, labelColor, valueColor, gapAfter: 0, truncate: false,
  });

  page.drawRectangle({
    x: margin, y: top - descriptionHeight - gap, width: contentWidth,
    height: size.height - margin - top + descriptionHeight + gap,
    borderColor, borderWidth: 0.6,
  });
}

function fieldHeight(value: string, font: PDFFont, width: number): number {
  return 22 + Math.max(1, wrapText(cleanPdfText(value), font, 10, width - 16).length) * 13;
}

function formatQuantity(value: number | undefined): string {
  return typeof value === 'number' && Number.isFinite(value)
    ? String(value)
    : '';
}

interface FieldOptions {
  readonly label: string;
  readonly value: string;
  readonly x: number;
  readonly top: number;
  readonly width: number;
  readonly height: number;
  readonly panelColor: ReturnType<typeof rgb>;
  readonly borderColor: ReturnType<typeof rgb>;
  readonly labelColor: ReturnType<typeof rgb>;
  readonly valueColor: ReturnType<typeof rgb>;
  readonly gapAfter: number;
  readonly truncate?: boolean;
}

function drawField(
  page: PDFPage,
  regularFont: PDFFont,
  boldFont: PDFFont,
  options: FieldOptions,
): number {
  const bottom = options.top - options.height;
  const innerX = options.x + 8;
  const innerWidth = options.width - 16;
  const labelSize = 8;

  page.drawRectangle({
    x: options.x,
    y: bottom,
    width: options.width,
    height: options.height,
    color: options.panelColor,
    borderColor: options.borderColor,
    borderWidth: 0.55,
  });

  page.drawText(options.label.replace(/^\d+\. /, ''), {
    x: innerX,
    y: options.top - 12,
    size: labelSize,
    font: boldFont,
    color: options.labelColor,
  });

  const availableHeight = options.height - 22;
  const normalizedValue = cleanPdfText(options.value);
  let valueSize = 10;
  let lineHeight = valueSize * 1.3;
  let lines = wrapText(
    normalizedValue,
    regularFont,
    valueSize,
    innerWidth,
  );

  while (
    valueSize > ((options.truncate ?? true) ? 8 : 5) &&
    lines.length * lineHeight > availableHeight
  ) {
    valueSize -= 0.5;
    lineHeight = valueSize * 1.3;
    lines = wrapText(
      normalizedValue,
      regularFont,
      valueSize,
      innerWidth,
    );
  }

  const maximumLines = Math.max(
    1,
    Math.floor(availableHeight / lineHeight),
  );

  if ((options.truncate ?? true) && lines.length > maximumLines) {
    lines = lines.slice(0, maximumLines);
    lines[maximumLines - 1] = fitEllipsis(
      lines[maximumLines - 1],
      regularFont,
      valueSize,
      innerWidth,
    );
  }

  let textY = options.top - 18 - valueSize;

  for (const line of lines) {
    page.drawText(line, {
      x: innerX,
      y: textY,
      size: valueSize,
      font: regularFont,
      color: options.valueColor,
    });
    textY -= lineHeight;
  }

  return bottom - options.gapAfter;
}

function wrapText(
  value: string,
  font: PDFFont,
  fontSize: number,
  maximumWidth: number,
): string[] {
  if (!value) {
    return [];
  }

  const words = value.split(/(\r?\n)|\s+/).filter(Boolean);
  const lines: string[] = [];
  let current = '';

  for (const word of words) {
    if (/^\r?\n$/.test(word)) {
      if (current) {
        lines.push(current);
        current = '';
      }
      continue;
    }
    const candidate = current
      ? `${current} ${word}`
      : word;

    if (
      font.widthOfTextAtSize(candidate, fontSize) <=
      maximumWidth
    ) {
      current = candidate;
      continue;
    }

    if (current) {
      lines.push(current);
    }

    if (
      font.widthOfTextAtSize(word, fontSize) <=
      maximumWidth
    ) {
      current = word;
      continue;
    }

    let fragment = '';

    for (const character of word) {
      const nextFragment = fragment + character;

      if (
        fragment &&
        font.widthOfTextAtSize(
          nextFragment,
          fontSize,
        ) > maximumWidth
      ) {
        lines.push(fragment);
        fragment = character;
      } else {
        fragment = nextFragment;
      }
    }

    current = fragment;
  }

  if (current) {
    lines.push(current);
  }

  return lines;
}

function fitEllipsis(
  value: string,
  font: PDFFont,
  fontSize: number,
  maximumWidth: number,
): string {
  let candidate = `${value}...`;

  while (
    candidate.length > 3 &&
    font.widthOfTextAtSize(candidate, fontSize) >
      maximumWidth
  ) {
    candidate = `${candidate.slice(0, -4).trimEnd()}...`;
  }

  return candidate;
}

function cleanPdfText(value: string): string {
  return (value ?? '')
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[\u2010-\u2015\u2212]/g, '-')
    .replace(/[\u00D7]/g, 'X')
    .replace(/[^\x20-\x7E\r\n\t]/g, ' ')
    .replace(/[ \t]+/g, ' ')
    .replace(/ *\r?\n */g, '\n')
    .trim();
}
