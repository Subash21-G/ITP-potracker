using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;
using POTracker.ViewModels;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace POTracker.Controllers
{
    public partial class ImportController
    {
        // ============================================================
        // IMPORT HOME PAGE
        // ============================================================

        [HttpGet]
        public IActionResult Index()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            DeleteExpiredBatchFiles();
            return View();
        }

        // ============================================================
        // DOWNLOAD EXCEL TEMPLATE
        // ============================================================

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Materials");

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

            for (var column = 0; column < headers.Length; column++)
            {
                worksheet.Cell(1, column + 1).Value = headers[column];
            }

            // Example row
            worksheet.Cell(2, 1).Value = "WEG Industries (India) PVT LTD";
            worksheet.Cell(2, 2).Value = "Gokulnath V";
            worksheet.Cell(2, 3).Value = "4519000001";
            worksheet.Cell(2, 4).Value = DateTime.Today;
            worksheet.Cell(2, 5).Value = DateTime.Today;
            worksheet.Cell(2, 6).Value = "Gokulnath V";
            worksheet.Cell(2, 7).Value = "N-10";
            worksheet.Cell(2, 8).Value = "15464033";
            worksheet.Cell(2, 9).Value = "8503.00.90";
            worksheet.Cell(2, 10).Value = "CABLE ENTRY PLATE";
            worksheet.Cell(2, 11).Value = "CABLE ENTRY PLATE SAE1010/20";
            worksheet.Cell(2, 12).Value = 2;
            worksheet.Cell(2, 13).Value = "UN";
            worksheet.Cell(2, 14).Value = "RS02";
            worksheet.Cell(2, 15).Value = 1531;
            worksheet.Cell(2, 16).Value = "UN";
            worksheet.Cell(2, 17).Value = 3062;
            worksheet.Cell(2, 18).Value = DateTime.Today.AddDays(30);
            worksheet.Cell(2, 19).Value = "SWD 10004012815 000 01";
            worksheet.Cell(2, 20).Value = "Gokulnath";
            worksheet.Cell(2, 21).Value = "Sample row - delete before upload";

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4FD7");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            worksheet.SheetView.FreezeRows(1);
          worksheet.RangeUsed()?.SetAutoFilter();
            worksheet.Columns().AdjustToContents(12, 32);
            worksheet.Column(11).Width = 45;
            worksheet.Column(21).Width = 35;

            worksheet.Column(4).Style.DateFormat.Format = "dd-MM-yyyy";
            worksheet.Column(5).Style.DateFormat.Format = "dd-MM-yyyy";
            worksheet.Column(18).Style.DateFormat.Format = "dd-MM-yyyy";
            worksheet.Column(12).Style.NumberFormat.Format = "0.00";
            worksheet.Column(15).Style.NumberFormat.Format = "0.00";
            worksheet.Column(17).Style.NumberFormat.Format = "0.00";

            worksheet.Range(2, 1, MaximumRowsPerImport + 1, headers.Length)
                .Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "POTracker_Material_Import_Template.xlsx");
        }

        // ============================================================
        // UPLOAD EXCEL AND CREATE SERVER-SIDE PREVIEW BATCH
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaximumExcelFileSize)]
        public async Task<IActionResult> Upload(IFormFile? excelFile)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "Please select an Excel file.";
                return RedirectToAction(nameof(Index));
            }

            if (excelFile.Length > MaximumExcelFileSize)
            {
                TempData["Error"] = "The Excel file is too large. Maximum allowed size is 20 MB.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(excelFile.FileName);
            var allowedExtensions = new[] { ".xlsx", ".xlsm" };

            if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only .xlsx and .xlsm Excel files are supported.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await using var uploadStream = new MemoryStream();
                await excelFile.CopyToAsync(uploadStream);
                uploadStream.Position = 0;

                using var workbook = new XLWorkbook(uploadStream);
                var worksheet = workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                {
                    TempData["Error"] = "The Excel workbook does not contain any worksheet.";
                    return RedirectToAction(nameof(Index));
                }

                var headerMap = BuildHeaderMap(worksheet);
                var missingHeaders = GetMissingRequiredHeaders(headerMap);

                if (missingHeaders.Count > 0)
                {
                    TempData["Error"] =
                        "Missing required Excel columns: " +
                        string.Join(", ", missingHeaders) + ".";

                    return RedirectToAction(nameof(Index));
                }

                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                var dataRowCount = Math.Max(0, lastRow - 1);

                if (dataRowCount == 0)
                {
                    TempData["Error"] = "The Excel file does not contain any material rows.";
                    return RedirectToAction(nameof(Index));
                }

                if (dataRowCount > MaximumRowsPerImport)
                {
                    TempData["Error"] =
                        $"A maximum of {MaximumRowsPerImport} rows can be imported at one time. " +
                        $"This file contains approximately {dataRowCount} data rows.";

                    return RedirectToAction(nameof(Index));
                }

                var rows = new List<MaterialImportRowViewModel>();

                for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
                {
                    if (IsRowEmpty(worksheet, rowNumber, headerMap))
                    {
                        continue;
                    }

                    var row = ParseExcelRow(worksheet, rowNumber, headerMap);
                    ValidateRow(row);
                    rows.Add(row);
                }

                if (rows.Count == 0)
                {
                    TempData["Error"] = "No non-empty material rows were found in the Excel file.";
                    return RedirectToAction(nameof(Index));
                }

                ValidateDuplicateRows(rows);
                ValidatePurchaseOrderConsistency(rows);

                var batch = new MaterialImportBatchViewModel
                {
                    BatchId = Guid.NewGuid().ToString("N"),
                    OriginalFileName = Path.GetFileName(excelFile.FileName),
                    CreatedAt = DateTime.Now,
                    Rows = rows
                };

                await SaveBatchAsync(batch);

                return RedirectToAction(
                    nameof(Preview),
                    new { batchId = batch.BatchId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Unable to process the Excel file. " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // ============================================================
        // SHOW PREVIEW USING BATCH ID ONLY
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Preview(string? batchId)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var batch = await LoadBatchAsync(batchId);

            if (batch == null)
            {
                TempData["Error"] = "The import preview has expired or could not be found. Please upload the file again.";
                return RedirectToAction(nameof(Index));
            }

            var model = new MaterialImportPreviewViewModel
            {
                BatchId = batch.BatchId,
                OriginalFileName = batch.OriginalFileName,
                CreatedAt = batch.CreatedAt,
                Rows = batch.Rows
            };

            return View(model);
        }

        // ============================================================
        // COMMIT VALID PREVIEW BATCH TO DATABASE
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Commit(string? batchId)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var batch = await LoadBatchAsync(batchId);

            if (batch == null)
            {
                TempData["Error"] = "The import batch has expired or could not be found. Please upload the file again.";
                return RedirectToAction(nameof(Index));
            }

            if (batch.Rows.Count == 0)
            {
                TempData["Error"] = "The import batch does not contain any material rows.";
                return RedirectToAction(nameof(Index));
            }

            if (batch.Rows.Any(x => !x.IsValid))
            {
                TempData["Error"] = "This import contains invalid rows. Correct the Excel file and upload it again.";
                return RedirectToAction(nameof(Preview), new { batchId = batch.BatchId });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var purchaserMap = await CreateOrUpdatePurchasersAsync(batch.Rows);
                var purchaseOrderMap = await CreateOrUpdatePurchaseOrdersAsync(batch.Rows, purchaserMap);

                await _context.SaveChangesAsync();

                var purchaseOrderIds = purchaseOrderMap.Values
                    .Select(x => x.Id)
                    .Distinct()
                    .ToList();

                var existingMaterials = await _context.Materials
                    .Where(x => purchaseOrderIds.Contains(x.PurchaseOrderId))
                    .ToListAsync();

                var materialMap = existingMaterials
                    .GroupBy(x => CreateMaterialKey(x.PurchaseOrderId, x.LineItem, x.MaterialNo))
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

                var createdCount = 0;
                var updatedCount = 0;

                foreach (var row in batch.Rows)
                {
                    var poKey = NormalizeKey(row.PONumber);
                    var purchaseOrder = purchaseOrderMap[poKey];
                    var lineItem = NormalizeLineItem(row.LineItem, row.RowNumber);
                    var materialKey = CreateMaterialKey(purchaseOrder.Id, lineItem, row.MaterialNo);

                    if (materialMap.TryGetValue(materialKey, out var material))
                    {
                        ApplyRowToExistingMaterial(material, row, purchaseOrder.Id, lineItem);
                        _context.Materials.Update(material);
                        updatedCount++;
                    }
                    else
                    {
                        material = CreateMaterial(row, purchaseOrder.Id, lineItem);
                        _context.Materials.Add(material);
                        materialMap[materialKey] = material;
                        createdCount++;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var matchedInvoiceRows = 0;

                foreach (var purchaseOrderId in purchaseOrderIds)
                {
                    try
                    {
                        matchedInvoiceRows += await _pendingInvoiceMatcher
                            .MatchPendingInvoicesForPurchaseOrderAsync(purchaseOrderId);
                    }
                    catch
                    {
                        // The material import remains successful even when an
                        // optional pending-invoice match fails for one PO.
                    }
                }

                DeleteBatchFile(batch.BatchId);

                TempData["Success"] =
                    $"Material import completed. Created: {createdCount}, " +
                    $"Updated: {updatedCount}, Pending invoices matched: {matchedInvoiceRows}.";

                return RedirectToAction("Index", "Material");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["Error"] = "Material import failed. No partial import was saved. " + ex.Message;
                return RedirectToAction(nameof(Preview), new { batchId = batch.BatchId });
            }
        }

        // ============================================================
        // CANCEL AND DELETE PREVIEW BATCH
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(string? batchId)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            if (!string.IsNullOrWhiteSpace(batchId))
            {
                DeleteBatchFile(batchId);
            }

            TempData["Success"] = "Material import preview cancelled.";
            return RedirectToAction(nameof(Index));
        }

    }
}
