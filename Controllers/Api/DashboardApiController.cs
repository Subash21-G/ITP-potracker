using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Services;

namespace POTracker.Controllers.Api
{
    /// <summary>
    /// Read-only dashboard data used by the Angular application.
    /// This endpoint provides data for the Angular dashboard
    /// during the gradual migration.
    /// </summary>
    [ApiController]
    [Route("api/dashboard")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class DashboardApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly ExternalStockService
            _externalStockService;

        public DashboardApiController(
            ApplicationDbContext context,
            ExternalStockService
                externalStockService)
        {
            _context = context;

            _externalStockService =
                externalStockService;
        }

        /// <summary>
        /// Returns dashboard cards, quantity summaries,
        /// status summaries and the latest material rows.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(
            typeof(DashboardResponseDto),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<DashboardResponseDto>> GetDashboard(
            CancellationToken cancellationToken)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var nextWeek = today.AddDays(7);

            // =====================================================
            // BASIC COUNTS
            // Queries are intentionally awaited one by one because
            // EF Core does not allow parallel operations on the same
            // ApplicationDbContext instance.
            // =====================================================

            var totalPurchasers =
                await _context.Purchasers
                    .AsNoTracking()
                    .CountAsync(cancellationToken);

            var totalPurchaseOrders =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .CountAsync(cancellationToken);

            var stockCandidates =
                await _context.Materials
                    .AsNoTracking()
                    .Where(material =>
                        material.PendingQty > 0 &&
                        material.ProcessStatus !=
                            "Completed")
                    .Select(material =>
                        new
                        {
                            material.Id,

                            MaterialNo =
                                material.MaterialNo ??
                                string.Empty,

                            material.PendingQty,

                            material.DeliveryDate
                        })
                    .ToListAsync(
                        cancellationToken);

            var stockLoad =
                await _externalStockService
                    .GetAllAsync(
                        false,
                        cancellationToken);

            var lowStock =
                0;

            if (
                stockLoad.Items.Count > 0 ||
                stockLoad.IsOnline)
            {
                foreach (
                    var materialGroup in
                    stockCandidates.GroupBy(
                        material =>
                            ExternalStockService
                                .Normalize(
                                    material.MaterialNo),
                        StringComparer
                            .OrdinalIgnoreCase))
                {
                    var firstMaterial =
                        materialGroup.First();

                    var stockSnapshot =
                        ExternalStockService
                            .BuildSnapshot(
                                stockLoad.Items,
                                firstMaterial.MaterialNo,
                                0);

                    var remainingStock =
                        stockSnapshot.LiveStock;

                    foreach (
                        var material in
                        materialGroup
                            .OrderBy(item =>
                                item.DeliveryDate ??
                                DateTime.MaxValue)
                            .ThenBy(item =>
                                item.Id))
                    {
                        var requiredQuantity =
                            Math.Max(
                                0,
                                material.PendingQty);

                        var allocatedQuantity =
                            Math.Min(
                                remainingStock,
                                requiredQuantity);

                        var shortageQuantity =
                            Math.Max(
                                0,
                                requiredQuantity -
                                allocatedQuantity);

                        remainingStock =
                            Math.Max(
                                0,
                                remainingStock -
                                allocatedQuantity);

                        if (shortageQuantity > 0)
                        {
                            lowStock++;
                        }
                    }
                }
            }

            var todayAlerts =
                await _context.Alerts
                    .AsNoTracking()
                    .CountAsync(
                        alert =>
                            !alert.IsRead &&
                            alert.AlertDate < tomorrow,
                        cancellationToken);

            // =====================================================
            // MATERIAL SUMMARY
            // All material totals are calculated in one SQL query.
            // =====================================================

            var materialStats =
                await _context.Materials
                    .AsNoTracking()
                    .GroupBy(material => 1)
                    .Select(group => new
                    {
                        TotalMaterials =
                            group.Count(),

                        PendingProcess =
                            group.Count(material =>
                                material.ProcessStatus ==
                                "Pending"),

                        RunningProcess =
                            group.Count(material =>
                                material.ProcessStatus ==
                                    "In Process" ||
                                material.ProcessStatus ==
                                    "In Progress"),

                        CompletedProcess =
                            group.Count(material =>
                                material.ProcessStatus ==
                                "Completed"),

                        ProcessStartAlerts =
                            group.Count(material =>
                                material.AlertDate != null &&
                                material.AlertDate <= today &&
                                material.ProcessStatus !=
                                    "Completed"),

                        Overdue =
                            group.Count(material =>
                                material.DeliveryDate != null &&
                                material.DeliveryDate < today &&
                                material.ProcessStatus !=
                                    "Completed"),

                        DeliveryWithin7Days =
                            group.Count(material =>
                                material.DeliveryDate != null &&
                                material.DeliveryDate >= today &&
                                material.DeliveryDate <= nextWeek &&
                                material.ProcessStatus !=
                                    "Completed"),

                        DispatchCompleted =
                            group.Count(material =>
                                material.DispatchStatus ==
                                "Dispatched"),

                        DispatchPending =
                            group.Count(material =>
                                material.DispatchStatus ==
                                "Pending"),

                        DispatchPartial =
                            group.Count(material =>
                                material.DispatchStatus ==
                                "Partial"),

                        DispatchPendingOrPartial =
                            group.Count(material =>
                                material.DispatchStatus ==
                                    "Pending" ||
                                material.DispatchStatus ==
                                    "Partial"),

                        FullyInvoiced =
                            group.Count(material =>
                                material.InvoiceStatus ==
                                "Fully Invoiced"),

                        PartiallyInvoiced =
                            group.Count(material =>
                                material.InvoiceStatus ==
                                "Partially Invoiced"),

                        NotInvoiced =
                            group.Count(material =>
                                material.InvoiceQty <= 0 ||
                                material.InvoiceStatus == "" ||
                                material.InvoiceStatus ==
                                    "Not Invoiced"),

                        TotalPoQty =
                            group.Sum(material =>
                                material.Qty),

                        TotalInvoiceQty =
                            group.Sum(material =>
                                material.InvoiceQty),

                        TotalDispatchQty =
                            group.Sum(material =>
                                material.DispatchQty),

                        TotalPendingQty =
                            group.Sum(material =>
                                material.PendingQty > 0
                                    ? material.PendingQty
                                    : 0m)
                    })
                    .FirstOrDefaultAsync(cancellationToken);

            // =====================================================
            // RECENT MATERIALS
            // Projection loads only fields required by Angular.
            // =====================================================

            var recentMaterials =
                await _context.Materials
                    .AsNoTracking()
                    .OrderByDescending(material =>
                        material.Id)
                    .Take(8)
                    .Select(material =>
                        new DashboardRecentMaterialDto
                        {
                            Id = material.Id,

                            PurchaseOrderId =
                                material.PurchaseOrderId,

                            PoNumber =
                                material.PurchaseOrder != null
                                    ? material.PurchaseOrder.PONumber
                                    : string.Empty,

                            PurchaserName =
                                material.PurchaseOrder != null &&
                                material.PurchaseOrder.Purchaser != null
                                    ? material.PurchaseOrder
                                        .Purchaser.PurchaserName
                                    : string.Empty,

                            LineItem =
                                material.LineItem ??
                                string.Empty,

                            MaterialNo =
                                material.MaterialNo ??
                                string.Empty,

                            Description =
                                material.ShortDescription ??
                                material.BriefDescription ??
                                string.Empty,

                            PoQty = material.Qty,

                            InvoiceQty =
                                material.InvoiceQty,

                            DispatchQty =
                                material.DispatchQty,

                            PendingQty =
                                material.PendingQty > 0
                                    ? material.PendingQty
                                    : 0m,

                            DeliveryDate =
                                material.DeliveryDate,

                            InvoiceStatus =
                                material.InvoiceStatus,

                            DispatchStatus =
                                material.DispatchStatus,

                            ProcessStatus =
                                material.ProcessStatus
                        })
                    .ToListAsync(cancellationToken);

            var response =
                new DashboardResponseDto
                {
                    GeneratedAt = DateTime.Now,

                    Overview =
                        new DashboardOverviewDto
                        {
                            TotalPurchasers =
                                totalPurchasers,

                            TotalPurchaseOrders =
                                totalPurchaseOrders,

                            TotalMaterials =
                                materialStats?
                                    .TotalMaterials ?? 0,

                            PendingMaterials =
                                materialStats?
                                    .PendingProcess ?? 0,

                            LowStock =
                                lowStock,

                            StockDataLive =
                                stockLoad.IsOnline,

                            StockMessage =
                                stockLoad.ErrorMessage ??
                                (
                                    stockLoad.IsOnline
                                        ? "Stock Maintenance connected."
                                        : "Stock Maintenance unavailable."
                                ),

                            TodayAlerts =
                                todayAlerts,

                            OverdueMaterials =
                                materialStats?
                                    .Overdue ?? 0,

                            DeliveryWithin7Days =
                                materialStats?
                                    .DeliveryWithin7Days ?? 0,

                            ProcessStartAlerts =
                                materialStats?
                                    .ProcessStartAlerts ?? 0,

                            DispatchPending =
                                materialStats?
                                    .DispatchPendingOrPartial ?? 0
                        },

                    Quantities =
                        new DashboardQuantitySummaryDto
                        {
                            TotalPoQty =
                                materialStats?
                                    .TotalPoQty ?? 0m,

                            TotalInvoiceQty =
                                materialStats?
                                    .TotalInvoiceQty ?? 0m,

                            TotalDispatchQty =
                                materialStats?
                                    .TotalDispatchQty ?? 0m,

                            TotalPendingQty =
                                materialStats?
                                    .TotalPendingQty ?? 0m
                        },

                    Process =
                        new DashboardProcessSummaryDto
                        {
                            Pending =
                                materialStats?
                                    .PendingProcess ?? 0,

                            InProgress =
                                materialStats?
                                    .RunningProcess ?? 0,

                            Completed =
                                materialStats?
                                    .CompletedProcess ?? 0
                        },

                    Dispatch =
                        new DashboardDispatchSummaryDto
                        {
                            Completed =
                                materialStats?
                                    .DispatchCompleted ?? 0,

                            Partial =
                                materialStats?
                                    .DispatchPartial ?? 0,

                            Pending =
                                materialStats?
                                    .DispatchPending ?? 0
                        },

                    Invoice =
                        new DashboardInvoiceSummaryDto
                        {
                            FullyInvoiced =
                                materialStats?
                                    .FullyInvoiced ?? 0,

                            PartiallyInvoiced =
                                materialStats?
                                    .PartiallyInvoiced ?? 0,

                            NotInvoiced =
                                materialStats?
                                    .NotInvoiced ?? 0
                        },

                    RecentMaterials =
                        recentMaterials
                };

            return Ok(response);
        }
    }
}


