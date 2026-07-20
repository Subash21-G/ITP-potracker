using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;
using POTracker.ViewModels;

namespace POTracker.Controllers
{
    public class PurchaseOrderController : Controller
    {
        private const string DefaultPurchaserName =
            "WEG Industries (India) PVT LTD";

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PurchaseOrderController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // ============================================================
        // LOGIN CHECK
        // ============================================================

        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetString("UserName") != null;
        }

        // ============================================================
        // INDEX
        // Premium PO list, filters, summary and pagination.
        // Purchaser is intentionally not shown.
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            string? handler,
            DateTime? poDateFrom,
            DateTime? poDateTo,
            DateTime? poReceivedDateFrom,
            DateTime? poReceivedDateTo,
            string? status,
            string? materialStatus,
            int page = 1,
            int pageSize = 10)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            page = Math.Max(1, page);
            pageSize = NormalizePageSize(pageSize);

            var basePurchaseOrderQuery =
                _context.PurchaseOrders
                    .AsNoTracking()
                    .AsQueryable();

            var handlerOptions =
                await LoadHandlerOptionsAsync(
                    basePurchaseOrderQuery);

            var filteredPurchaseOrderQuery =
                ApplyPurchaseOrderFilters(
                    basePurchaseOrderQuery,
                    search,
                    handler,
                    poDateFrom,
                    poDateTo,
                    poReceivedDateFrom,
                    poReceivedDateTo);

            var aggregateQuery =
                CreatePurchaseOrderAggregateQuery(
                    filteredPurchaseOrderQuery);

            aggregateQuery =
                ApplyPurchaseOrderAggregateFilters(
                    aggregateQuery,
                    status,
                    materialStatus);

            // Load one compact aggregate row per filtered PO for the
            // summary cards and status-option counts. Material entity
            // rows themselves are not loaded into application memory.
            var summaryAggregateRows = await aggregateQuery
                .ToListAsync();

            var allSummaryRows = summaryAggregateRows
                .Select(MapPurchaseOrderAggregateRow)
                .ToList();

            var totalRecords = allSummaryRows.Count;

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalRecords /
                    (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var aggregateRows = await aggregateQuery
                .OrderByDescending(x =>
                    x.POReceivedDate ??
                    x.PODate ??
                    x.CreatedDate)
                .ThenByDescending(x =>
                    x.PurchaseOrderId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagedRows = aggregateRows
                .Select(MapPurchaseOrderAggregateRow)
                .ToList();

            var statusOptions =
                BuildStatusOptions(allSummaryRows);

            var materialStatusOptions =
                BuildMaterialStatusOptions(
                    allSummaryRows);

            var model =
                new PurchaseOrderIndexViewModel
                {
                    Items = pagedRows,

                    Summary =
                        BuildPurchaseOrderSummary(
                            allSummaryRows),

                    Search = search,
                    Handler = handler,

                    PODateFrom = poDateFrom,
                    PODateTo = poDateTo,

                    POReceivedDateFrom =
                        poReceivedDateFrom,

                    POReceivedDateTo =
                        poReceivedDateTo,

                    Status = status,

                    MaterialStatus =
                        materialStatus,

                    Handlers = handlerOptions,

                    Statuses = statusOptions,

                    MaterialStatuses =
                        materialStatusOptions,

                    Page = page,
                    PageSize = pageSize,
                    TotalRecords = totalRecords
                };

            MarkSelectedOptions(model);

            return View(model);
        }

        // ============================================================
        // FILTERED EXCEL EXPORT
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            string? search,
            string? handler,
            DateTime? poDateFrom,
            DateTime? poDateTo,
            DateTime? poReceivedDateFrom,
            DateTime? poReceivedDateTo,
            string? status,
            string? materialStatus)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var purchaseOrders =
                await ApplyPurchaseOrderFilters(
                        _context.PurchaseOrders
                            .AsNoTracking(),
                        search,
                        handler,
                        poDateFrom,
                        poDateTo,
                        poReceivedDateFrom,
                        poReceivedDateTo)
                    .OrderByDescending(x =>
                        x.ReceivedDate ??
                        x.PODate ??
                        x.CreatedDate)
                    .ThenByDescending(x => x.Id)
                    .ToListAsync();

            var rows =
                await BuildPurchaseOrderRowsAsync(
                    purchaseOrders);

            if (!string.IsNullOrWhiteSpace(status))
            {
                rows = rows
                    .Where(x =>
                        string.Equals(
                            x.Status,
                            status,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(
                    materialStatus))
            {
                rows = rows
                    .Where(x =>
                        string.Equals(
                            x.MaterialStatus,
                            materialStatus,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add(
                    "Purchase Orders");

            WritePurchaseOrderExcel(
                worksheet,
                rows);

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Purchase_Orders_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        // ============================================================
        // DETAILS
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var purchaseOrder =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == id);

            if (purchaseOrder == null)
            {
                return NotFound();
            }

            var materials =
                await _context.Materials
                    .AsNoTracking()
                    .Where(x =>
                        x.PurchaseOrderId == id)
                    .OrderBy(x => x.LineItem)
                    .ThenBy(x => x.MaterialNo)
                    .ToListAsync();

            ViewBag.Materials = materials;

            ViewBag.TotalMaterials =
                materials.Count;

            ViewBag.PendingMaterials =
                materials.Count(x =>
                    !IsMaterialCompleted(x));

            ViewBag.CompletedMaterials =
                materials.Count(
                    IsMaterialCompleted);

            ViewBag.OverdueMaterials =
                materials.Count(x =>
                    x.DeliveryDate.HasValue &&
                    x.DeliveryDate.Value.Date <
                    DateTime.Today &&
                    x.PendingQty > 0);

            ViewBag.TotalPOQuantity =
                materials.Sum(x => x.Qty);

            ViewBag.TotalInvoiceBalance =
                materials.Sum(x =>
                    Math.Max(
                        0,
                        x.BalanceQty));

            ViewBag.TotalDispatchBalance =
                materials.Sum(x =>
                    Math.Max(
                        0,
                        x.PendingQty));

            return View(purchaseOrder);
        }

        // ============================================================
        // CREATE
        // Purchaser remains only as an internal database relationship.
        // It is automatically assigned and is not shown on the PO page.
        // ============================================================

        [HttpGet]
        public IActionResult Create()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            return View(new PurchaseOrder
            {
                PODate = DateTime.Today,
                ReceivedDate = DateTime.Today
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            PurchaseOrder purchaseOrder,
            IFormFile? pdfFile)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            purchaseOrder.PONumber =
                purchaseOrder.PONumber?.Trim() ?? "";

            purchaseOrder.BuyerContactPerson =
                purchaseOrder.BuyerContactPerson?
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                    purchaseOrder.PONumber))
            {
                ModelState.AddModelError(
                    nameof(PurchaseOrder.PONumber),
                    "PO Number is required.");
            }

            var duplicateExists =
                await _context.PurchaseOrders
                    .AnyAsync(x =>
                        x.PONumber ==
                        purchaseOrder.PONumber);

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    nameof(PurchaseOrder.PONumber),
                    "This PO Number already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(purchaseOrder);
            }

            purchaseOrder.PurchaserId =
                await GetOrCreateDefaultPurchaserIdAsync(
                    purchaseOrder.BuyerContactPerson);

            purchaseOrder.CreatedDate =
                DateTime.Now;

            if (pdfFile != null &&
                pdfFile.Length > 0)
            {
                purchaseOrder.PdfFilePath =
                    await SavePdfFileAsync(
                        pdfFile);
            }

            _context.PurchaseOrders.Add(
                purchaseOrder);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Purchase Order created successfully.";

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // EDIT
        // PO Date and PO Received Date are reflected to all materials.
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var purchaseOrder =
                await _context.PurchaseOrders
                    .FindAsync(id);

            if (purchaseOrder == null)
            {
                return NotFound();
            }

            return View(purchaseOrder);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            PurchaseOrder purchaseOrder,
            IFormFile? pdfFile)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            if (id != purchaseOrder.Id)
            {
                return NotFound();
            }

            var existingPurchaseOrder =
                await _context.PurchaseOrders
                    .FirstOrDefaultAsync(x =>
                        x.Id == id);

            if (existingPurchaseOrder == null)
            {
                return NotFound();
            }

            purchaseOrder.PONumber =
                purchaseOrder.PONumber?.Trim() ?? "";

            purchaseOrder.BuyerContactPerson =
                purchaseOrder.BuyerContactPerson?
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                    purchaseOrder.PONumber))
            {
                ModelState.AddModelError(
                    nameof(PurchaseOrder.PONumber),
                    "PO Number is required.");
            }

            var duplicateExists =
                await _context.PurchaseOrders
                    .AnyAsync(x =>
                        x.Id != id &&
                        x.PONumber ==
                        purchaseOrder.PONumber);

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    nameof(PurchaseOrder.PONumber),
                    "This PO Number already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(purchaseOrder);
            }

            existingPurchaseOrder.PONumber =
                purchaseOrder.PONumber;

            existingPurchaseOrder.PODate =
                purchaseOrder.PODate;

            existingPurchaseOrder.ReceivedDate =
                purchaseOrder.ReceivedDate;

            existingPurchaseOrder.BuyerContactPerson =
                purchaseOrder.BuyerContactPerson;

            // Preserve the existing purchaser relationship.
            if (existingPurchaseOrder.PurchaserId <= 0)
            {
                existingPurchaseOrder.PurchaserId =
                    await GetOrCreateDefaultPurchaserIdAsync(
                        purchaseOrder.BuyerContactPerson);
            }

            if (pdfFile != null &&
                pdfFile.Length > 0)
            {
                existingPurchaseOrder.PdfFilePath =
                    await SavePdfFileAsync(
                        pdfFile);
            }

            var linkedMaterials =
                await _context.Materials
                    .Where(x =>
                        x.PurchaseOrderId == id)
                    .ToListAsync();

            foreach (var material in linkedMaterials)
            {
                // Date of Order
                material.PODate =
                    existingPurchaseOrder.PODate;

                // Date of emission = PO Received Date
                material.ReceivingDate =
                    existingPurchaseOrder.ReceivedDate;

                // Keep the PO PDF link available
                // from every material record too.
                if (!string.IsNullOrWhiteSpace(
                        existingPurchaseOrder
                            .PdfFilePath))
                {
                    material.PDFFilePath =
                        existingPurchaseOrder
                            .PdfFilePath;
                }
            }

            _context.PurchaseOrders.Update(
                existingPurchaseOrder);

            if (linkedMaterials.Count > 0)
            {
                _context.Materials.UpdateRange(
                    linkedMaterials);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Purchase Order updated successfully. " +
                $"{linkedMaterials.Count} linked material(s) refreshed.";

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // MULTIPLE DELETE
        // POs with linked materials are safely skipped.
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

            var purchaseOrderIds =
                selectedIds?
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList() ??
                new List<int>();

            if (purchaseOrderIds.Count == 0)
            {
                TempData["Error"] =
                    "Select at least one purchase order to delete.";

                return RedirectToPurchaseOrderList(
                    returnUrl);
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var purchaseOrders =
                    await _context.PurchaseOrders
                        .Where(x =>
                            purchaseOrderIds.Contains(x.Id))
                        .ToListAsync();

                var existingIds =
                    purchaseOrders
                        .Select(x => x.Id)
                        .ToList();

                var linkedMaterialCounts =
                    await _context.Materials
                        .Where(x =>
                            existingIds.Contains(
                                x.PurchaseOrderId))
                        .GroupBy(x =>
                            x.PurchaseOrderId)
                        .Select(group => new
                        {
                            PurchaseOrderId =
                                group.Key,

                            MaterialCount =
                                group.Count()
                        })
                        .ToDictionaryAsync(
                            x => x.PurchaseOrderId,
                            x => x.MaterialCount);

                var blockedPurchaseOrders =
                    purchaseOrders
                        .Where(x =>
                            linkedMaterialCounts
                                .ContainsKey(x.Id))
                        .ToList();

                var deletablePurchaseOrders =
                    purchaseOrders
                        .Where(x =>
                            !linkedMaterialCounts
                                .ContainsKey(x.Id))
                        .ToList();

                if (deletablePurchaseOrders.Count > 0)
                {
                    _context.PurchaseOrders
                        .RemoveRange(
                            deletablePurchaseOrders);

                    await _context
                        .SaveChangesAsync();
                }

                await transaction.CommitAsync();

                var deletedCount =
                    deletablePurchaseOrders.Count;

                var skippedCount =
                    blockedPurchaseOrders.Count;

                var missingCount =
                    purchaseOrderIds.Count -
                    purchaseOrders.Count;

                var linkedMaterialCount =
                    blockedPurchaseOrders.Sum(x =>
                        linkedMaterialCounts.TryGetValue(
                            x.Id,
                            out var count)
                            ? count
                            : 0);

                if (deletedCount > 0)
                {
                    TempData["Success"] =
                        $"{deletedCount} purchase order(s) deleted successfully.";
                }

                if (skippedCount > 0 ||
                    missingCount > 0)
                {
                    var reasons =
                        new List<string>();

                    if (skippedCount > 0)
                    {
                        reasons.Add(
                            $"{skippedCount} linked PO(s) with " +
                            $"{linkedMaterialCount} material(s)");
                    }

                    if (missingCount > 0)
                    {
                        reasons.Add(
                            $"{missingCount} PO(s) not found");
                    }

                    var skippedMessage =
                        $"{skippedCount + missingCount} purchase order(s) " +
                        $"were not deleted ({string.Join(", ", reasons)}).";

                    TempData["Error"] =
                        deletedCount > 0
                            ? skippedMessage
                            : "No selected purchase orders were deleted. " +
                              skippedMessage;
                }

                if (deletedCount == 0 &&
                    skippedCount == 0 &&
                    missingCount == 0)
                {
                    TempData["Error"] =
                        "No purchase orders were available to delete.";
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to delete the selected purchase orders. " +
                    "No partial deletion was saved. Error: " +
                    ex.Message;
            }

            return RedirectToPurchaseOrderList(
                returnUrl);
        }

        // ============================================================
        // DELETE
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var purchaseOrder =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == id);

            if (purchaseOrder == null)
            {
                return NotFound();
            }

            ViewBag.MaterialCount =
                await _context.Materials
                    .CountAsync(x =>
                        x.PurchaseOrderId == id);

            return View(purchaseOrder);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteConfirmed(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var purchaseOrder =
                await _context.PurchaseOrders
                    .FindAsync(id);

            if (purchaseOrder == null)
            {
                return RedirectToAction(
                    nameof(Index));
            }

            var materialCount =
                await _context.Materials
                    .CountAsync(x =>
                        x.PurchaseOrderId == id);

            if (materialCount > 0)
            {
                TempData["Error"] =
                    $"Cannot delete PO {purchaseOrder.PONumber}. " +
                    $"{materialCount} material(s) are linked to it.";

                return RedirectToAction(
                    nameof(Index));
            }

            _context.PurchaseOrders.Remove(
                purchaseOrder);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Purchase Order deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private IActionResult RedirectToPurchaseOrderList(
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
        // DATABASE FILTERS
        // ============================================================

        private static IQueryable<PurchaseOrder>
            ApplyPurchaseOrderFilters(
                IQueryable<PurchaseOrder> query,
                string? search,
                string? handler,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(x =>
                    x.PONumber.Contains(search) ||
                    (x.BuyerContactPerson != null &&
                     x.BuyerContactPerson
                         .Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(handler))
            {
                handler = handler.Trim();

                query = query.Where(x =>
                    x.BuyerContactPerson != null &&
                    x.BuyerContactPerson
                        .Contains(handler));
            }

            if (poDateFrom.HasValue)
            {
                var date =
                    poDateFrom.Value.Date;

                query = query.Where(x =>
                    x.PODate.HasValue &&
                    x.PODate.Value >= date);
            }

            if (poDateTo.HasValue)
            {
                var endExclusive =
                    poDateTo.Value.Date.AddDays(1);

                query = query.Where(x =>
                    x.PODate.HasValue &&
                    x.PODate.Value < endExclusive);
            }

            if (poReceivedDateFrom.HasValue)
            {
                var date =
                    poReceivedDateFrom
                        .Value.Date;

                query = query.Where(x =>
                    x.ReceivedDate.HasValue &&
                    x.ReceivedDate.Value >= date);
            }

            if (poReceivedDateTo.HasValue)
            {
                var endExclusive =
                    poReceivedDateTo
                        .Value.Date
                        .AddDays(1);

                query = query.Where(x =>
                    x.ReceivedDate.HasValue &&
                    x.ReceivedDate.Value <
                    endExclusive);
            }

            return query;
        }

        // ============================================================
        // SQL PURCHASE ORDER AGGREGATION
        // ============================================================

        private static IQueryable<PurchaseOrderAggregateRow>
            CreatePurchaseOrderAggregateQuery(
                IQueryable<PurchaseOrder> query)
        {
            var today = DateTime.Today;

            return query.Select(purchaseOrder =>
                new PurchaseOrderAggregateRow
                {
                    PurchaseOrderId =
                        purchaseOrder.Id,

                    PONumber =
                        purchaseOrder.PONumber,

                    Handler =
                        purchaseOrder.BuyerContactPerson ?? "",

                    PODate =
                        purchaseOrder.PODate,

                    POReceivedDate =
                        purchaseOrder.ReceivedDate,

                    PdfFilePath =
                        purchaseOrder.PdfFilePath,

                    CreatedDate =
                        purchaseOrder.CreatedDate,

                    TotalMaterials =
                        purchaseOrder.Materials.Count(),

                    CompletedMaterials =
                        purchaseOrder.Materials.Count(material =>
                            (material.BalanceQty <= 0 ||
                             material.InvoiceStatus ==
                                "Fully Invoiced") &&
                            (material.PendingQty <= 0 ||
                             material.DispatchStatus ==
                                "Dispatched")),

                    NotInvoicedMaterials =
                        purchaseOrder.Materials.Count(material =>
                            material.InvoiceQty <= 0 ||
                            material.InvoiceStatus ==
                                "Not Invoiced"),

                    PendingDispatchMaterials =
                        purchaseOrder.Materials.Count(material =>
                            material.PendingQty > 0 ||
                            material.DispatchStatus !=
                                "Dispatched"),

                    OverdueMaterials =
                        purchaseOrder.Materials.Count(material =>
                            material.DeliveryDate.HasValue &&
                            material.DeliveryDate < today &&
                            material.PendingQty > 0),

                    TotalPOQuantity =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)material.Qty) ?? 0m,

                    TotalInvoiceQuantity =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)material.InvoiceQty) ?? 0m,

                    TotalInvoiceBalanceQuantity =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)(
                                    material.BalanceQty > 0
                                        ? material.BalanceQty
                                        : 0m)) ?? 0m,

                    TotalDispatchQuantity =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)material.DispatchQty) ?? 0m,

                    TotalPendingDispatchQuantity =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)(
                                    material.PendingQty > 0
                                        ? material.PendingQty
                                        : 0m)) ?? 0m,

                    EarliestDeliveryDate =
                        purchaseOrder.Materials
                            .Min(material =>
                                material.DeliveryDate),

                    LatestDeliveryDate =
                        purchaseOrder.Materials
                            .Max(material =>
                                material.DeliveryDate)
                });
        }

        private static IQueryable<PurchaseOrderAggregateRow>
            ApplyPurchaseOrderAggregateFilters(
                IQueryable<PurchaseOrderAggregateRow> query,
                string? status,
                string? materialStatus)
        {
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = status.Trim().ToLowerInvariant() switch
                {
                    "open" => query.Where(x =>
                        x.TotalMaterials <= 0 ||
                        x.CompletedMaterials <= 0),

                    "in progress" => query.Where(x =>
                        x.TotalMaterials > 0 &&
                        x.CompletedMaterials > 0 &&
                        x.CompletedMaterials <
                            x.TotalMaterials),

                    "completed" => query.Where(x =>
                        x.TotalMaterials > 0 &&
                        x.CompletedMaterials >=
                            x.TotalMaterials),

                    _ => query.Where(x => false)
                };
            }

            if (!string.IsNullOrWhiteSpace(materialStatus))
            {
                query = materialStatus.Trim().ToLowerInvariant() switch
                {
                    "no materials" => query.Where(x =>
                        x.TotalMaterials <= 0),

                    "pending" => query.Where(x =>
                        x.TotalMaterials > 0 &&
                        x.CompletedMaterials <= 0),

                    "partially completed" => query.Where(x =>
                        x.TotalMaterials > 0 &&
                        x.CompletedMaterials > 0 &&
                        x.CompletedMaterials <
                            x.TotalMaterials),

                    "completed" => query.Where(x =>
                        x.TotalMaterials > 0 &&
                        x.CompletedMaterials >=
                            x.TotalMaterials),

                    _ => query.Where(x => false)
                };
            }

            return query;
        }

        private static PurchaseOrderIndexRowViewModel
            MapPurchaseOrderAggregateRow(
                PurchaseOrderAggregateRow source)
        {
            var pendingMaterials = Math.Max(
                0,
                source.TotalMaterials -
                source.CompletedMaterials);

            var row = new PurchaseOrderIndexRowViewModel
            {
                PurchaseOrderId =
                    source.PurchaseOrderId,

                PONumber =
                    source.PONumber,

                Handler =
                    source.Handler,

                PODate =
                    source.PODate,

                POReceivedDate =
                    source.POReceivedDate,

                PdfFilePath =
                    source.PdfFilePath,

                CreatedDate =
                    source.CreatedDate,

                TotalMaterials =
                    source.TotalMaterials,

                CompletedMaterials =
                    source.CompletedMaterials,

                PendingMaterials =
                    pendingMaterials,

                NotInvoicedMaterials =
                    source.NotInvoicedMaterials,

                PendingDispatchMaterials =
                    source.PendingDispatchMaterials,

                OverdueMaterials =
                    source.OverdueMaterials,

                TotalPOQuantity =
                    source.TotalPOQuantity,

                TotalInvoiceQuantity =
                    source.TotalInvoiceQuantity,

                TotalInvoiceBalanceQuantity =
                    source.TotalInvoiceBalanceQuantity,

                TotalDispatchQuantity =
                    source.TotalDispatchQuantity,

                TotalPendingDispatchQuantity =
                    source.TotalPendingDispatchQuantity,

                EarliestDeliveryDate =
                    source.EarliestDeliveryDate,

                LatestDeliveryDate =
                    source.LatestDeliveryDate
            };

            row.Status = GetPurchaseOrderStatus(row);

            return row;
        }

        // ============================================================
        // BUILD PO ROWS FROM LINKED MATERIALS
        // ============================================================

        private async Task<List<
            PurchaseOrderIndexRowViewModel>>
            BuildPurchaseOrderRowsAsync(
                IReadOnlyCollection<
                    PurchaseOrder> purchaseOrders)
        {
            if (purchaseOrders.Count == 0)
            {
                return new List<
                    PurchaseOrderIndexRowViewModel>();
            }

            var purchaseOrderIds =
                purchaseOrders
                    .Select(x => x.Id)
                    .ToList();

            var materials =
                await _context.Materials
                    .AsNoTracking()
                    .Where(x =>
                        purchaseOrderIds.Contains(
                            x.PurchaseOrderId))
                    .ToListAsync();

            var materialMap =
                materials
                    .GroupBy(x =>
                        x.PurchaseOrderId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToList());

            var rows =
                new List<
                    PurchaseOrderIndexRowViewModel>();

            foreach (var purchaseOrder
                     in purchaseOrders)
            {
                var poMaterials =
                    materialMap.TryGetValue(
                        purchaseOrder.Id,
                        out var values)
                        ? values
                        : new List<Material>();

                var totalMaterials =
                    poMaterials.Count;

                var completedMaterials =
                    poMaterials.Count(
                        IsMaterialCompleted);

                var pendingMaterials =
                    totalMaterials -
                    completedMaterials;

                var row =
                    new PurchaseOrderIndexRowViewModel
                    {
                        PurchaseOrderId =
                            purchaseOrder.Id,

                        PONumber =
                            purchaseOrder.PONumber,

                        Handler =
                            purchaseOrder
                                .BuyerContactPerson ??
                            "",

                        PODate =
                            purchaseOrder.PODate,

                        POReceivedDate =
                            purchaseOrder
                                .ReceivedDate,

                        PdfFilePath =
                            purchaseOrder
                                .PdfFilePath,

                        CreatedDate =
                            purchaseOrder
                                .CreatedDate,

                        TotalMaterials =
                            totalMaterials,

                        PendingMaterials =
                            pendingMaterials,

                        CompletedMaterials =
                            completedMaterials,

                        NotInvoicedMaterials =
                            poMaterials.Count(x =>
                                x.InvoiceQty <= 0 ||
                                string.Equals(
                                    x.InvoiceStatus,
                                    "Not Invoiced",
                                    StringComparison.OrdinalIgnoreCase)),

                        PendingDispatchMaterials =
                            poMaterials.Count(x =>
                                x.PendingQty > 0 ||
                                !string.Equals(
                                    x.DispatchStatus,
                                    "Dispatched",
                                    StringComparison.OrdinalIgnoreCase)),

                        OverdueMaterials =
                            poMaterials.Count(x =>
                                x.DeliveryDate.HasValue &&
                                x.DeliveryDate.Value.Date <
                                DateTime.Today &&
                                x.PendingQty > 0),

                        TotalPOQuantity =
                            poMaterials.Sum(x =>
                                x.Qty),

                        TotalInvoiceQuantity =
                            poMaterials.Sum(x =>
                                x.InvoiceQty),

                        TotalInvoiceBalanceQuantity =
                            poMaterials.Sum(x =>
                                Math.Max(
                                    0,
                                    x.BalanceQty)),

                        TotalDispatchQuantity =
                            poMaterials.Sum(x =>
                                x.DispatchQty),

                        TotalPendingDispatchQuantity =
                            poMaterials.Sum(x =>
                                Math.Max(
                                    0,
                                    x.PendingQty)),

                        EarliestDeliveryDate =
                            poMaterials
                                .Where(x =>
                                    x.DeliveryDate
                                        .HasValue)
                                .Select(x =>
                                    x.DeliveryDate)
                                .OrderBy(x => x)
                                .FirstOrDefault(),

                        LatestDeliveryDate =
                            poMaterials
                                .Where(x =>
                                    x.DeliveryDate
                                        .HasValue)
                                .Select(x =>
                                    x.DeliveryDate)
                                .OrderByDescending(
                                    x => x)
                                .FirstOrDefault()
                    };

                row.Status =
                    GetPurchaseOrderStatus(
                        row);

                rows.Add(row);
            }

            return rows;
        }

        private static bool IsMaterialCompleted(
            Material material)
        {
            var invoiceCompleted =
                material.BalanceQty <= 0 ||
                string.Equals(
                    material.InvoiceStatus,
                    "Fully Invoiced",
                    StringComparison.OrdinalIgnoreCase);

            var dispatchCompleted =
                material.PendingQty <= 0 ||
                string.Equals(
                    material.DispatchStatus,
                    "Dispatched",
                    StringComparison.OrdinalIgnoreCase);

            return invoiceCompleted &&
                   dispatchCompleted;
        }

        private static string
            GetPurchaseOrderStatus(
                PurchaseOrderIndexRowViewModel row)
        {
            if (row.TotalMaterials <= 0)
            {
                return "Open";
            }

            if (row.PendingMaterials <= 0)
            {
                return "Completed";
            }

            if (row.CompletedMaterials > 0)
            {
                return "In Progress";
            }

            return "Open";
        }

        // ============================================================
        // SUMMARY
        // ============================================================

        private static
            PurchaseOrderIndexSummaryViewModel
            BuildPurchaseOrderSummary(
                IReadOnlyCollection<
                    PurchaseOrderIndexRowViewModel> rows)
        {
            return new
                PurchaseOrderIndexSummaryViewModel
            {
                TotalPurchaseOrders =
                        rows.Count,

                OpenPurchaseOrders =
                        rows.Count(x =>
                            !string.Equals(
                                x.Status,
                                "Completed",
                                StringComparison.OrdinalIgnoreCase)),

                CompletedPurchaseOrders =
                        rows.Count(x =>
                            string.Equals(
                                x.Status,
                                "Completed",
                                StringComparison.OrdinalIgnoreCase)),

                PurchaseOrdersWithPendingMaterials =
                        rows.Count(x =>
                            x.PendingMaterials > 0),

                PurchaseOrdersWithOverdueMaterials =
                        rows.Count(x =>
                            x.OverdueMaterials > 0),

                PurchaseOrdersWithoutHandler =
                        rows.Count(x =>
                            string.IsNullOrWhiteSpace(
                                x.Handler)),

                TotalPOQuantity =
                        rows.Sum(x =>
                            x.TotalPOQuantity),

                TotalInvoiceBalanceQuantity =
                        rows.Sum(x =>
                            x.TotalInvoiceBalanceQuantity),

                TotalPendingDispatchQuantity =
                        rows.Sum(x =>
                            x.TotalPendingDispatchQuantity)
            };
        }

        // ============================================================
        // FILTER OPTIONS
        // ============================================================

        private static async Task<List<
            PurchaseOrderFilterOptionViewModel>>
            LoadHandlerOptionsAsync(
                IQueryable<PurchaseOrder> query)
        {
            return await query
                .Where(x =>
                    x.BuyerContactPerson != null &&
                    x.BuyerContactPerson != "")
                .GroupBy(x =>
                    x.BuyerContactPerson!)
                .Select(group =>
                    new PurchaseOrderFilterOptionViewModel
                    {
                        Value = group.Key,
                        Text = group.Key,
                        Count = group.Count()
                    })
                .OrderBy(x => x.Text)
                .ToListAsync();
        }

        private static List<
            PurchaseOrderFilterOptionViewModel>
            BuildStatusOptions(
                IReadOnlyCollection<
                    PurchaseOrderIndexRowViewModel> rows)
        {
            var values = new[]
            {
                "Open",
                "In Progress",
                "Completed"
            };

            return values
                .Select(value =>
                    new PurchaseOrderFilterOptionViewModel
                    {
                        Value = value,
                        Text = value,
                        Count = rows.Count(x =>
                            string.Equals(
                                x.Status,
                                value,
                                StringComparison.OrdinalIgnoreCase))
                    })
                .ToList();
        }

        private static List<
            PurchaseOrderFilterOptionViewModel>
            BuildMaterialStatusOptions(
                IReadOnlyCollection<
                    PurchaseOrderIndexRowViewModel> rows)
        {
            var values = new[]
            {
                "No Materials",
                "Pending",
                "Partially Completed",
                "Completed"
            };

            return values
                .Select(value =>
                    new PurchaseOrderFilterOptionViewModel
                    {
                        Value = value,
                        Text = value,
                        Count = rows.Count(x =>
                            string.Equals(
                                x.MaterialStatus,
                                value,
                                StringComparison.OrdinalIgnoreCase))
                    })
                .ToList();
        }

        private static void MarkSelectedOptions(
            PurchaseOrderIndexViewModel model)
        {
            MarkSelected(
                model.Handlers,
                model.Handler);

            MarkSelected(
                model.Statuses,
                model.Status);

            MarkSelected(
                model.MaterialStatuses,
                model.MaterialStatus);
        }

        private static void MarkSelected(
            IEnumerable<
                PurchaseOrderFilterOptionViewModel> options,
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
            var supportedSizes =
                new[] { 10, 25, 50, 100, 200 };

            return supportedSizes.Contains(
                pageSize)
                ? pageSize
                : 10;
        }

        // ============================================================
        // DEFAULT PURCHASER
        // Internal relationship only.
        // ============================================================

        private async Task<int>
            GetOrCreateDefaultPurchaserIdAsync(
                string? handler)
        {
            var purchaser =
                await _context.Purchasers
                    .FirstOrDefaultAsync(x =>
                        x.PurchaserName ==
                        DefaultPurchaserName);

            if (purchaser == null)
            {
                purchaser = new Purchaser
                {
                    PurchaserName =
                        DefaultPurchaserName,

                    ContactPerson =
                        handler
                };

                _context.Purchasers.Add(
                    purchaser);

                await _context
                    .SaveChangesAsync();
            }
            else if (
                !string.IsNullOrWhiteSpace(
                    handler))
            {
                purchaser.ContactPerson =
                    handler;

                _context.Purchasers.Update(
                    purchaser);

                await _context
                    .SaveChangesAsync();
            }

            return purchaser.Id;
        }

        // ============================================================
        // PDF FILE
        // ============================================================

        private async Task<string>
            SavePdfFileAsync(
                IFormFile pdfFile)
        {
            var extension =
                Path.GetExtension(
                    pdfFile.FileName);

            if (!string.Equals(
                    extension,
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Only PDF files are allowed.");
            }

            var uploadFolder =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "po");

            Directory.CreateDirectory(
                uploadFolder);

            var safeFileName =
                Path.GetFileName(
                    pdfFile.FileName);

            var fileName =
                $"{Guid.NewGuid():N}_{safeFileName}";

            var fullPath =
                Path.Combine(
                    uploadFolder,
                    fileName);

            await using var stream =
                new FileStream(
                    fullPath,
                    FileMode.Create);

            await pdfFile.CopyToAsync(
                stream);

            return
                "/uploads/po/" +
                fileName;
        }

        // ============================================================
        // EXCEL
        // ============================================================

        private static void WritePurchaseOrderExcel(
            IXLWorksheet worksheet,
            IReadOnlyList<
                PurchaseOrderIndexRowViewModel> rows)
        {
            worksheet.Cell("A1").Value =
                "PO TRACKER - PURCHASE ORDERS";

            worksheet.Range("A1:U1").Merge();

            worksheet.Cell("A2").Value =
                $"Generated: {DateTime.Now:dd-MM-yyyy hh:mm tt}";

            worksheet.Range("A2:U2").Merge();

            worksheet.Cell("A3").Value =
                $"Total purchase orders: {rows.Count}";

            worksheet.Range("A3:U3").Merge();

            var headers = new[]
            {
                "S.No",
                "PO Number",
                "Handler / Person",
                "PO Date",
                "PO Received Date",
                "PO Status",
                "Material Status",
                "Total Materials",
                "Pending Materials",
                "Completed Materials",
                "Not Invoiced Materials",
                "Pending Dispatch Materials",
                "Overdue Materials",
                "Total PO Qty",
                "Invoice Qty",
                "Invoice Balance",
                "Dispatch Qty",
                "Dispatch Balance",
                "Earliest Delivery",
                "Latest Delivery",
                "PDF Available"
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

            var rowNumber = 6;

            for (var index = 0;
                 index < rows.Count;
                 index++)
            {
                var item = rows[index];

                worksheet.Cell(
                    rowNumber,
                    1).Value = index + 1;

                worksheet.Cell(
                    rowNumber,
                    2).Value = item.PONumber;

                worksheet.Cell(
                    rowNumber,
                    3).Value =
                    item.HandlerDisplay;

                if (item.PODate.HasValue)
                {
                    worksheet.Cell(
                        rowNumber,
                        4).Value =
                        item.PODate.Value;
                }

                if (item.POReceivedDate.HasValue)
                {
                    worksheet.Cell(
                        rowNumber,
                        5).Value =
                        item.POReceivedDate.Value;
                }

                worksheet.Cell(rowNumber, 6).Value =
                    item.Status;

                worksheet.Cell(rowNumber, 7).Value =
                    item.MaterialStatus;

                worksheet.Cell(rowNumber, 8).Value =
                    item.TotalMaterials;

                worksheet.Cell(rowNumber, 9).Value =
                    item.PendingMaterials;

                worksheet.Cell(rowNumber, 10).Value =
                    item.CompletedMaterials;

                worksheet.Cell(rowNumber, 11).Value =
                    item.NotInvoicedMaterials;

                worksheet.Cell(rowNumber, 12).Value =
                    item.PendingDispatchMaterials;

                worksheet.Cell(rowNumber, 13).Value =
                    item.OverdueMaterials;

                worksheet.Cell(rowNumber, 14).Value =
                    item.TotalPOQuantity;

                worksheet.Cell(rowNumber, 15).Value =
                    item.TotalInvoiceQuantity;

                worksheet.Cell(rowNumber, 16).Value =
                    item.TotalInvoiceBalanceQuantity;

                worksheet.Cell(rowNumber, 17).Value =
                    item.TotalDispatchQuantity;

                worksheet.Cell(rowNumber, 18).Value =
                    item.TotalPendingDispatchQuantity;

                if (item.EarliestDeliveryDate.HasValue)
                {
                    worksheet.Cell(
                        rowNumber,
                        19).Value =
                        item.EarliestDeliveryDate.Value;
                }

                if (item.LatestDeliveryDate.HasValue)
                {
                    worksheet.Cell(
                        rowNumber,
                        20).Value =
                        item.LatestDeliveryDate.Value;
                }

                worksheet.Cell(rowNumber, 21).Value =
                    item.HasPdf ? "Yes" : "No";

                StyleExcelStatus(
                    worksheet,
                    rowNumber,
                    item);

                rowNumber++;
            }

            var title =
                worksheet.Range("A1:U1");

            title.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#4C2FD4");

            title.Style.Font.FontColor =
                XLColor.White;

            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 16;

            title.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Range("A2:U3")
                .Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            var header =
                worksheet.Range("A5:U5");

            header.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#EDE9FE");

            header.Style.Font.FontColor =
                XLColor.FromHtml("#2D1B69");

            header.Style.Font.Bold = true;

            if (rows.Count > 0)
            {
                worksheet.Range(
                        6,
                        4,
                        5 + rows.Count,
                        5)
                    .Style.DateFormat.Format =
                    "dd-mmm-yyyy";

                worksheet.Range(
                        6,
                        19,
                        5 + rows.Count,
                        20)
                    .Style.DateFormat.Format =
                    "dd-mmm-yyyy";

                worksheet.Range(
                        6,
                        14,
                        5 + rows.Count,
                        18)
                    .Style.NumberFormat.Format =
                    "0.00";
            }

            worksheet.SheetView
                .FreezeRows(5);

            worksheet.RangeUsed()?
                .SetAutoFilter();

            worksheet.Columns(1, 21)
                .AdjustToContents();

            worksheet.Column(3).Width = 22;
            worksheet.Column(6).Width = 16;
            worksheet.Column(7).Width = 20;
        }

        private sealed class PurchaseOrderAggregateRow
        {
            public int PurchaseOrderId { get; set; }

            public string PONumber { get; set; } =
                string.Empty;

            public string Handler { get; set; } =
                string.Empty;

            public DateTime? PODate { get; set; }

            public DateTime? POReceivedDate { get; set; }

            public string? PdfFilePath { get; set; }

            public DateTime CreatedDate { get; set; }

            public int TotalMaterials { get; set; }

            public int CompletedMaterials { get; set; }

            public int NotInvoicedMaterials { get; set; }

            public int PendingDispatchMaterials { get; set; }

            public int OverdueMaterials { get; set; }

            public decimal TotalPOQuantity { get; set; }

            public decimal TotalInvoiceQuantity { get; set; }

            public decimal TotalInvoiceBalanceQuantity { get; set; }

            public decimal TotalDispatchQuantity { get; set; }

            public decimal TotalPendingDispatchQuantity { get; set; }

            public DateTime? EarliestDeliveryDate { get; set; }

            public DateTime? LatestDeliveryDate { get; set; }
        }

        private static void StyleExcelStatus(
            IXLWorksheet worksheet,
            int rowNumber,
            PurchaseOrderIndexRowViewModel item)
        {
            var statusCell =
                worksheet.Cell(
                    rowNumber,
                    6);

            if (item.Status == "Completed")
            {
                statusCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#DCFCE7");

                statusCell.Style.Font.FontColor =
                    XLColor.FromHtml("#166534");
            }
            else if (
                item.Status == "In Progress")
            {
                statusCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#DBEAFE");

                statusCell.Style.Font.FontColor =
                    XLColor.FromHtml("#1D4ED8");
            }
            else
            {
                statusCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#FEF3C7");

                statusCell.Style.Font.FontColor =
                    XLColor.FromHtml("#92400E");
            }

            statusCell.Style.Font.Bold = true;

            if (item.OverdueMaterials > 0)
            {
                var overdueCell =
                    worksheet.Cell(
                        rowNumber,
                        13);

                overdueCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#FFE4E6");

                overdueCell.Style.Font.FontColor =
                    XLColor.FromHtml("#9F1239");

                overdueCell.Style.Font.Bold = true;
            }
        }
    }
}
