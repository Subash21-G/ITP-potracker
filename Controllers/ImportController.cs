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
    public class ImportController : Controller
    {
        private const int MaximumRowsPerImport = 2000;
        private const long MaximumExcelFileSize = 20L * 1024L * 1024L;
        private static readonly TimeSpan BatchLifetime = TimeSpan.FromHours(24);

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly PendingInvoiceMatcher _pendingInvoiceMatcher;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public ImportController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            PendingInvoiceMatcher pendingInvoiceMatcher)
        {
            _context = context;
            _environment = environment;
            _pendingInvoiceMatcher = pendingInvoiceMatcher;
        }

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
            worksheet.RangeUsed().SetAutoFilter();
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

        // ============================================================
        // EXCEL HEADER HELPERS
        // ============================================================

        private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet worksheet)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

            for (var columnNumber = 1; columnNumber <= lastColumn; columnNumber++)
            {
                var rawHeader = worksheet.Cell(1, columnNumber).GetFormattedString();
                var normalizedHeader = NormalizeHeader(rawHeader);

                if (!string.IsNullOrWhiteSpace(normalizedHeader) &&
                    !map.ContainsKey(normalizedHeader))
                {
                    map[normalizedHeader] = columnNumber;
                }
            }

            return map;
        }

        private static List<string> GetMissingRequiredHeaders(
            IReadOnlyDictionary<string, int> headerMap)
        {
            var requiredHeaders = new Dictionary<string, string>
            {
                ["purchasername"] = "PurchaserName",
                ["ponumber"] = "PONumber",
                ["materialno"] = "MaterialNo",
                ["qty"] = "Qty"
            };

            return requiredHeaders
                .Where(x => !headerMap.ContainsKey(x.Key))
                .Select(x => x.Value)
                .ToList();
        }

        private static string NormalizeHeader(string? value)
        {
            return Regex.Replace(
                value?.Trim().ToLowerInvariant() ?? string.Empty,
                @"[^a-z0-9]",
                string.Empty);
        }

        // ============================================================
        // EXCEL ROW PARSING
        // ============================================================

        private static MaterialImportRowViewModel ParseExcelRow(
            IXLWorksheet worksheet,
            int rowNumber,
            IReadOnlyDictionary<string, int> headerMap)
        {
            var row = new MaterialImportRowViewModel
            {
                RowNumber = rowNumber,
                PurchaserName = GetCellText(worksheet, rowNumber, headerMap, "purchasername"),
                ContactPerson = GetCellText(worksheet, rowNumber, headerMap, "contactperson"),
                PONumber = GetCellText(worksheet, rowNumber, headerMap, "ponumber"),
                PODate = GetCellDate(worksheet, rowNumber, headerMap, "podate"),
                ReceivedDate = GetCellDate(worksheet, rowNumber, headerMap, "receiveddate"),
                BuyerContactPerson = GetCellText(worksheet, rowNumber, headerMap, "buyercontactperson"),
                LineItem = GetCellText(worksheet, rowNumber, headerMap, "lineitem"),
                MaterialNo = GetCellText(worksheet, rowNumber, headerMap, "materialno"),
                HsnSacCode = GetCellText(worksheet, rowNumber, headerMap, "hsnsaccode"),
                ShortDescription = GetCellText(worksheet, rowNumber, headerMap, "shortdescription"),
                BriefDescription = GetCellText(worksheet, rowNumber, headerMap, "briefdescription"),
                Qty = GetCellDecimal(worksheet, rowNumber, headerMap, "qty"),
                Unit = GetCellText(worksheet, rowNumber, headerMap, "unit"),
                Dep = GetCellText(worksheet, rowNumber, headerMap, "dep"),
                ValueOfGoods = GetCellDecimal(worksheet, rowNumber, headerMap, "valueofgoods"),
                PriceUnit = GetCellText(worksheet, rowNumber, headerMap, "priceunit"),
                TotalValue = GetCellDecimal(worksheet, rowNumber, headerMap, "totalvalue"),
                DeliveryDate = GetCellDate(worksheet, rowNumber, headerMap, "deliverydate"),
                DrawingNo = GetCellText(worksheet, rowNumber, headerMap, "drawingno"),
                AssignedDashboard = GetCellText(worksheet, rowNumber, headerMap, "assigneddashboard"),
                Remark = GetCellText(worksheet, rowNumber, headerMap, "remark")
            };

            row.PurchaserName = row.PurchaserName.Trim();
            row.PONumber = row.PONumber.Trim();
            row.MaterialNo = row.MaterialNo.Trim();
            row.LineItem = NormalizeLineItem(row.LineItem, rowNumber);

            if (string.IsNullOrWhiteSpace(row.ContactPerson))
            {
                row.ContactPerson = row.BuyerContactPerson;
            }

            if (!row.PODate.HasValue)
            {
                row.PODate = row.ReceivedDate;
            }

            if (!row.ReceivedDate.HasValue)
            {
                row.ReceivedDate = row.PODate;
            }

            if (row.TotalValue <= 0 && row.ValueOfGoods > 0 && row.Qty > 0)
            {
                row.TotalValue = row.ValueOfGoods * row.Qty;
            }

            return row;
        }

        private static bool IsRowEmpty(
            IXLWorksheet worksheet,
            int rowNumber,
            IReadOnlyDictionary<string, int> headerMap)
        {
            var keyHeaders = new[]
            {
                "purchasername",
                "ponumber",
                "lineitem",
                "materialno",
                "qty",
                "shortdescription"
            };

            return keyHeaders.All(header =>
                string.IsNullOrWhiteSpace(GetCellText(worksheet, rowNumber, headerMap, header)));
        }

        private static string GetCellText(
            IXLWorksheet worksheet,
            int rowNumber,
            IReadOnlyDictionary<string, int> headerMap,
            string normalizedHeader)
        {
            if (!headerMap.TryGetValue(normalizedHeader, out var columnNumber))
            {
                return string.Empty;
            }

            return worksheet.Cell(rowNumber, columnNumber)
                .GetFormattedString()
                .Trim();
        }

        private static decimal GetCellDecimal(
            IXLWorksheet worksheet,
            int rowNumber,
            IReadOnlyDictionary<string, int> headerMap,
            string normalizedHeader)
        {
            if (!headerMap.TryGetValue(normalizedHeader, out var columnNumber))
            {
                return 0;
            }

            var cell = worksheet.Cell(rowNumber, columnNumber);

            if (cell.TryGetValue<decimal>(out var decimalValue))
            {
                return decimalValue;
            }

            var text = cell.GetFormattedString()
                .Replace(",", string.Empty)
                .Trim();

            return decimal.TryParse(
                text,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var parsedValue)
                ? parsedValue
                : 0;
        }

        private static DateTime? GetCellDate(
            IXLWorksheet worksheet,
            int rowNumber,
            IReadOnlyDictionary<string, int> headerMap,
            string normalizedHeader)
        {
            if (!headerMap.TryGetValue(normalizedHeader, out var columnNumber))
            {
                return null;
            }

            var cell = worksheet.Cell(rowNumber, columnNumber);

            if (cell.TryGetValue<DateTime>(out var dateValue))
            {
                return dateValue.Date;
            }

            var text = cell.GetFormattedString().Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var formats = new[]
            {
                "dd.MM.yy",
                "dd.MM.yyyy",
                "dd/MM/yy",
                "dd/MM/yyyy",
                "dd-MM-yy",
                "dd-MM-yyyy",
                "yyyy-MM-dd",
                "MM/dd/yyyy"
            };

            if (DateTime.TryParseExact(
                    text,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
            {
                return parsedDate.Date;
            }

            return DateTime.TryParse(text, out parsedDate)
                ? parsedDate.Date
                : null;
        }

        // ============================================================
        // ROW VALIDATION
        // ============================================================

        private static void ValidateRow(MaterialImportRowViewModel row)
        {
            if (string.IsNullOrWhiteSpace(row.PurchaserName))
            {
                row.Errors.Add("PurchaserName is required.");
            }

            if (string.IsNullOrWhiteSpace(row.PONumber))
            {
                row.Errors.Add("PONumber is required.");
            }

            if (string.IsNullOrWhiteSpace(row.MaterialNo))
            {
                row.Errors.Add("MaterialNo is required.");
            }

            if (row.Qty <= 0)
            {
                row.Errors.Add("Qty must be greater than zero.");
            }

            if (row.PurchaserName.Length > 250)
            {
                row.Errors.Add("PurchaserName is too long.");
            }

            if (row.PONumber.Length > 100)
            {
                row.Errors.Add("PONumber is too long.");
            }

            if (row.MaterialNo.Length > 100)
            {
                row.Errors.Add("MaterialNo is too long.");
            }

            if (!string.IsNullOrWhiteSpace(row.AssignedDashboard))
            {
                var validDashboards = new[]
                {
                    "Gokulnath",
                    "Paramaguru",
                    "Sam",
                    "Abdul",
                    "Kalai"
                };

                if (!validDashboards.Contains(
                        row.AssignedDashboard.Trim(),
                        StringComparer.OrdinalIgnoreCase))
                {
                    row.Errors.Add(
                        "AssignedDashboard must be Gokulnath, Paramaguru, Sam, Abdul or Kalai.");
                }
            }
        }

        private static void ValidateDuplicateRows(
            IReadOnlyCollection<MaterialImportRowViewModel> rows)
        {
            var duplicateGroups = rows
                .GroupBy(x =>
                    $"{NormalizeKey(x.PONumber)}|" +
                    $"{NormalizeKey(x.LineItem)}")
                .Where(x => x.Count() > 1);

            foreach (var duplicateGroup in duplicateGroups)
            {
                foreach (var row in duplicateGroup)
                {
                    row.Errors.Add(
                        "Duplicate line item exists for the same PO in this Excel file.");
                }
            }
        }

        private static void ValidatePurchaseOrderConsistency(
            IReadOnlyCollection<MaterialImportRowViewModel> rows)
        {
            var purchaseOrderGroups = rows
                .Where(x => !string.IsNullOrWhiteSpace(x.PONumber))
                .GroupBy(x => NormalizeKey(x.PONumber));

            foreach (var group in purchaseOrderGroups)
            {
                var purchaserCount = group
                    .Select(x => NormalizeKey(x.PurchaserName))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .Count();

                if (purchaserCount <= 1)
                {
                    continue;
                }

                foreach (var row in group)
                {
                    row.Errors.Add(
                        "The same PO number is assigned to more than one purchaser in this Excel file.");
                }
            }
        }

        // ============================================================
        // PURCHASER AND PO UPSERT
        // ============================================================

        private async Task<Dictionary<string, Purchaser>> CreateOrUpdatePurchasersAsync(
            IReadOnlyCollection<MaterialImportRowViewModel> rows)
        {
            var names = rows
                .Select(x => x.PurchaserName.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingPurchasers = await _context.Purchasers
                .Where(x => names.Contains(x.PurchaserName))
                .ToListAsync();

            var map = existingPurchasers
                .GroupBy(x => NormalizeKey(x.PurchaserName))
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                var key = NormalizeKey(name);
                var firstRow = rows.First(x =>
                    string.Equals(
                        x.PurchaserName.Trim(),
                        name,
                        StringComparison.OrdinalIgnoreCase));

                if (!map.TryGetValue(key, out var purchaser))
                {
                    purchaser = new Purchaser
                    {
                        PurchaserName = name,
                        ContactPerson = FirstNonEmpty(
                            firstRow.ContactPerson,
                            firstRow.BuyerContactPerson)
                    };

                    _context.Purchasers.Add(purchaser);
                    map[key] = purchaser;
                }
                else
                {
                    var contactPerson = FirstNonEmpty(
                        firstRow.ContactPerson,
                        firstRow.BuyerContactPerson);

                    if (!string.IsNullOrWhiteSpace(contactPerson))
                    {
                        purchaser.ContactPerson = contactPerson;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return map;
        }

        private async Task<Dictionary<string, PurchaseOrder>> CreateOrUpdatePurchaseOrdersAsync(
            IReadOnlyCollection<MaterialImportRowViewModel> rows,
            IReadOnlyDictionary<string, Purchaser> purchaserMap)
        {
            var poNumbers = rows
                .Select(x => x.PONumber.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingPurchaseOrders = await _context.PurchaseOrders
                .Where(x => poNumbers.Contains(x.PONumber))
                .ToListAsync();

            var map = existingPurchaseOrders
                .GroupBy(x => NormalizeKey(x.PONumber))
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var poNumber in poNumbers)
            {
                var poKey = NormalizeKey(poNumber);
                var poRows = rows
                    .Where(x => NormalizeKey(x.PONumber) == poKey)
                    .ToList();

                var firstRow = poRows.First();
                var purchaser = purchaserMap[NormalizeKey(firstRow.PurchaserName)];

                if (!map.TryGetValue(poKey, out var purchaseOrder))
                {
                    purchaseOrder = new PurchaseOrder
                    {
                        PurchaserId = purchaser.Id,
                        PONumber = poNumber,
                        PODate = FirstDate(poRows.Select(x => x.PODate)),
                        ReceivedDate = FirstDate(poRows.Select(x => x.ReceivedDate)),
                        BuyerContactPerson = FirstNonEmpty(
                            poRows.Select(x => x.BuyerContactPerson).ToArray()),
                        CreatedDate = DateTime.Now
                    };

                    _context.PurchaseOrders.Add(purchaseOrder);
                    map[poKey] = purchaseOrder;
                }
                else
                {
                    purchaseOrder.PurchaserId = purchaser.Id;
                    purchaseOrder.PODate =
                        FirstDate(poRows.Select(x => x.PODate)) ?? purchaseOrder.PODate;
                    purchaseOrder.ReceivedDate =
                        FirstDate(poRows.Select(x => x.ReceivedDate)) ?? purchaseOrder.ReceivedDate;

                    var buyer = FirstNonEmpty(
                        poRows.Select(x => x.BuyerContactPerson).ToArray());

                    if (!string.IsNullOrWhiteSpace(buyer))
                    {
                        purchaseOrder.BuyerContactPerson = buyer;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return map;
        }

        // ============================================================
        // MATERIAL CREATE/UPDATE
        // ============================================================

        private static Material CreateMaterial(
            MaterialImportRowViewModel row,
            int purchaseOrderId,
            string lineItem)
        {
            var material = new Material
            {
                PurchaseOrderId = purchaseOrderId,
                LineItem = lineItem,
                MaterialNo = row.MaterialNo.Trim(),
                HsnSacCode = NullIfWhiteSpace(row.HsnSacCode),
                ShortDescription = NullIfWhiteSpace(row.ShortDescription),
                BriefDescription = NullIfWhiteSpace(row.BriefDescription),
                Qty = row.Qty,
                Unit = NullIfWhiteSpace(row.Unit),
                Dep = NullIfWhiteSpace(row.Dep),
                DispatchQty = 0,
                PendingQty = row.Qty,
                StockQty = 0,
                InvoiceQty = 0,
                BalanceQty = row.Qty,
                InvoiceStatus = "Not Invoiced",
                Cost = row.ValueOfGoods,
                ValueOfGoods = row.ValueOfGoods,
                PriceUnit = NullIfWhiteSpace(row.PriceUnit),
                TotalValue = row.TotalValue,
                PODate = row.PODate,
                ReceivingDate = row.ReceivedDate,
                DeliveryDate = row.DeliveryDate,
                DrawingNo = NullIfWhiteSpace(row.DrawingNo),
                DrawingVersion = ExtractDrawingVersion(row.DrawingNo),
                ProcessStatus = "Pending",
                DispatchStatus = "Pending",
                AssignedDashboard = NullIfWhiteSpace(row.AssignedDashboard),
                Remark = NullIfWhiteSpace(row.Remark),
                CreatedDate = DateTime.Now
            };

            ApplyMaterialSchedule(material);
            return material;
        }

        private static void ApplyRowToExistingMaterial(
            Material material,
            MaterialImportRowViewModel row,
            int purchaseOrderId,
            string lineItem)
        {
            material.PurchaseOrderId = purchaseOrderId;
            material.LineItem = lineItem;
            material.MaterialNo = row.MaterialNo.Trim();
            material.HsnSacCode = NullIfWhiteSpace(row.HsnSacCode);
            material.ShortDescription = NullIfWhiteSpace(row.ShortDescription);
            material.BriefDescription = NullIfWhiteSpace(row.BriefDescription);
            material.Qty = row.Qty;
            material.Unit = NullIfWhiteSpace(row.Unit);
            material.Dep = NullIfWhiteSpace(row.Dep);
            material.PendingQty = Math.Max(0, row.Qty - material.DispatchQty);
            material.BalanceQty = Math.Max(0, row.Qty - material.InvoiceQty);
            material.InvoiceStatus = GetInvoiceStatus(
                row.Qty,
                material.InvoiceQty,
                material.BalanceQty);
            material.Cost = row.ValueOfGoods;
            material.ValueOfGoods = row.ValueOfGoods;
            material.PriceUnit = NullIfWhiteSpace(row.PriceUnit);
            material.TotalValue = row.TotalValue;
            material.PODate = row.PODate;
            material.ReceivingDate = row.ReceivedDate;
            material.DeliveryDate = row.DeliveryDate;

            if (!string.IsNullOrWhiteSpace(row.DrawingNo))
            {
                material.DrawingNo = row.DrawingNo.Trim();
                material.DrawingVersion = ExtractDrawingVersion(row.DrawingNo);
            }

            if (string.IsNullOrWhiteSpace(material.ProcessStatus))
            {
                material.ProcessStatus = "Pending";
            }

            if (string.IsNullOrWhiteSpace(material.DispatchStatus))
            {
                material.DispatchStatus = "Pending";
            }

            material.AssignedDashboard = NullIfWhiteSpace(row.AssignedDashboard);
            material.Remark = NullIfWhiteSpace(row.Remark);

            ApplyMaterialSchedule(material);
        }

        private static void ApplyMaterialSchedule(Material material)
        {
            if (!material.DeliveryDate.HasValue)
            {
                material.ProcessStartDate = null;
                material.AlertDate = null;
                return;
            }

            material.ProcessStartDate = material.DeliveryDate.Value.AddMonths(-1);
            material.AlertDate = material.ProcessStartDate;
        }

        private static string GetInvoiceStatus(
            decimal quantity,
            decimal invoiceQuantity,
            decimal balanceQuantity)
        {
            if (invoiceQuantity <= 0)
            {
                return "Not Invoiced";
            }

            if (balanceQuantity <= 0 || invoiceQuantity >= quantity)
            {
                return "Fully Invoiced";
            }

            return "Partially Invoiced";
        }

        // ============================================================
        // SERVER-SIDE BATCH STORAGE
        // ============================================================

        private string GetBatchFolder()
        {
            return Path.Combine(
                _environment.ContentRootPath,
                "App_Data",
                "material-import-batches");
        }

        private string? GetBatchFilePath(string? batchId)
        {
            if (!Guid.TryParseExact(batchId, "N", out var parsedBatchId))
            {
                return null;
            }

            return Path.Combine(
                GetBatchFolder(),
                parsedBatchId.ToString("N") + ".json");
        }

        private async Task SaveBatchAsync(MaterialImportBatchViewModel batch)
        {
            var folder = GetBatchFolder();
            Directory.CreateDirectory(folder);

            var filePath = GetBatchFilePath(batch.BatchId)
                ?? throw new InvalidOperationException("Invalid import batch ID.");

            var json = JsonSerializer.Serialize(batch, _jsonOptions);
            await System.IO.File.WriteAllTextAsync(filePath, json);
        }

        private async Task<MaterialImportBatchViewModel?> LoadBatchAsync(string? batchId)
        {
            var filePath = GetBatchFilePath(batchId);

            if (filePath == null || !System.IO.File.Exists(filePath))
            {
                return null;
            }

            var fileInfo = new FileInfo(filePath);

            if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > BatchLifetime)
            {
                DeleteBatchFile(batchId);
                return null;
            }

            try
            {
                var json = await System.IO.File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<MaterialImportBatchViewModel>(json, _jsonOptions);
            }
            catch
            {
                DeleteBatchFile(batchId);
                return null;
            }
        }

        private void DeleteBatchFile(string? batchId)
        {
            var filePath = GetBatchFilePath(batchId);

            if (filePath == null)
            {
                return;
            }

            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch
            {
                // Cleanup failure should not interrupt the request.
            }
        }

        private void DeleteExpiredBatchFiles()
        {
            try
            {
                var folder = GetBatchFolder();

                if (!Directory.Exists(folder))
                {
                    return;
                }

                foreach (var filePath in Directory.GetFiles(folder, "*.json"))
                {
                    var fileInfo = new FileInfo(filePath);

                    if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > BatchLifetime)
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }
            catch
            {
                // Expired-batch cleanup is best effort only.
            }
        }

        // ============================================================
        // GENERAL HELPERS
        // ============================================================

        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetString("UserName") != null;
        }

        private static string NormalizeLineItem(string? value, int rowNumber)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                var generatedNumber = Math.Max(10, (rowNumber - 1) * 10);
                return "N-" + generatedNumber;
            }

            var digits = Regex.Replace(value, @"\D", string.Empty);

            return string.IsNullOrWhiteSpace(digits)
                ? value.Trim().ToUpperInvariant()
                : "N-" + digits;
        }

        private static string CreateMaterialKey(
            int purchaseOrderId,
            string? lineItem,
            string? materialNo)
        {
            // Line item is the unique material row key inside a purchase order.
            // materialNo is retained in the signature for readable call sites.
            _ = materialNo;
            return $"{purchaseOrderId}|{NormalizeKey(lineItem)}";
        }

        private static string NormalizeKey(string? value)
        {
            return value?.Trim().ToUpperInvariant() ?? string.Empty;
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static string? ExtractDrawingVersion(string? drawingNumber)
        {
            if (string.IsNullOrWhiteSpace(drawingNumber))
            {
                return null;
            }

            var match = Regex.Match(
                drawingNumber.Trim(),
                @"(?<revision>\d{1,3})\s*$");

            return match.Success
                ? match.Groups["revision"].Value
                : null;
        }

        private static DateTime? FirstDate(IEnumerable<DateTime?> dates)
        {
            return dates.FirstOrDefault(x => x.HasValue);
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
        }
    }
}