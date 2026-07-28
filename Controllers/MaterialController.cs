using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;
using POTracker.ViewModels;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace POTracker.Controllers
{
    public class MaterialController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PendingInvoiceMatcher _pendingInvoiceMatcher;

        private static readonly JsonSerializerOptions
            StockJsonOptions = new()
            {
                PropertyNameCaseInsensitive = true
            };

        public MaterialController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IHttpClientFactory httpClientFactory,
            PendingInvoiceMatcher pendingInvoiceMatcher)
        {
            _context = context;
            _environment = environment;
            _httpClientFactory = httpClientFactory;
            _pendingInvoiceMatcher = pendingInvoiceMatcher;
        }

        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetString("UserName") != null;
        }

        // ============================================================
        // MATERIAL INDEX
        // Premium list + search + filters + pagination
        // Purchaser is intentionally not shown.
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            string? poNumber,
            string? handler,
            DateTime? poDateFrom,
            DateTime? poDateTo,
            DateTime? poReceivedDateFrom,
            DateTime? poReceivedDateTo,
            string? invoiceStatus,
            string? dispatchStatus,
            string? stockStatus,
            int page = 1,
            int pageSize = 10)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            page = page < 1 ? 1 : page;
            pageSize = NormalizePageSize(pageSize);

            var baseQuery = CreateMaterialQuery();

            var filterOptions = await LoadMaterialFilterOptionsAsync(
                baseQuery);

            var filteredQuery = ApplyMaterialFilters(
                baseQuery,
                search,
                poNumber,
                handler,
                poDateFrom,
                poDateTo,
                poReceivedDateFrom,
                poReceivedDateTo,
                invoiceStatus,
                dispatchStatus);

            List<MaterialIndexRowViewModel> pagedRows;
            MaterialIndexSummaryViewModel summary;
            int totalRecords;

            if (string.IsNullOrWhiteSpace(stockStatus))
            {
                // Normal fast path: calculate database totals first,
                // paginate in SQL, and request live stock only for the
                // rows that will actually be displayed.
                summary = await BuildMaterialDatabaseSummaryAsync(
                    filteredQuery);

                totalRecords = summary.TotalMaterials;

                var totalPages = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        totalRecords / (double)pageSize));

                if (page > totalPages)
                {
                    page = totalPages;
                }

                var pageMaterials = await filteredQuery
                    .OrderByDescending(x => x.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var pageStockMap =
                    await GetStockMapAsync(pageMaterials);

                pagedRows = pageMaterials
                    .Select(material =>
                        MapMaterialRow(
                            material,
                            GetStockForMaterial(
                                pageStockMap,
                                material.MaterialNo)))
                    .ToList();

                var pageStockSummary =
                    BuildMaterialSummary(pagedRows);

                summary.InStockMaterials =
                    pageStockSummary.InStockMaterials;

                summary.ShortageMaterials =
                    pageStockSummary.ShortageMaterials;

                summary.TotalLiveStockQuantity =
                    pageStockSummary.TotalLiveStockQuantity;

                summary.StockSummaryIsCurrentPage = true;
            }
            else
            {
                // Stock status belongs to the external Stock API and
                // cannot be filtered by SQL. Preserve correct filter
                // results by loading stock before applying this filter.
                var materials = await filteredQuery
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();

                var stockMap =
                    await GetStockMapAsync(materials);

                var allRows = materials
                    .Select(material =>
                        MapMaterialRow(
                            material,
                            GetStockForMaterial(
                                stockMap,
                                material.MaterialNo)))
                    .Where(x =>
                        string.Equals(
                            x.StockStatus,
                            stockStatus,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                totalRecords = allRows.Count;

                var totalPages = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        totalRecords / (double)pageSize));

                if (page > totalPages)
                {
                    page = totalPages;
                }

                pagedRows = allRows
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                summary = BuildMaterialSummary(allRows);
                summary.StockSummaryIsCurrentPage = false;
            }

            var model = new MaterialIndexViewModel
            {
                Items = pagedRows,
                Summary = summary,

                Search = search,
                PONumber = poNumber,
                Handler = handler,
                PODateFrom = poDateFrom,
                PODateTo = poDateTo,
                POReceivedDateFrom = poReceivedDateFrom,
                POReceivedDateTo = poReceivedDateTo,
                InvoiceStatus = invoiceStatus,
                DispatchStatus = dispatchStatus,
                StockStatus = stockStatus,

                Handlers = filterOptions.Handlers,
                InvoiceStatuses = filterOptions.InvoiceStatuses,
                DispatchStatuses = filterOptions.DispatchStatuses,
                StockStatuses = BuildStockStatusOptions(),

                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            };

            MarkSelectedOptions(model);

            return View(model);
        }

        // ============================================================
        // FILTERED MATERIAL EXCEL EXPORT
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            string? search,
            string? poNumber,
            string? handler,
            DateTime? poDateFrom,
            DateTime? poDateTo,
            DateTime? poReceivedDateFrom,
            DateTime? poReceivedDateTo,
            string? invoiceStatus,
            string? dispatchStatus,
            string? stockStatus)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var query = ApplyMaterialFilters(
                CreateMaterialQuery(),
                search,
                poNumber,
                handler,
                poDateFrom,
                poDateTo,
                poReceivedDateFrom,
                poReceivedDateTo,
                invoiceStatus,
                dispatchStatus);

            var materials = await query
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            var stockMap = await GetStockMapAsync(materials);

            var rows = materials
                .Select(material =>
                    MapMaterialRow(
                        material,
                        GetStockForMaterial(
                            stockMap,
                            material.MaterialNo)))
                .ToList();

            if (!string.IsNullOrWhiteSpace(stockStatus))
            {
                rows = rows
                    .Where(x =>
                        string.Equals(
                            x.StockStatus,
                            stockStatus,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Materials");

            CreateMaterialExcelTitle(
                worksheet,
                rows.Count);

            WriteMaterialExcelHeaders(worksheet);
            WriteMaterialExcelRows(worksheet, rows);
            StyleMaterialExcel(worksheet, rows.Count);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var fileName =
                $"Materials_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ============================================================
        // CREATE
        // ============================================================

        public IActionResult Create()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            LoadPurchaseOrderDropdown();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Material material,
            IFormFile? pdfFile,
            IFormFile? drawingFile)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            material.DispatchQty = 0;
            material.InvoiceQty = 0;

            ApplyScheduleRules(material);

            if (pdfFile != null && pdfFile.Length > 0)
            {
                material.PDFFilePath =
                    await SaveFile(pdfFile, "pdfs");
            }

            if (drawingFile != null && drawingFile.Length > 0)
            {
                material.DrawingFilePath =
                    await SaveFile(drawingFile, "drawings");
            }

            material.CreatedDate = DateTime.Now;

            _context.Materials.Add(material);
            await _context.SaveChangesAsync();

            await CreateOrUpdateProcessAlert(material);

            var matchedInvoiceRows =
                await _pendingInvoiceMatcher
                    .MatchPendingInvoicesForMaterialAsync(
                        material.Id);

            TempData["Success"] = matchedInvoiceRows > 0
                ? $"Material created successfully. Pending invoice matched: {matchedInvoiceRows}."
                : "Material created successfully.";

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // EDIT
        // ============================================================

        public async Task<IActionResult> Edit(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var material =
                await _context.Materials.FindAsync(id);

            if (material == null)
            {
                return NotFound();
            }

            LoadPurchaseOrderDropdown(
                material.PurchaseOrderId);

            ViewBag.StockInfo =
                await GetStockInfoAsync(
                    material.MaterialNo);

            return View(material);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            Material material,
            IFormFile? pdfFile,
            IFormFile? drawingFile)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var existingMaterial =
                await _context.Materials
                    .FirstOrDefaultAsync(x =>
                        x.Id == material.Id);

            if (existingMaterial == null)
            {
                return NotFound();
            }

            // BASIC DETAILS
            existingMaterial.PurchaseOrderId =
                material.PurchaseOrderId;

            existingMaterial.LineItem =
                material.LineItem;

            existingMaterial.MaterialNo =
                material.MaterialNo;

            existingMaterial.HsnSacCode =
                material.HsnSacCode;

            existingMaterial.ShortDescription =
                material.ShortDescription;

            existingMaterial.BriefDescription =
                material.BriefDescription;

            // QUANTITY DETAILS
            existingMaterial.Qty =
                material.Qty;

            existingMaterial.Unit =
                material.Unit;

            existingMaterial.Dep =
                material.Dep;

            existingMaterial.StockQty =
                material.StockQty;

            // DispatchQty is controlled by Dispatch.
            // InvoiceQty is controlled by Invoice Import.
            // Balance quantities are recalculated below.

            // COST DETAILS
            existingMaterial.Cost =
                material.Cost;

            existingMaterial.ValueOfGoods =
                material.ValueOfGoods;

            existingMaterial.PriceUnit =
                material.PriceUnit;

            existingMaterial.TotalValue =
                material.TotalValue;

            // RAW MATERIAL DETAILS
            existingMaterial.RMGrade =
                material.RMGrade;

            existingMaterial.RMSource =
                material.RMSource;

            existingMaterial.RMProcess =
                material.RMProcess;

            // SURFACE / MACHINING
            existingMaterial.SurfaceFinish =
                material.SurfaceFinish;

            existingMaterial.MachiningType =
                material.MachiningType;

            // PROCESS TIMES
            existingMaterial.ProcessTime =
                material.ProcessTime;

            existingMaterial.MaterialProcessTime =
                material.MaterialProcessTime;

            // DATE DETAILS
            existingMaterial.PODate =
                material.PODate;

            existingMaterial.ReceivingDate =
                material.ReceivingDate;

            existingMaterial.DeliveryDate =
                material.DeliveryDate;

            existingMaterial.DispatchDate =
                material.DispatchDate;

            existingMaterial.DrawingUpdatedDate =
                material.DrawingUpdatedDate;

            // DRAWING DETAILS
            existingMaterial.DrawingNo =
                material.DrawingNo;

            existingMaterial.DrawingVersion =
                material.DrawingVersion;

            if (pdfFile != null && pdfFile.Length > 0)
            {
                existingMaterial.PDFFilePath =
                    await SaveFile(pdfFile, "pdfs");
            }

            if (drawingFile != null &&
                drawingFile.Length > 0)
            {
                existingMaterial.DrawingFilePath =
                    await SaveFile(
                        drawingFile,
                        "drawings");
            }

            // STATUS DETAILS
            existingMaterial.ProcessStatus =
                string.IsNullOrWhiteSpace(
                    material.ProcessStatus)
                    ? "Pending"
                    : material.ProcessStatus;

            existingMaterial.AssignedDashboard =
                material.AssignedDashboard;

            existingMaterial.Remark =
                material.Remark;

            ApplyScheduleRules(existingMaterial);

            _context.Materials.Update(
                existingMaterial);

            await _context.SaveChangesAsync();

            await CreateOrUpdateProcessAlert(
                existingMaterial);

            var matchedInvoiceRows =
                await _pendingInvoiceMatcher
                    .MatchPendingInvoicesForMaterialAsync(
                        existingMaterial.Id);

            TempData["Success"] = matchedInvoiceRows > 0
                ? $"Material updated successfully. Pending invoice matched: {matchedInvoiceRows}."
                : "Material updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // MULTIPLE DELETE
        // Deletes only materials without linked invoice/dispatch rows.
        // Linked materials are safely skipped.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete(
            List<int>? selectedIds,
            string? returnUrl)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var materialIds =
                selectedIds?
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList() ??
                new List<int>();

            if (materialIds.Count == 0)
            {
                TempData["Error"] =
                    "Select at least one material to delete.";

                return RedirectToMaterialList(
                    returnUrl);
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var materials =
                    await _context.Materials
                        .Include(x =>
                            x.InvoiceItems)
                        .Include(x =>
                            x.Dispatches)
                        .Where(x =>
                            materialIds.Contains(x.Id))
                        .ToListAsync();

                var missingCount =
                    materialIds.Count -
                    materials.Count;

                var invoiceLinkedCount =
                    materials.Count(x =>
                        x.InvoiceItems.Any());

                var dispatchLinkedCount =
                    materials.Count(x =>
                        x.Dispatches.Any());

                var blockedMaterials =
                    materials
                        .Where(x =>
                            x.InvoiceItems.Any() ||
                            x.Dispatches.Any())
                        .ToList();

                var deletableMaterials =
                    materials
                        .Where(x =>
                            !x.InvoiceItems.Any() &&
                            !x.Dispatches.Any())
                        .ToList();

                if (deletableMaterials.Count > 0)
                {
                    var deletableIds =
                        deletableMaterials
                            .Select(x => x.Id)
                            .ToList();

                    var alerts =
                        await _context.Alerts
                            .Where(x =>
                                deletableIds.Contains(
                                    x.MaterialId))
                            .ToListAsync();

                    if (alerts.Count > 0)
                    {
                        _context.Alerts
                            .RemoveRange(alerts);
                    }

                    _context.Materials
                        .RemoveRange(
                            deletableMaterials);

                    await _context
                        .SaveChangesAsync();
                }

                await transaction.CommitAsync();

                var deletedCount =
                    deletableMaterials.Count;

                var skippedCount =
                    blockedMaterials.Count;

                if (deletedCount > 0)
                {
                    TempData["Success"] =
                        $"{deletedCount} material(s) deleted successfully.";
                }

                if (skippedCount > 0 ||
                    missingCount > 0)
                {
                    var details =
                        new List<string>();

                    if (invoiceLinkedCount > 0)
                    {
                        details.Add(
                            $"{invoiceLinkedCount} invoice-linked");
                    }

                    if (dispatchLinkedCount > 0)
                    {
                        details.Add(
                            $"{dispatchLinkedCount} dispatch-linked");
                    }

                    if (missingCount > 0)
                    {
                        details.Add(
                            $"{missingCount} not found");
                    }

                    var skippedMessage =
                        $"{skippedCount + missingCount} material(s) " +
                        $"were not deleted ({string.Join(", ", details)}).";

                    if (deletedCount > 0)
                    {
                        TempData["Error"] =
                            skippedMessage;
                    }
                    else
                    {
                        TempData["Error"] =
                            "No selected materials were deleted. " +
                            skippedMessage;
                    }
                }

                if (deletedCount == 0 &&
                    skippedCount == 0 &&
                    missingCount == 0)
                {
                    TempData["Error"] =
                        "No materials were available to delete.";
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to delete the selected materials. " +
                    "No partial deletion was saved. Error: " +
                    ex.Message;
            }

            return RedirectToMaterialList(
                returnUrl);
        }

        // ============================================================
        // DELETE
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var material =
                await _context.Materials
                    .Include(x => x.InvoiceItems)
                    .Include(x => x.Dispatches)
                    .FirstOrDefaultAsync(x =>
                        x.Id == id);

            if (material == null)
            {
                return RedirectToAction(nameof(Index));
            }

            if (material.InvoiceItems.Any())
            {
                TempData["Error"] =
                    "Cannot delete this material because invoice items are already linked.";

                return RedirectToAction(nameof(Index));
            }

            if (material.Dispatches.Any())
            {
                TempData["Error"] =
                    "Cannot delete this material because dispatch records are already linked.";

                return RedirectToAction(nameof(Index));
            }

            var alerts =
                await _context.Alerts
                    .Where(x =>
                        x.MaterialId == id)
                    .ToListAsync();

            if (alerts.Any())
            {
                _context.Alerts.RemoveRange(alerts);
            }

            _context.Materials.Remove(material);
            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Material deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            var material =
                await _context.Materials
                    .AsNoTracking()
                    .Include(x => x.PurchaseOrder)
                    .ThenInclude(x => x!.Purchaser)
                    .Include(x => x.InvoiceItems)
                    .Include(x => x.Dispatches)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(x =>
                        x.Id == id);

            if (material == null)
            {
                return NotFound();
            }

            ViewBag.StockInfo =
                await GetStockInfoAsync(
                    material.MaterialNo);

            return View(material);
        }

        private IActionResult RedirectToMaterialList(
            string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(
                    returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(
                    returnUrl);
            }

            return RedirectToAction(
                nameof(Index));
        }

        // ============================================================
        // MATERIAL QUERY
        // ============================================================

        private IQueryable<Material> CreateMaterialQuery()
        {
            return _context.Materials
                .AsNoTracking()
                .Include(x => x.PurchaseOrder)
                .ThenInclude(x => x!.Purchaser);
        }

        private static IQueryable<Material>
            ApplyMaterialFilters(
                IQueryable<Material> query,
                string? search,
                string? poNumber,
                string? handler,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo,
                string? invoiceStatus,
                string? dispatchStatus)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(x =>
                    (x.MaterialNo != null &&
                     x.MaterialNo.Contains(search)) ||
                    (x.LineItem != null &&
                     x.LineItem.Contains(search)) ||
                    (x.HsnSacCode != null &&
                     x.HsnSacCode.Contains(search)) ||
                    (x.ShortDescription != null &&
                     x.ShortDescription.Contains(search)) ||
                    (x.BriefDescription != null &&
                     x.BriefDescription.Contains(search)) ||
                    (x.DrawingNo != null &&
                     x.DrawingNo.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(poNumber))
            {
                poNumber = poNumber.Trim();

                query = query.Where(x =>
                    x.PurchaseOrder != null &&
                    x.PurchaseOrder.PONumber
                        .Contains(poNumber));
            }

            if (!string.IsNullOrWhiteSpace(handler))
            {
                handler = handler.Trim();

                query = query.Where(x =>
                    x.PurchaseOrder != null &&
                    x.PurchaseOrder.BuyerContactPerson != null &&
                    x.PurchaseOrder.BuyerContactPerson
                        .Contains(handler));
            }

            if (poDateFrom.HasValue)
            {
                var date = poDateFrom.Value.Date;

                query = query.Where(x =>
                    (x.PurchaseOrder != null &&
                     x.PurchaseOrder.PODate.HasValue &&
                     x.PurchaseOrder.PODate.Value >= date) ||
                    ((x.PurchaseOrder == null ||
                      !x.PurchaseOrder.PODate.HasValue) &&
                     x.PODate.HasValue &&
                     x.PODate.Value >= date));
            }

            if (poDateTo.HasValue)
            {
                var endExclusive =
                    poDateTo.Value.Date.AddDays(1);

                query = query.Where(x =>
                    (x.PurchaseOrder != null &&
                     x.PurchaseOrder.PODate.HasValue &&
                     x.PurchaseOrder.PODate.Value < endExclusive) ||
                    ((x.PurchaseOrder == null ||
                      !x.PurchaseOrder.PODate.HasValue) &&
                     x.PODate.HasValue &&
                     x.PODate.Value < endExclusive));
            }

            if (poReceivedDateFrom.HasValue)
            {
                var date =
                    poReceivedDateFrom.Value.Date;

                query = query.Where(x =>
                    (x.PurchaseOrder != null &&
                     x.PurchaseOrder.ReceivedDate.HasValue &&
                     x.PurchaseOrder.ReceivedDate.Value >= date) ||
                    ((x.PurchaseOrder == null ||
                      !x.PurchaseOrder.ReceivedDate.HasValue) &&
                     x.ReceivingDate.HasValue &&
                     x.ReceivingDate.Value >= date));
            }

            if (poReceivedDateTo.HasValue)
            {
                var endExclusive =
                    poReceivedDateTo.Value.Date
                        .AddDays(1);

                query = query.Where(x =>
                    (x.PurchaseOrder != null &&
                     x.PurchaseOrder.ReceivedDate.HasValue &&
                     x.PurchaseOrder.ReceivedDate.Value < endExclusive) ||
                    ((x.PurchaseOrder == null ||
                      !x.PurchaseOrder.ReceivedDate.HasValue) &&
                     x.ReceivingDate.HasValue &&
                     x.ReceivingDate.Value < endExclusive));
            }

            if (!string.IsNullOrWhiteSpace(
                    invoiceStatus))
            {
                query = query.Where(x =>
                    x.InvoiceStatus ==
                    invoiceStatus);
            }

            if (!string.IsNullOrWhiteSpace(
                    dispatchStatus))
            {
                query = query.Where(x =>
                    x.DispatchStatus ==
                    dispatchStatus);
            }

            return query;
        }

        // ============================================================
        // MAP MATERIAL + STOCK API TO VIEWMODEL
        // ============================================================

        private static MaterialIndexRowViewModel
            MapMaterialRow(
                Material material,
                StockInfoDto? stockInfo)
        {
            var stockSnapshot =
                BuildStockSnapshot(
                    stockInfo,
                    material.PendingQty);

            return new MaterialIndexRowViewModel
            {
                MaterialId =
                    material.Id,

                PurchaseOrderId =
                    material.PurchaseOrderId,

                PONumber =
                    material.PurchaseOrder?
                        .PONumber ?? "",

                Handler =
                    material.PurchaseOrder?
                        .BuyerContactPerson ?? "",

                PODate =
                    material.PurchaseOrder?
                        .PODate ??
                    material.PODate,

                POReceivedDate =
                    material.PurchaseOrder?
                        .ReceivedDate ??
                    material.ReceivingDate,

                LineItem =
                    material.LineItem ?? "",

                MaterialNo =
                    material.MaterialNo ?? "",

                HsnSacCode =
                    material.HsnSacCode,

                Description =
                    !string.IsNullOrWhiteSpace(
                        material.ShortDescription)
                        ? material.ShortDescription
                        : material.BriefDescription ?? "",

                BriefDescription =
                    material.BriefDescription,

                Unit =
                    material.Unit ?? "",

                UnitPrice =
                    material.Cost,

                PriceUnit =
                    material.PriceUnit ?? "", 

                DrawingNo =
                    material.DrawingNo,

                POQuantity =
                    material.Qty,

                InvoiceQuantity =
                    material.InvoiceQty,

                BalanceQuantity =
                    Math.Max(
                        0,
                        material.BalanceQty),

                DispatchQuantity =
                    material.DispatchQty,

                PendingDispatchQuantity =
                    Math.Max(
                        0,
                        material.PendingQty),

                LiveStockQuantity =
                    stockSnapshot.LiveStock,

                ShortageQuantity =
                    stockSnapshot.Shortage,

                InvoiceStatus =
                    string.IsNullOrWhiteSpace(
                        material.InvoiceStatus)
                        ? "Not Invoiced"
                        : material.InvoiceStatus,

                DispatchStatus =
                    string.IsNullOrWhiteSpace(
                        material.DispatchStatus)
                        ? "Pending"
                        : material.DispatchStatus,

                StockStatus =
                    stockSnapshot.Status,

                ProcessStatus =
                    string.IsNullOrWhiteSpace(
                        material.ProcessStatus)
                        ? "Pending"
                        : material.ProcessStatus,

                DrawingFilePath =
                    material.DrawingFilePath,

                PdfFilePath =
                    material.PDFFilePath
            };
        }

        private static async Task<MaterialIndexSummaryViewModel>
            BuildMaterialDatabaseSummaryAsync(
                IQueryable<Material> query)
        {
            var totals = await query
                .GroupBy(x => 1)
                .Select(group => new
                {
                    TotalMaterials = group.Count(),

                    PendingDispatchMaterials =
                        group.Count(x =>
                            x.PendingQty > 0),

                    NotInvoicedMaterials =
                        group.Count(x =>
                            x.InvoiceQty <= 0 ||
                            x.InvoiceStatus ==
                                "Not Invoiced"),

                    TotalPOQuantity =
                        group.Sum(x => x.Qty),

                    TotalBalanceQuantity =
                        group.Sum(x =>
                            x.BalanceQty > 0
                                ? x.BalanceQty
                                : 0m),

                    TotalPendingDispatchQuantity =
                        group.Sum(x =>
                            x.PendingQty > 0
                                ? x.PendingQty
                                : 0m)
                })
                .FirstOrDefaultAsync();

            return new MaterialIndexSummaryViewModel
            {
                TotalMaterials =
                    totals?.TotalMaterials ?? 0,

                PendingDispatchMaterials =
                    totals?.PendingDispatchMaterials ?? 0,

                NotInvoicedMaterials =
                    totals?.NotInvoicedMaterials ?? 0,

                TotalPOQuantity =
                    totals?.TotalPOQuantity ?? 0m,

                TotalBalanceQuantity =
                    totals?.TotalBalanceQuantity ?? 0m,

                TotalPendingDispatchQuantity =
                    totals?.TotalPendingDispatchQuantity ?? 0m
            };
        }

        private static MaterialIndexSummaryViewModel
            BuildMaterialSummary(
                IReadOnlyCollection<
                    MaterialIndexRowViewModel> rows)
        {
            return new MaterialIndexSummaryViewModel
            {
                TotalMaterials =
                    rows.Count,

                PendingDispatchMaterials =
                    rows.Count(x =>
                        x.PendingDispatchQuantity > 0),

                NotInvoicedMaterials =
                    rows.Count(x =>
                        x.InvoiceQuantity <= 0 ||
                        x.InvoiceStatus.Equals(
                            "Not Invoiced",
                            StringComparison.OrdinalIgnoreCase)),

                InStockMaterials =
                    rows.Count(x =>
                        x.LiveStockQuantity > 0),

                ShortageMaterials =
                    rows.Count(x =>
                        x.ShortageQuantity > 0),

                TotalPOQuantity =
                    rows.Sum(x =>
                        x.POQuantity),

                TotalBalanceQuantity =
                    rows.Sum(x =>
                        x.BalanceQuantity),

                TotalPendingDispatchQuantity =
                    rows.Sum(x =>
                        x.PendingDispatchQuantity),

                TotalLiveStockQuantity =
                    rows.Sum(x =>
                        x.LiveStockQuantity)
            };
        }

        // ============================================================
        // FILTER DROPDOWN DATA
        // ============================================================

        private static async Task<
            MaterialFilterDataViewModel>
            LoadMaterialFilterOptionsAsync(
                IQueryable<Material> baseQuery)
        {
            var handlers =
                await baseQuery
                    .Where(x =>
                        x.PurchaseOrder != null &&
                        x.PurchaseOrder
                            .BuyerContactPerson != null &&
                        x.PurchaseOrder
                            .BuyerContactPerson != "")
                    .GroupBy(x =>
                        x.PurchaseOrder!
                            .BuyerContactPerson!)
                    .Select(group => new
                    {
                        Value = group.Key,
                        Count = group.Count()
                    })
                    .OrderBy(x => x.Value)
                    .ToListAsync();

            var invoiceStatuses =
                await baseQuery
                    .Where(x =>
                        x.InvoiceStatus != null &&
                        x.InvoiceStatus != "")
                    .GroupBy(x =>
                        x.InvoiceStatus)
                    .Select(group => new
                    {
                        Value = group.Key,
                        Count = group.Count()
                    })
                    .OrderBy(x => x.Value)
                    .ToListAsync();

            var dispatchStatuses =
                await baseQuery
                    .Where(x =>
                        x.DispatchStatus != null &&
                        x.DispatchStatus != "")
                    .GroupBy(x =>
                        x.DispatchStatus)
                    .Select(group => new
                    {
                        Value = group.Key,
                        Count = group.Count()
                    })
                    .OrderBy(x => x.Value)
                    .ToListAsync();

            return new MaterialFilterDataViewModel
            {
                Handlers = handlers
                    .Select(x =>
                        new MaterialFilterOptionViewModel
                        {
                            Value = x.Value,
                            Text = x.Value,
                            Count = x.Count
                        })
                    .ToList(),

                InvoiceStatuses = invoiceStatuses
                    .Select(x =>
                        new MaterialFilterOptionViewModel
                        {
                            Value = x.Value,
                            Text = x.Value,
                            Count = x.Count
                        })
                    .ToList(),

                DispatchStatuses = dispatchStatuses
                    .Select(x =>
                        new MaterialFilterOptionViewModel
                        {
                            Value = x.Value,
                            Text = x.Value,
                            Count = x.Count
                        })
                    .ToList()
            };
        }

        private static List<
            MaterialFilterOptionViewModel>
            BuildStockStatusOptions()
        {
            return new List<MaterialFilterOptionViewModel>
            {
                new()
                {
                    Value = "Available",
                    Text = "Available"
                },
                new()
                {
                    Value = "Shortage",
                    Text = "Shortage"
                },
                new()
                {
                    Value = "Out of Stock",
                    Text = "Out of Stock"
                },
                new()
                {
                    Value = "Not Found",
                    Text = "Not Found"
                }
            };
        }

        private static void MarkSelectedOptions(
            MaterialIndexViewModel model)
        {
            MarkSelected(
                model.Handlers,
                model.Handler);

            MarkSelected(
                model.InvoiceStatuses,
                model.InvoiceStatus);

            MarkSelected(
                model.DispatchStatuses,
                model.DispatchStatus);

            MarkSelected(
                model.StockStatuses,
                model.StockStatus);
        }

        private static void MarkSelected(
            IEnumerable<
                MaterialFilterOptionViewModel> options,
            string? selectedValue)
        {
            foreach (var option in options)
            {
                option.IsSelected =
                    !string.IsNullOrWhiteSpace(
                        selectedValue) &&
                    string.Equals(
                        option.Value,
                        selectedValue,
                        StringComparison.OrdinalIgnoreCase);
            }
        }

        private static int NormalizePageSize(
            int pageSize)
        {
            var sizes =
                new[] { 10, 25, 50, 100, 200 };

            return sizes.Contains(pageSize)
                ? pageSize
                : 10;
        }

        // ============================================================
        // STOCK API
        // Reflection is used only to support different StockInfoDto
        // property names without breaking your existing API model.
        // ============================================================

        private async Task<
            Dictionary<string, StockInfoDto>>
            GetStockMapAsync(
                IEnumerable<Material> materials)
        {
            var stockMap =
                new Dictionary<string, StockInfoDto>(
                    StringComparer.OrdinalIgnoreCase);

            var materialNumbers = materials
                .Select(x => x.MaterialNo)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Stock API calls are independent. Run a small controlled
            // number in parallel so large material pages do not wait
            // for every network request one-by-one.
            using var concurrencyGate =
                new SemaphoreSlim(
                    initialCount: 8,
                    maxCount: 8);

            var stockTasks = materialNumbers
                .Select(async materialNo =>
                {
                    await concurrencyGate.WaitAsync();

                    try
                    {
                        var stockInfo =
                            await GetStockInfoAsync(materialNo);

                        return new
                        {
                            MaterialNo = materialNo,
                            StockInfo = stockInfo
                        };
                    }
                    finally
                    {
                        concurrencyGate.Release();
                    }
                })
                .ToList();

            var stockResults =
                await Task.WhenAll(stockTasks);

            foreach (var result in stockResults)
            {
                if (result.StockInfo != null)
                {
                    stockMap[result.MaterialNo] =
                        result.StockInfo;
                }
            }

            return stockMap;
        }

        private static StockInfoDto?
            GetStockForMaterial(
                IReadOnlyDictionary<
                    string,
                    StockInfoDto> stockMap,
                string? materialNo)
        {
            if (string.IsNullOrWhiteSpace(
                    materialNo))
            {
                return null;
            }

            return stockMap.TryGetValue(
                materialNo,
                out var stockInfo)
                ? stockInfo
                : null;
        }

        private async Task<StockInfoDto?>
            GetStockInfoAsync(
                string? materialNo)
        {
            if (string.IsNullOrWhiteSpace(
                    materialNo))
            {
                return null;
            }

            try
            {
                var client =
                    _httpClientFactory
                        .CreateClient("StockApi");

                var response =
                    await client.GetAsync(
                        $"api/stock/check/{Uri.EscapeDataString(materialNo)}");

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json =
                    await response.Content
                        .ReadAsStringAsync();

                return JsonSerializer
                    .Deserialize<StockInfoDto>(
                        json,
                        StockJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static StockSnapshot
            BuildStockSnapshot(
                StockInfoDto? stockInfo,
                decimal requiredQuantity)
        {
            if (stockInfo == null)
            {
                return new StockSnapshot
                {
                    LiveStock = 0,
                    Shortage = Math.Max(
                        0,
                        requiredQuantity),
                    Status = "Not Found"
                };
            }

            var exists =
                ReadBooleanProperty(
                    stockInfo,
                    true,
                    "Exists",
                    "Found",
                    "IsFound",
                    "Success");

            if (!exists)
            {
                return new StockSnapshot
                {
                    LiveStock = 0,
                    Shortage = Math.Max(
                        0,
                        requiredQuantity),
                    Status = "Not Found"
                };
            }

            var liveStock =
                Math.Max(
                    0,
                    ReadDecimalProperty(
                        stockInfo,
                        "CurrentStock",
                        "AvailableStock",
                        "AvailableQuantity",
                        "StockQty",
                        "Quantity",
                        "CurrentQty",
                        "BalanceQty"));

            var shortage =
                ReadDecimalProperty(
                    stockInfo,
                    "ShortageQty",
                    "ShortageQuantity",
                    "RequiredShortage");

            if (shortage <= 0)
            {
                shortage = Math.Max(
                    0,
                    requiredQuantity -
                    liveStock);
            }

            var apiStatus =
                ReadStringProperty(
                    stockInfo,
                    "StockStatus",
                    "AvailabilityStatus",
                    "Status");

            string status;

            if (!string.IsNullOrWhiteSpace(
                    apiStatus) &&
                apiStatus.Contains(
                    "not found",
                    StringComparison.OrdinalIgnoreCase))
            {
                status = "Not Found";
            }
            else if (liveStock <= 0)
            {
                status = "Out of Stock";
            }
            else if (shortage > 0)
            {
                status = "Shortage";
            }
            else
            {
                status = "Available";
            }

            return new StockSnapshot
            {
                LiveStock = liveStock,
                Shortage = Math.Max(
                    0,
                    shortage),
                Status = status
            };
        }

        private static decimal ReadDecimalProperty(
            object source,
            params string[] propertyNames)
        {
            var value =
                ReadPropertyValue(
                    source,
                    propertyNames);

            if (value == null)
            {
                return 0;
            }

            if (value is decimal decimalValue)
            {
                return decimalValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            if (value is double doubleValue)
            {
                return Convert.ToDecimal(
                    doubleValue);
            }

            if (value is float floatValue)
            {
                return Convert.ToDecimal(
                    floatValue);
            }

            return decimal.TryParse(
                Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : 0;
        }

        private static bool ReadBooleanProperty(
            object source,
            bool defaultValue,
            params string[] propertyNames)
        {
            var value =
                ReadPropertyValue(
                    source,
                    propertyNames);

            if (value == null)
            {
                return defaultValue;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            return bool.TryParse(
                value.ToString(),
                out var parsed)
                ? parsed
                : defaultValue;
        }

        private static string?
            ReadStringProperty(
                object source,
                params string[] propertyNames)
        {
            return ReadPropertyValue(
                    source,
                    propertyNames)?
                .ToString();
        }

        private static object?
            ReadPropertyValue(
                object source,
                IEnumerable<string> propertyNames)
        {
            var type = source.GetType();

            foreach (var propertyName in propertyNames)
            {
                var property =
                    type.GetProperty(
                        propertyName,
                        BindingFlags.Public |
                        BindingFlags.Instance |
                        BindingFlags.IgnoreCase);

                if (property != null)
                {
                    return property.GetValue(source);
                }
            }

            return null;
        }

        // ============================================================
        // SCHEDULE / STATUS RULES
        // ============================================================

        private static void ApplyScheduleRules(
            Material material)
        {
            if (material.DeliveryDate.HasValue)
            {
                material.ProcessStartDate =
                    material.DeliveryDate.Value
                        .AddMonths(-1);

                material.AlertDate =
                    material.ProcessStartDate;
            }
            else
            {
                material.ProcessStartDate = null;
                material.AlertDate = null;
            }

            material.PendingQty =
                material.Qty -
                material.DispatchQty;

            if (material.PendingQty < 0)
            {
                material.PendingQty = 0;
            }

            if (material.DispatchQty <= 0)
            {
                material.DispatchStatus =
                    "Pending";
            }
            else if (
                material.PendingQty <= 0 ||
                material.DispatchQty >=
                material.Qty)
            {
                material.DispatchStatus =
                    "Dispatched";
            }
            else
            {
                material.DispatchStatus =
                    "Partial";
            }

            material.BalanceQty =
                material.Qty -
                material.InvoiceQty;

            if (material.BalanceQty < 0)
            {
                material.BalanceQty = 0;
            }

            if (material.InvoiceQty <= 0)
            {
                material.InvoiceStatus =
                    "Not Invoiced";
            }
            else if (
                material.BalanceQty <= 0 ||
                material.InvoiceQty >=
                material.Qty)
            {
                material.InvoiceStatus =
                    "Fully Invoiced";
            }
            else
            {
                material.InvoiceStatus =
                    "Partially Invoiced";
            }
        }

        private async Task
            CreateOrUpdateProcessAlert(
                Material material)
        {
            var existingAlert =
                await _context.Alerts
                    .FirstOrDefaultAsync(x =>
                        x.MaterialId ==
                        material.Id &&
                        x.AlertType ==
                        "Process Start");

            if (material.AlertDate.HasValue)
            {
                if (existingAlert == null)
                {
                    var alert = new Alert
                    {
                        MaterialId =
                            material.Id,

                        AlertType =
                            "Process Start",

                        AlertDate =
                            material.AlertDate.Value,

                        Message =
                            $"Start process for {material.MaterialNo} before delivery date {material.DeliveryDate?.ToString("dd-MM-yyyy")}"
                    };

                    _context.Alerts.Add(alert);
                }
                else
                {
                    existingAlert.AlertDate =
                        material.AlertDate.Value;

                    existingAlert.Message =
                        $"Start process for {material.MaterialNo} before delivery date {material.DeliveryDate?.ToString("dd-MM-yyyy")}";

                    _context.Alerts.Update(
                        existingAlert);
                }
            }
            else if (existingAlert != null)
            {
                _context.Alerts.Remove(
                    existingAlert);
            }

            await _context.SaveChangesAsync();
        }

        // ============================================================
        // PURCHASE ORDER DROPDOWN
        // ============================================================

        private void LoadPurchaseOrderDropdown(
            int? selectedPurchaseOrderId = null)
        {
            ViewBag.PurchaseOrders =
                new SelectList(
                    _context.PurchaseOrders
                        .OrderByDescending(x => x.Id),
                    "Id",
                    "PONumber",
                    selectedPurchaseOrderId);
        }

        // ============================================================
        // FILE UPLOAD
        // ============================================================

        private async Task<string> SaveFile(
            IFormFile file,
            string folderName)
        {
            var uploadFolder =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    folderName);

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(
                    uploadFolder);
            }

            var safeExtension =
                Path.GetExtension(
                    file.FileName);

            var fileName =
                Guid.NewGuid() +
                safeExtension;

            var filePath =
                Path.Combine(
                    uploadFolder,
                    fileName);

            await using var stream =
                new FileStream(
                    filePath,
                    FileMode.Create);

            await file.CopyToAsync(stream);

            return
                $"/uploads/{folderName}/{fileName}";
        }

        // ============================================================
        // EXCEL HELPERS
        // ============================================================

        private static void CreateMaterialExcelTitle(
            IXLWorksheet worksheet,
            int rowCount)
        {
            worksheet.Cell("A1").Value =
                "PO TRACKER - MATERIAL LIST";

            worksheet.Range("A1:V1").Merge();

            worksheet.Cell("A2").Value =
                $"Generated: {DateTime.Now:dd-MM-yyyy hh:mm tt}";

            worksheet.Range("A2:T2").Merge();

            worksheet.Cell("A3").Value =
                $"Total materials: {rowCount}";

            worksheet.Range("A3:V3").Merge();
        }

        private static void WriteMaterialExcelHeaders(
            IXLWorksheet worksheet)
        {
            var headers = new[]
            {
                "S.No",
                "PO Number",
                "Handler / Person",
                "PO Date",
                "PO Received Date",
                "Line Item",
                "Material Number",
                "HSN/SAC",
                "Description",
                "PO Qty",
                "Unit Price",
                "Price Unit",
                "Invoice Qty",
                "Balance Qty",
                "Dispatch Qty",
                "Pending Dispatch Qty",
                "Invoice Status",
                "Dispatch Status",
                "Live Stock",
                "Shortage",
                "Stock Status",
                "Drawing Number"
            };

            for (var index = 0;
                 index < headers.Length;
                 index++)
            {
                worksheet.Cell(
                    5,
                    index + 1).Value =
                    headers[index];
            }
        }

        private static void WriteMaterialExcelRows(
            IXLWorksheet worksheet,
            IReadOnlyList<
                MaterialIndexRowViewModel> rows)
        {
            var excelRow = 6;

            for (var index = 0;
                 index < rows.Count;
                 index++)
            {
                var item = rows[index];

                worksheet.Cell(excelRow, 1).Value =
                    index + 1;

                worksheet.Cell(excelRow, 2).Value =
                    item.PONumber;

                worksheet.Cell(excelRow, 3).Value =
                    string.IsNullOrWhiteSpace(
                        item.Handler)
                        ? "Unassigned"
                        : item.Handler;

                if (item.PODate.HasValue)
                {
                    worksheet.Cell(excelRow, 4).Value =
                        item.PODate.Value;
                }

                if (item.POReceivedDate.HasValue)
                {
                    worksheet.Cell(excelRow, 5).Value =
                        item.POReceivedDate.Value;
                }

                worksheet.Cell(excelRow, 6).Value =
                    item.LineItem;

                worksheet.Cell(excelRow, 7).Value =
                    item.MaterialNo;

                worksheet.Cell(excelRow, 8).Value =
                    item.HsnSacCode ?? "";

                worksheet.Cell(excelRow, 9).Value =
                    item.Description;
                worksheet.Cell(excelRow, 10).Value =
                    item.POQuantity;

                worksheet.Cell(excelRow, 11).Value =
                    item.UnitPrice;

                worksheet.Cell(excelRow, 12).Value =
                    item.PriceUnit;

                worksheet.Cell(excelRow, 13).Value =
                    item.InvoiceQuantity;

                worksheet.Cell(excelRow, 14).Value =
                    item.BalanceQuantity;

                worksheet.Cell(excelRow, 15).Value =
                    item.DispatchQuantity;

                worksheet.Cell(excelRow, 16).Value =
                    item.PendingDispatchQuantity;

                worksheet.Cell(excelRow, 17).Value =
                    item.InvoiceStatus;

                worksheet.Cell(excelRow, 18).Value =
                    item.DispatchStatus;

                worksheet.Cell(excelRow, 19).Value =
                    item.LiveStockQuantity;

                worksheet.Cell(excelRow, 20).Value =
                    item.ShortageQuantity;

                worksheet.Cell(excelRow, 21).Value =
                    item.StockStatus;

                worksheet.Cell(excelRow, 22).Value =
                    item.DrawingNo ?? "";

                StyleMaterialExcelStatus(
                    worksheet,
                    excelRow,
                    item);

                excelRow++;
            }
        }

        private static void StyleMaterialExcel(
            IXLWorksheet worksheet,
            int rowCount)
        {
            var titleRange =
                worksheet.Range("A1:V1");

            titleRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#4C2FD4");

            titleRange.Style.Font.FontColor =
                XLColor.White;

            titleRange.Style.Font.Bold = true;
            titleRange.Style.Font.FontSize = 16;

            titleRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Range("A2:V3")
                .Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            var header =
                worksheet.Range("A5:V5");

            header.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#EDE9FE");

            header.Style.Font.FontColor =
                XLColor.FromHtml("#2D1B69");

            header.Style.Font.Bold = true;

            header.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            if (rowCount > 0)
            {
                worksheet.Range(
                        6,
                        4,
                        5 + rowCount,
                        5)
                    .Style.DateFormat.Format =
                    "dd-mmm-yyyy";

                worksheet.Range(
                        6,
                        10,
                        5 + rowCount,
                        18)
                    .Style.NumberFormat.Format =
                    "0.00";

                worksheet.Range(
                        6,
                        1,
                        5 + rowCount,
                        22)
                    .Style.Border.BottomBorder =
                    XLBorderStyleValues.Hair;
            }

            worksheet.SheetView.FreezeRows(5);
            worksheet.RangeUsed()?.SetAutoFilter();

            worksheet.Columns(1, 22)
                .AdjustToContents();

            worksheet.Column(3).Width = 22;
            worksheet.Column(9).Width = 35;
            worksheet.Column(9)
                .Style.Alignment.WrapText = true;
            worksheet.Column(17).Width = 20;
            worksheet.Column(18).Width = 18;
            worksheet.Column(21).Width = 15;
            worksheet.Column(22).Width = 24;
        }

        private static void StyleMaterialExcelStatus(
            IXLWorksheet worksheet,
            int row,
            MaterialIndexRowViewModel item)
        {
            var stockCell =
                worksheet.Cell(row, 21);

            if (item.StockStatus == "Available")
            {
                stockCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#DCFCE7");

                stockCell.Style.Font.FontColor =
                    XLColor.FromHtml("#166534");
            }
            else if (
                item.StockStatus == "Shortage")
            {
                stockCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#FEF3C7");

                stockCell.Style.Font.FontColor =
                    XLColor.FromHtml("#92400E");
            }
            else
            {
                stockCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#FFE4E6");

                stockCell.Style.Font.FontColor =
                    XLColor.FromHtml("#9F1239");
            }

            stockCell.Style.Font.Bold = true;
        }

        // ============================================================
        // PRIVATE SUPPORT MODELS
        // ============================================================

        private sealed class
            MaterialFilterDataViewModel
        {
            public List<
                MaterialFilterOptionViewModel>
                Handlers
            { get; set; } = new();

            public List<
                MaterialFilterOptionViewModel>
                InvoiceStatuses
            { get; set; } = new();

            public List<
                MaterialFilterOptionViewModel>
                DispatchStatuses
            { get; set; } = new();
        }

        private sealed class StockSnapshot
        {
            public decimal LiveStock { get; set; }

            public decimal Shortage { get; set; }

            public string Status { get; set; } =
                "Not Found";
        }
    }
}





