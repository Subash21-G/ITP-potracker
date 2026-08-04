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
    public partial class ImportController : ControllerBase
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
