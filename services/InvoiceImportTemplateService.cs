using ClosedXML.Excel;

namespace POTracker.Services
{
    public static class InvoiceImportTemplateService
    {
        public static byte[] CreateInvoiceImportTemplate()
        {
            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add(
                    "Invoice Import");

            const int headerRow = 6;
            const int maximumDataRows = 500;

            var headers = new[]
            {
                "PO/DC Date",
                "PO/DC NO",
                "ITEM NO",
                "MATERIAL",
                "DESCRIPTION",
                "HSN/SAC",
                "QTY",
                "Price/Unit",
                "GST",
                "Amount"
            };

            worksheet.Range("A1:J1").Merge();

            worksheet.Cell("A1").Value =
                "POTracker Invoice Excel Import Template";

            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 16;

            worksheet.Cell("A1").Style.Font.FontColor =
                XLColor.White;

            worksheet.Cell("A1").Style.Fill.BackgroundColor =
                XLColor.FromHtml("#1F4FD7");

            worksheet.Cell("A1").Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Cell("A1").Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            worksheet.Row(1).Height = 30;

            worksheet.Cell("A2").Value = "Invoice No";
            worksheet.Cell("A3").Value = "Date";
            worksheet.Cell("A4").Value = "Bill To";

            worksheet.Range("B2:F2").Merge();
            worksheet.Range("B3:F3").Merge();
            worksheet.Range("B4:F4").Merge();

            var invoiceLabelRange =
                worksheet.Range("A2:A4");

            invoiceLabelRange.Style.Font.Bold = true;

            invoiceLabelRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#E8EEFF");

            invoiceLabelRange.Style.Font.FontColor =
                XLColor.FromHtml("#1F3A8A");

            invoiceLabelRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            var invoiceValueRange =
                worksheet.Range("B2:F4");

            invoiceValueRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#FFFDF5");

            invoiceValueRange.Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            invoiceValueRange.Style.Border.InsideBorder =
                XLBorderStyleValues.Thin;

            invoiceValueRange.Style.Border.OutsideBorderColor =
                XLColor.FromHtml("#D8DEE9");

            invoiceValueRange.Style.Border.InsideBorderColor =
                XLColor.FromHtml("#D8DEE9");

            worksheet.Cell("B3")
                .Style.DateFormat.Format =
                "dd-MM-yyyy";

            for (var columnIndex = 0;
                 columnIndex < headers.Length;
                 columnIndex++)
            {
                worksheet.Cell(
                    headerRow,
                    columnIndex + 1).Value =
                    headers[columnIndex];
            }

            var tableHeaderRange =
                worksheet.Range(
                    headerRow,
                    1,
                    headerRow,
                    headers.Length);

            tableHeaderRange.Style.Font.Bold = true;

            tableHeaderRange.Style.Font.FontColor =
                XLColor.White;

            tableHeaderRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#3346A8");

            tableHeaderRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            tableHeaderRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            tableHeaderRange.Style.Alignment.WrapText = true;

            tableHeaderRange.Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            tableHeaderRange.Style.Border.InsideBorder =
                XLBorderStyleValues.Thin;

            tableHeaderRange.Style.Border.OutsideBorderColor =
                XLColor.White;

            tableHeaderRange.Style.Border.InsideBorderColor =
                XLColor.White;

            worksheet.Row(headerRow).Height = 28;

            var requiredColumns = new[]
            {
                2,
                4,
                7
            };

            foreach (var columnNumber in requiredColumns)
            {
                worksheet.Cell(
                        headerRow,
                        columnNumber)
                    .Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#B42318");
            }

            var firstDataRow =
                headerRow + 1;

            var lastDataRow =
                headerRow + maximumDataRows;

            var dataRange =
                worksheet.Range(
                    firstDataRow,
                    1,
                    lastDataRow,
                    headers.Length);

            dataRange.Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            dataRange.Style.Border.InsideBorder =
                XLBorderStyleValues.Hair;

            dataRange.Style.Border.OutsideBorderColor =
                XLColor.FromHtml("#CBD5E1");

            dataRange.Style.Border.InsideBorderColor =
                XLColor.FromHtml("#E5E7EB");

            dataRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Top;

            worksheet.Column(1)
                .Style.DateFormat.Format =
                "dd-MM-yyyy";

            worksheet.Column(2)
                .Style.NumberFormat.Format = "@";

            worksheet.Column(3)
                .Style.NumberFormat.Format = "@";

            worksheet.Column(4)
                .Style.NumberFormat.Format = "@";

            worksheet.Column(6)
                .Style.NumberFormat.Format = "@";

            worksheet.Column(7)
                .Style.NumberFormat.Format =
                "0.00";

            worksheet.Column(8)
                .Style.NumberFormat.Format =
                "#,##0.00";

            worksheet.Column(9)
                .Style.NumberFormat.Format =
                "#,##0.00";

            worksheet.Column(10)
                .Style.NumberFormat.Format =
                "#,##0.00";

            worksheet.Column(1).Width = 15;
            worksheet.Column(2).Width = 20;
            worksheet.Column(3).Width = 13;
            worksheet.Column(4).Width = 18;
            worksheet.Column(5).Width = 38;
            worksheet.Column(6).Width = 16;
            worksheet.Column(7).Width = 12;
            worksheet.Column(8).Width = 15;
            worksheet.Column(9).Width = 14;
            worksheet.Column(10).Width = 17;

            worksheet.Column(5)
                .Style.Alignment.WrapText = true;

            worksheet.SheetView.FreezeRows(headerRow);

            worksheet.Range(
                    headerRow,
                    1,
                    lastDataRow,
                    headers.Length)
                .SetAutoFilter();

            var instructions =
                workbook.Worksheets.Add(
                    "Instructions");

            instructions.Range("A1:D1").Merge();

            instructions.Cell("A1").Value =
                "Invoice Excel Import Instructions";

            instructions.Cell("A1").Style.Font.Bold = true;
            instructions.Cell("A1").Style.Font.FontSize = 16;

            instructions.Cell("A1").Style.Font.FontColor =
                XLColor.White;

            instructions.Cell("A1").Style.Fill.BackgroundColor =
                XLColor.FromHtml("#1F4FD7");

            instructions.Cell("A1").Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            instructions.Cell("A3").Value = "Field";
            instructions.Cell("B3").Value = "Required";
            instructions.Cell("C3").Value = "Example";
            instructions.Cell("D3").Value = "Description";

            var instructionHeaderRange =
                instructions.Range("A3:D3");

            instructionHeaderRange.Style.Font.Bold = true;

            instructionHeaderRange.Style.Font.FontColor =
                XLColor.White;

            instructionHeaderRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#3346A8");

            var instructionRows = new[]
            {
                new[]
                {
                    "Invoice No",
                    "Yes",
                    "INV-2026-001",
                    "Enter a unique invoice number."
                },
                new[]
                {
                    "Date",
                    "Yes",
                    "15-07-2026",
                    "Enter the invoice date."
                },
                new[]
                {
                    "Bill To",
                    "No",
                    "WEG Industries (India) PVT LTD",
                    "Customer or billing company name."
                },
                new[]
                {
                    "PO/DC Date",
                    "No",
                    "02-05-2026",
                    "Purchase-order or delivery-challan date."
                },
                new[]
                {
                    "PO/DC NO",
                    "Yes",
                    "4519000001",
                    "PO number used for matching the material."
                },
                new[]
                {
                    "ITEM NO",
                    "Recommended",
                    "N-10",
                    "10, N10 and N-10 formats are supported."
                },
                new[]
                {
                    "MATERIAL",
                    "Yes",
                    "15464033",
                    "Material number used for matching."
                },
                new[]
                {
                    "DESCRIPTION",
                    "No",
                    "Split Clamp",
                    "Material description."
                },
                new[]
                {
                    "HSN/SAC",
                    "No",
                    "73269099",
                    "HSN or SAC code."
                },
                new[]
                {
                    "QTY",
                    "Yes",
                    "2",
                    "Invoice quantity must be greater than zero."
                },
                new[]
                {
                    "Price/Unit",
                    "No",
                    "1531.00",
                    "Invoice price or rate per unit."
                },
                new[]
                {
                    "GST",
                    "No",
                    "551.16",
                    "GST amount for the material row."
                },
                new[]
                {
                    "Amount",
                    "No",
                    "3062.00",
                    "Total value of the material row."
                }
            };

            for (var rowIndex = 0;
                 rowIndex < instructionRows.Length;
                 rowIndex++)
            {
                var excelRow =
                    rowIndex + 4;

                for (var columnIndex = 0;
                     columnIndex <
                     instructionRows[rowIndex].Length;
                     columnIndex++)
                {
                    instructions.Cell(
                        excelRow,
                        columnIndex + 1).Value =
                        instructionRows[rowIndex][columnIndex];
                }
            }

            instructions.Columns(1, 4)
                .AdjustToContents();

            instructions.Column(1).Width = 22;
            instructions.Column(2).Width = 16;
            instructions.Column(3).Width = 28;
            instructions.Column(4).Width = 48;

            instructions.Column(4)
                .Style.Alignment.WrapText = true;

            var usedInstructionRange =
                instructions.RangeUsed();

            if (usedInstructionRange != null)
            {
                usedInstructionRange.Style.Border.OutsideBorder =
                    XLBorderStyleValues.Thin;

                usedInstructionRange.Style.Border.InsideBorder =
                    XLBorderStyleValues.Thin;

                usedInstructionRange
                    .Style.Border.OutsideBorderColor =
                    XLColor.FromHtml("#D8DEE9");

                usedInstructionRange
                    .Style.Border.InsideBorderColor =
                    XLColor.FromHtml("#E5E7EB");
            }

            worksheet.Position = 1;

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            return stream.ToArray();
        }
    }
}