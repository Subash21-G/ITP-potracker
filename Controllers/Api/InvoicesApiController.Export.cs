using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    public sealed partial class InvoicesApiController
    {
        [HttpGet("export-excel")]
        public async Task<IActionResult> ExportExcel()
        {
            var invoices = await _context.Invoices
                .AsNoTracking()
                .OrderByDescending(x => x.InvoiceDate)
                .ThenByDescending(x => x.CreatedDate)
                .ToListAsync();

            if (invoices.Count == 0)
            {
                return NotFound(new
                {
                    message =
                        "No invoices are available to export."
                });
            }

            var invoiceItems = await _context.InvoiceItems
                .AsNoTracking()
                .OrderBy(x => x.InvoiceId)
                .ThenBy(x => x.PONumber)
                .ThenBy(x => x.LineItem)
                .ThenBy(x => x.MaterialNo)
                .ToListAsync();

            var pendingItems =
                await _context.PendingInvoiceItems
                    .AsNoTracking()
                    .Where(x =>
                        x.Status == "Pending")
                    .OrderBy(x =>
                        x.InvoiceId)
                    .ThenBy(x =>
                        x.PONumber)
                    .ThenBy(x =>
                        x.LineItem)
                    .ThenBy(x =>
                        x.MaterialNo)
                    .ToListAsync();

            var materialIds = invoiceItems
                .Select(x => x.MaterialId)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            var materials = materialIds.Count == 0
                ? new List<Material>()
                : await _context.Materials
                    .AsNoTracking()
                    .Where(x => materialIds.Contains(x.Id))
                    .ToListAsync();

            var invoiceMap = invoices
                .ToDictionary(x => x.Id);

            var materialMap = materials
                .ToDictionary(x => x.Id);

            var matchedByInvoice = invoiceItems
                .GroupBy(x => x.InvoiceId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var pendingByInvoice = pendingItems
                .GroupBy(x => x.InvoiceId)
                .ToDictionary(x => x.Key, x => x.ToList());

            using var workbook = new XLWorkbook();

            // ========================================================
            // SHEET 1: ALL INVOICE MATERIAL ROWS
            // ========================================================

            var mergedSheet =
                workbook.Worksheets.Add("Merged Invoice");

            var mergedHeaders = new[]
            {
                "Sl.No",
                "Invoice Number",
                "Invoice Date",
                "Customer Name",
                "PO Number",
                "Line Item",
                "Material Number",
                "Description",
                "HSN/SAC",
                "Invoice Qty",
                "Price/Unit",
                "GST Amount",
                "Amount",
                "Record Status",
                "PO Qty",
                "Total Invoiced Qty",
                "Invoice Balance Qty",
                "Dispatch Qty",
                "Dispatch Pending Qty",
                "Invoice Status",
                "Dispatch Status",
                "Delivery Date",
                "Uploaded File",
                "Imported Date",
                "Notes"
            };

            for (var columnIndex = 0;
                 columnIndex < mergedHeaders.Length;
                 columnIndex++)
            {
                mergedSheet.Cell(
                    1,
                    columnIndex + 1).Value =
                        mergedHeaders[columnIndex];
            }

            var mergedHeaderRange = mergedSheet.Range(
                1,
                1,
                1,
                mergedHeaders.Length);

            mergedHeaderRange.Style.Font.Bold = true;
            mergedHeaderRange.Style.Font.FontColor = XLColor.White;
            mergedHeaderRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#1F4FD7");
            mergedHeaderRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            mergedHeaderRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;
            mergedHeaderRange.Style.Alignment.WrapText = true;

            mergedSheet.Row(1).Height = 36;

            var mergedRow = 2;
            var serialNumber = 1;

            foreach (var item in invoiceItems)
            {
                invoiceMap.TryGetValue(
                    item.InvoiceId,
                    out var invoice);

                materialMap.TryGetValue(
                    item.MaterialId,
                    out var material);

                mergedSheet.Cell(mergedRow, 1).Value =
                    serialNumber++;

                mergedSheet.Cell(mergedRow, 2).Value =
                    invoice?.InvoiceNumber ?? "";

                if (invoice?.InvoiceDate != null)
                {
                    mergedSheet.Cell(mergedRow, 3).Value =
                        invoice.InvoiceDate.Value;

                    mergedSheet.Cell(mergedRow, 3)
                        .Style.DateFormat.Format =
                            "dd-MMM-yyyy";
                }

                mergedSheet.Cell(mergedRow, 4).Value =
                    invoice?.CustomerName ?? "";

                mergedSheet.Cell(mergedRow, 5).Value =
                    item.PONumber ?? "";

                mergedSheet.Cell(mergedRow, 6).Value =
                    item.LineItem ?? "";

                mergedSheet.Cell(mergedRow, 7).Value =
                    item.MaterialNo ?? "";

                mergedSheet.Cell(mergedRow, 8).Value =
                    item.Description ?? "";

                mergedSheet.Cell(mergedRow, 9).Value =
                    item.HsnSacCode ?? "";

                mergedSheet.Cell(mergedRow, 10).Value =
                    item.InvoiceQty;

                mergedSheet.Cell(mergedRow, 11).Value =
                    item.PriceUnit;

                mergedSheet.Cell(mergedRow, 12).Value =
                    item.GstAmount;

                mergedSheet.Cell(mergedRow, 13).Value =
                    item.Amount;

                mergedSheet.Cell(mergedRow, 14).Value =
                    "Matched";

                mergedSheet.Cell(mergedRow, 15).Value =
                    material?.Qty ?? 0;

                mergedSheet.Cell(mergedRow, 16).Value =
                    material?.InvoiceQty ?? 0;

                mergedSheet.Cell(mergedRow, 17).Value =
                    material?.BalanceQty ?? 0;

                mergedSheet.Cell(mergedRow, 18).Value =
                    material?.DispatchQty ?? 0;

                mergedSheet.Cell(mergedRow, 19).Value =
                    material?.PendingQty ?? 0;

                mergedSheet.Cell(mergedRow, 20).Value =
                    material?.InvoiceStatus ?? "";

                mergedSheet.Cell(mergedRow, 21).Value =
                    material?.DispatchStatus ?? "";

                if (material?.DeliveryDate != null)
                {
                    mergedSheet.Cell(mergedRow, 22).Value =
                        material.DeliveryDate.Value;

                    mergedSheet.Cell(mergedRow, 22)
                        .Style.DateFormat.Format =
                            "dd-MMM-yyyy";
                }

                mergedSheet.Cell(mergedRow, 23).Value =
                    invoice?.UploadedFileName ?? "";

                if (invoice != null)
                {
                    mergedSheet.Cell(mergedRow, 24).Value =
                        invoice.CreatedDate;

                    mergedSheet.Cell(mergedRow, 24)
                        .Style.DateFormat.Format =
                            "dd-MMM-yyyy HH:mm";
                }

                mergedSheet.Cell(mergedRow, 25).Value =
                    "PO and material matched successfully.";

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Fill.BackgroundColor =
                        XLColor.FromHtml("#DCFCE7");

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Font.FontColor =
                        XLColor.FromHtml("#166534");

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Font.Bold = true;

                mergedRow++;
            }

            foreach (var item in pendingItems)
            {
                invoiceMap.TryGetValue(
                    item.InvoiceId,
                    out var invoice);

                mergedSheet.Cell(mergedRow, 1).Value =
                    serialNumber++;

                mergedSheet.Cell(mergedRow, 2).Value =
                    !string.IsNullOrWhiteSpace(item.InvoiceNumber)
                        ? item.InvoiceNumber
                        : invoice?.InvoiceNumber ?? "";

                var invoiceDate =
                    item.InvoiceDate ??
                    invoice?.InvoiceDate;

                if (invoiceDate.HasValue)
                {
                    mergedSheet.Cell(mergedRow, 3).Value =
                        invoiceDate.Value;

                    mergedSheet.Cell(mergedRow, 3)
                        .Style.DateFormat.Format =
                            "dd-MMM-yyyy";
                }

                mergedSheet.Cell(mergedRow, 4).Value =
                    !string.IsNullOrWhiteSpace(item.CustomerName)
                        ? item.CustomerName
                        : invoice?.CustomerName ?? "";

                mergedSheet.Cell(mergedRow, 5).Value =
                    item.PONumber ?? "";

                mergedSheet.Cell(mergedRow, 6).Value =
                    item.LineItem ?? "";

                mergedSheet.Cell(mergedRow, 7).Value =
                    item.MaterialNo ?? "";

                mergedSheet.Cell(mergedRow, 8).Value =
                    item.Description ?? "";

                mergedSheet.Cell(mergedRow, 9).Value =
                    item.HsnSacCode ?? "";

                mergedSheet.Cell(mergedRow, 10).Value =
                    item.InvoiceQty;

                mergedSheet.Cell(mergedRow, 11).Value =
                    item.PriceUnit;

                mergedSheet.Cell(mergedRow, 12).Value =
                    item.GstAmount;

                mergedSheet.Cell(mergedRow, 13).Value =
                    item.Amount;

                mergedSheet.Cell(mergedRow, 14).Value =
                    item.Status ?? "Pending";

                mergedSheet.Cell(mergedRow, 23).Value =
                    invoice?.UploadedFileName ?? "";

                mergedSheet.Cell(mergedRow, 24).Value =
                    item.CreatedDate;

                mergedSheet.Cell(mergedRow, 24)
                    .Style.DateFormat.Format =
                        "dd-MMM-yyyy HH:mm";

                mergedSheet.Cell(mergedRow, 25).Value =
                    item.ErrorMessage ??
                    "PO or material is not matched.";

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Fill.BackgroundColor =
                        XLColor.FromHtml("#FEF3C7");

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Font.FontColor =
                        XLColor.FromHtml("#92400E");

                mergedSheet.Cell(mergedRow, 14)
                    .Style.Font.Bold = true;

                mergedRow++;
            }

            if (mergedRow == 2)
            {
                mergedSheet.Cell(2, 1).Value =
                    "No invoice material rows found.";
            }
            else
            {
                mergedSheet.Range(
                        2,
                        10,
                        mergedRow - 1,
                        19)
                    .Style.NumberFormat.Format =
                        "#,##0.00";
            }

            mergedSheet.SheetView.FreezeRows(1);

            if (mergedRow > 2)
            {
                mergedSheet.Range(
                        1,
                        1,
                        mergedRow - 1,
                        mergedHeaders.Length)
                    .SetAutoFilter();
            }

            mergedSheet.Column(1).Width = 8;
            mergedSheet.Column(2).Width = 22;
            mergedSheet.Column(3).Width = 16;
            mergedSheet.Column(4).Width = 30;
            mergedSheet.Column(5).Width = 18;
            mergedSheet.Column(6).Width = 13;
            mergedSheet.Column(7).Width = 18;
            mergedSheet.Column(8).Width = 40;
            mergedSheet.Column(9).Width = 15;
            mergedSheet.Columns(10, 19).Width = 17;
            mergedSheet.Columns(20, 21).Width = 20;
            mergedSheet.Column(22).Width = 16;
            mergedSheet.Column(23).Width = 30;
            mergedSheet.Column(24).Width = 21;
            mergedSheet.Column(25).Width = 55;

            mergedSheet.Column(8)
                .Style.Alignment.WrapText = true;

            mergedSheet.Column(25)
                .Style.Alignment.WrapText = true;

            // ========================================================
            // SHEET 2: INVOICE-WISE SUMMARY
            // ========================================================

            var summarySheet =
                workbook.Worksheets.Add("Invoice Summary");

            var summaryHeaders = new[]
            {
                "Sl.No",
                "Invoice Number",
                "Invoice Date",
                "Customer Name",
                "Matched Rows",
                "Pending Rows",
                "Total Rows",
                "Matched Qty",
                "Pending Qty",
                "Total Qty",
                "Matched GST",
                "Pending GST",
                "Total GST",
                "Matched Amount",
                "Pending Amount",
                "Total Amount",
                "Uploaded File",
                "Imported Date"
            };

            for (var columnIndex = 0;
                 columnIndex < summaryHeaders.Length;
                 columnIndex++)
            {
                summarySheet.Cell(
                    1,
                    columnIndex + 1).Value =
                        summaryHeaders[columnIndex];
            }

            var summaryHeaderRange = summarySheet.Range(
                1,
                1,
                1,
                summaryHeaders.Length);

            summaryHeaderRange.Style.Font.Bold = true;
            summaryHeaderRange.Style.Font.FontColor = XLColor.White;
            summaryHeaderRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#334155");
            summaryHeaderRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            summaryHeaderRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;
            summaryHeaderRange.Style.Alignment.WrapText = true;

            summarySheet.Row(1).Height = 34;

            var summaryRow = 2;

            for (var index = 0;
                 index < invoices.Count;
                 index++)
            {
                var invoice =
                    invoices[index];

                var matchedRows =
                    matchedByInvoice.TryGetValue(
                        invoice.Id,
                        out var matchedList)
                        ? matchedList
                        : new List<InvoiceItem>();

                var storedPendingRows =
                    pendingByInvoice.TryGetValue(
                        invoice.Id,
                        out var pendingList)
                        ? pendingList
                        : new List<PendingInvoiceItem>();

                var matchedQty =
                    matchedRows.Sum(x => x.InvoiceQty);

                var pendingQty =
                    storedPendingRows.Sum(x => x.InvoiceQty);

                var matchedGst =
                    matchedRows.Sum(x => x.GstAmount);

                var pendingGst =
                    storedPendingRows.Sum(x => x.GstAmount);

                var matchedAmount =
                    matchedRows.Sum(x => x.Amount);

                var pendingAmount =
                    storedPendingRows.Sum(x => x.Amount);

                summarySheet.Cell(summaryRow, 1).Value =
                    index + 1;

                summarySheet.Cell(summaryRow, 2).Value =
                    invoice.InvoiceNumber;

                if (invoice.InvoiceDate.HasValue)
                {
                    summarySheet.Cell(summaryRow, 3).Value =
                        invoice.InvoiceDate.Value;

                    summarySheet.Cell(summaryRow, 3)
                        .Style.DateFormat.Format =
                            "dd-MMM-yyyy";
                }

                summarySheet.Cell(summaryRow, 4).Value =
                    invoice.CustomerName ?? "";

                summarySheet.Cell(summaryRow, 5).Value =
                    matchedRows.Count;

                summarySheet.Cell(summaryRow, 6).Value =
                    storedPendingRows.Count;

                summarySheet.Cell(summaryRow, 7).Value =
                    matchedRows.Count +
                    storedPendingRows.Count;

                summarySheet.Cell(summaryRow, 8).Value =
                    matchedQty;

                summarySheet.Cell(summaryRow, 9).Value =
                    pendingQty;

                summarySheet.Cell(summaryRow, 10).Value =
                    matchedQty + pendingQty;

                summarySheet.Cell(summaryRow, 11).Value =
                    matchedGst;

                summarySheet.Cell(summaryRow, 12).Value =
                    pendingGst;

                summarySheet.Cell(summaryRow, 13).Value =
                    matchedGst + pendingGst;

                summarySheet.Cell(summaryRow, 14).Value =
                    matchedAmount;

                summarySheet.Cell(summaryRow, 15).Value =
                    pendingAmount;

                summarySheet.Cell(summaryRow, 16).Value =
                    matchedAmount + pendingAmount;

                summarySheet.Cell(summaryRow, 17).Value =
                    invoice.UploadedFileName ?? "";

                summarySheet.Cell(summaryRow, 18).Value =
                    invoice.CreatedDate;

                summarySheet.Cell(summaryRow, 18)
                    .Style.DateFormat.Format =
                        "dd-MMM-yyyy HH:mm";

                summaryRow++;
            }

            if (summaryRow > 2)
            {
                summarySheet.Range(
                        2,
                        8,
                        summaryRow - 1,
                        16)
                    .Style.NumberFormat.Format =
                        "#,##0.00";
            }

            var totalRow = summaryRow;

            summarySheet.Cell(totalRow, 1).Value =
                "TOTAL";

            summarySheet.Range(
                    totalRow,
                    1,
                    totalRow,
                    4)
                .Merge();

            summarySheet.Cell(totalRow, 5).Value =
                invoiceItems.Count;

            summarySheet.Cell(totalRow, 6).Value =
                pendingItems.Count;

            summarySheet.Cell(totalRow, 7).Value =
                invoiceItems.Count +
                pendingItems.Count;

            summarySheet.Cell(totalRow, 8).Value =
                invoiceItems.Sum(x => x.InvoiceQty);

            summarySheet.Cell(totalRow, 9).Value =
                pendingItems.Sum(x => x.InvoiceQty);

            summarySheet.Cell(totalRow, 10).Value =
                invoiceItems.Sum(x => x.InvoiceQty) +
                pendingItems.Sum(x => x.InvoiceQty);

            summarySheet.Cell(totalRow, 11).Value =
                invoiceItems.Sum(x => x.GstAmount);

            summarySheet.Cell(totalRow, 12).Value =
                pendingItems.Sum(x => x.GstAmount);

            summarySheet.Cell(totalRow, 13).Value =
                invoiceItems.Sum(x => x.GstAmount) +
                pendingItems.Sum(x => x.GstAmount);

            summarySheet.Cell(totalRow, 14).Value =
                invoiceItems.Sum(x => x.Amount);

            summarySheet.Cell(totalRow, 15).Value =
                pendingItems.Sum(x => x.Amount);

            summarySheet.Cell(totalRow, 16).Value =
                invoiceItems.Sum(x => x.Amount) +
                pendingItems.Sum(x => x.Amount);

            summarySheet.Range(
                    totalRow,
                    1,
                    totalRow,
                    summaryHeaders.Length)
                .Style.Font.Bold = true;

            summarySheet.Range(
                    totalRow,
                    1,
                    totalRow,
                    summaryHeaders.Length)
                .Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#E8EEFF");

            summarySheet.Range(
                    totalRow,
                    1,
                    totalRow,
                    summaryHeaders.Length)
                .Style.Font.FontColor =
                    XLColor.FromHtml("#1F3A8A");

            summarySheet.Range(
                    totalRow,
                    8,
                    totalRow,
                    16)
                .Style.NumberFormat.Format =
                    "#,##0.00";

            summarySheet.SheetView.FreezeRows(1);

            if (invoices.Count > 0)
            {
                summarySheet.Range(
                        1,
                        1,
                        totalRow - 1,
                        summaryHeaders.Length)
                    .SetAutoFilter();
            }

            summarySheet.Column(1).Width = 9;
            summarySheet.Column(2).Width = 22;
            summarySheet.Column(3).Width = 16;
            summarySheet.Column(4).Width = 30;
            summarySheet.Columns(5, 16).Width = 16;
            summarySheet.Column(17).Width = 30;
            summarySheet.Column(18).Width = 21;

            // ========================================================
            // COMMON WORKBOOK STYLING
            // ========================================================

            foreach (var worksheet in workbook.Worksheets)
            {
                var usedRange =
                    worksheet.RangeUsed();

                if (usedRange != null)
                {
                    usedRange.Style.Border.OutsideBorder =
                        XLBorderStyleValues.Thin;

                    usedRange.Style.Border.InsideBorder =
                        XLBorderStyleValues.Hair;

                    usedRange.Style.Border.OutsideBorderColor =
                        XLColor.FromHtml("#CBD5E1");

                    usedRange.Style.Border.InsideBorderColor =
                        XLColor.FromHtml("#E5E7EB");

                    usedRange.Style.Alignment.Vertical =
                        XLAlignmentVerticalValues.Top;
                }
            }

            mergedSheet.Position = 1;
            summarySheet.Position = 2;

            using var outputStream =
                new MemoryStream();

            workbook.SaveAs(outputStream);

            var downloadFileName =
                "Merged_All_Invoices_" +
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss") +
                ".xlsx";

            return File(
                outputStream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                downloadFileName);
        }

        // ============================================================
        // OLD LINK COMPATIBILITY
        // ============================================================

    }
}