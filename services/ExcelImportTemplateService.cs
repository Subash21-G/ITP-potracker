using ClosedXML.Excel;

namespace POTracker.Services
{
    public static class ExcelImportTemplateService
    {
        public static byte[] CreateMaterialImportTemplate(
            int maximumRowsPerImport)
        {
            if (maximumRowsPerImport <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumRowsPerImport));
            }

            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add(
                    "Materials");

            var headers = new[]
            {
                "PurchaserName",
                "ContactPerson",
                "PONumber",
                "PODate",
                "ReceivedDate",
                "BuyerContactPerson",
                "LineItem",
                "MaterialNo",
                "HsnSacCode",
                "ShortDescription",
                "BriefDescription",
                "Qty",
                "Unit",
                "Dep",
                "ValueOfGoods",
                "PriceUnit",
                "TotalValue",
                "DeliveryDate",
                "DrawingNo",
                "AssignedDashboard",
                "Remark"
            };

            for (var column = 0;
                 column < headers.Length;
                 column++)
            {
                worksheet.Cell(
                    1,
                    column + 1).Value =
                    headers[column];
            }

            // Example row
            worksheet.Cell(2, 1).Value =
                "WEG Industries (India) PVT LTD";

            worksheet.Cell(2, 2).Value =
                "Gokulnath V";

            worksheet.Cell(2, 3).Value =
                "4519000001";

            worksheet.Cell(2, 4).Value =
                DateTime.Today;

            worksheet.Cell(2, 5).Value =
                DateTime.Today;

            worksheet.Cell(2, 6).Value =
                "Gokulnath V";

            worksheet.Cell(2, 7).Value =
                "N-10";

            worksheet.Cell(2, 8).Value =
                "15464033";

            worksheet.Cell(2, 9).Value =
                "8503.00.90";

            worksheet.Cell(2, 10).Value =
                "CABLE ENTRY PLATE";

            worksheet.Cell(2, 11).Value =
                "CABLE ENTRY PLATE SAE1010/20";

            worksheet.Cell(2, 12).Value = 2;
            worksheet.Cell(2, 13).Value = "UN";
            worksheet.Cell(2, 14).Value = "RS02";
            worksheet.Cell(2, 15).Value = 1531;
            worksheet.Cell(2, 16).Value = "UN";
            worksheet.Cell(2, 17).Value = 3062;

            worksheet.Cell(2, 18).Value =
                DateTime.Today.AddDays(30);

            worksheet.Cell(2, 19).Value =
                "SWD 10004012815 000 01";

            worksheet.Cell(2, 20).Value =
                "Gokulnath";

            worksheet.Cell(2, 21).Value =
                "Sample row - delete before upload";

            var headerRange =
                worksheet.Range(
                    1,
                    1,
                    1,
                    headers.Length);

            headerRange.Style.Font.Bold = true;

            headerRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#1F4FD7");

            headerRange.Style.Font.FontColor =
                XLColor.White;

            headerRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            headerRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            worksheet.SheetView.FreezeRows(1);

            worksheet.RangeUsed()?
                .SetAutoFilter();

            worksheet.Columns()
                .AdjustToContents(12, 32);

            worksheet.Column(11).Width = 45;
            worksheet.Column(21).Width = 35;

            worksheet.Column(4)
                .Style.DateFormat.Format =
                "dd-MM-yyyy";

            worksheet.Column(5)
                .Style.DateFormat.Format =
                "dd-MM-yyyy";

            worksheet.Column(18)
                .Style.DateFormat.Format =
                "dd-MM-yyyy";

            worksheet.Column(12)
                .Style.NumberFormat.Format =
                "0.00";

            worksheet.Column(15)
                .Style.NumberFormat.Format =
                "0.00";

            worksheet.Column(17)
                .Style.NumberFormat.Format =
                "0.00";

            worksheet.Range(
                    2,
                    1,
                    maximumRowsPerImport + 1,
                    headers.Length)
                .Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Top;

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            return stream.ToArray();
        }
    }
}