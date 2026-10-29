using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace POTracker.Controllers.Api;

public sealed partial class InvoicesApiController
{
    [HttpGet("pending/export-excel")]
    public async Task<IActionResult> ExportPendingExcel(
        string? search,
        string? sortColumn,
        string? sortDirection,
        string? excelFilters,
        CancellationToken cancellationToken)
    {
        var query = ApplyPendingInvoiceExcelFilters(
            ApplyPendingInvoiceSearch(
                _context.PendingInvoiceItems.AsNoTracking()
                    .Where(item => item.Status == "Pending"),
                search),
            excelFilters);

        var items = await ApplyPendingInvoiceSort(query, sortColumn, sortDirection)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Pending Invoice Rows");
        string[] headers =
        [
            "Invoice Number", "Invoice Date", "Customer Name", "PO Number",
            "Line Item", "Material Number", "Description", "HSN/SAC",
            "Quantity", "Price Unit", "GST", "Amount", "Mismatch Reason",
            "Imported Date"
        ];

        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];

        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4FD7");
        sheet.SheetView.FreezeRows(1);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var row = index + 2;
            sheet.Cell(row, 1).Value = item.InvoiceNumber;
            if (item.InvoiceDate.HasValue)
                sheet.Cell(row, 2).Value = item.InvoiceDate.Value;
            sheet.Cell(row, 3).Value = item.CustomerName;
            sheet.Cell(row, 4).Value = item.PONumber;
            sheet.Cell(row, 5).Value = NormalizeLineItem(item.LineItem);
            sheet.Cell(row, 6).Value = item.MaterialNo;
            sheet.Cell(row, 7).Value = item.Description;
            sheet.Cell(row, 8).Value = item.HsnSacCode;
            sheet.Cell(row, 9).Value = item.InvoiceQty;
            sheet.Cell(row, 10).Value = item.PriceUnit;
            sheet.Cell(row, 11).Value = item.GstAmount;
            sheet.Cell(row, 12).Value = item.Amount;
            sheet.Cell(row, 13).Value = item.ErrorMessage;
            sheet.Cell(row, 14).Value = item.CreatedDate;
        }

        sheet.Column(2).Style.DateFormat.Format = "dd mmm yyyy";
        sheet.Column(14).Style.DateFormat.Format = "dd mmm yyyy, hh:mm";
        sheet.Columns(9, 12).Style.NumberFormat.Format = "#,##0.00";
        sheet.Columns().AdjustToContents(1, Math.Min(items.Count + 1, 100));
        sheet.Column(13).Width = Math.Min(sheet.Column(13).Width, 60);
        sheet.RangeUsed()?.SetAutoFilter();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Pending_Invoice_Rows_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
    }
}
