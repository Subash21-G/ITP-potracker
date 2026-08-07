using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Services;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/alerts")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class AlertsApiController :
        ControllerBase
    {
        private const int DefaultPageSize = 25;

        private const string DefaultCategory =
            "process-start";

        private readonly ApplicationDbContext
            _context;

        private readonly ExternalStockService
            _externalStockService;

        public AlertsApiController(
            ApplicationDbContext context,
            ExternalStockService
                externalStockService)
        {
            _context = context;

            _externalStockService =
                externalStockService;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(AlertsPagedResponseDto),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<AlertsPagedResponseDto>>
            GetAlerts(
                string? category,
                string? search,
                string? material,
                string? description,
                DateTime? priorityDateFrom,
                DateTime? priorityDateTo,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                string? processStatus,
                string? dispatchStatus,
                decimal? minAllocatedStock,
                decimal? maxAllocatedStock,
                decimal? minLiveStock,
                decimal? maxLiveStock,
                string? stockStatus,

                string? sortColumn,
                string? sortDirection,

                int page = 1,
                int pageSize = DefaultPageSize,
                CancellationToken
                    cancellationToken = default)
        {
            var today =
                DateTime.Today;

            var nextWeek =
                today.AddDays(7);

            var normalizedCategory =
                NormalizeCategory(
                    category);

            page =
                Math.Max(
                    page,
                    1);

            pageSize =
                NormalizePageSize(
                    pageSize);

            var materials =
                await _context.Materials
                    .AsNoTracking()
                    .Select(material =>
                        new AlertMaterialDto
                        {
                            MaterialId =
                                material.Id,

                            PurchaseOrderId =
                                material
                                    .PurchaseOrderId,

                            LineItem =
                                material.LineItem ??
                                string.Empty,

                            MaterialNo =
                                material.MaterialNo ??
                                string.Empty,

                            Description =
                                material
                                    .ShortDescription ??
                                material
                                    .BriefDescription ??
                                string.Empty,

                            DeliveryDate =
                                material.DeliveryDate,

                            AlertDate =
                                material.AlertDate,

                            DispatchDate =
                                material.DispatchDate,

                            ProcessStatus =
                                material
                                    .ProcessStatus ??
                                string.Empty,

                            DispatchStatus =
                                material
                                    .DispatchStatus ??
                                string.Empty,

                            PendingQuantity =
                                material.PendingQty
                        })
                    .ToListAsync(
                        cancellationToken);

            var stockLoad =
                await _externalStockService
                    .GetAllAsync(
                        false,
                        cancellationToken);

            ApplyStockAllocation(
                materials,
                stockLoad);

            var processStartAlerts =
                materials
                    .Where(material =>
                        material.AlertDate != null &&
                        material.AlertDate <= today &&
                        !IsCompleted(
                            material.ProcessStatus))
                    .OrderBy(material =>
                        material.AlertDate)
                    .ThenBy(material =>
                        material.MaterialId)
                    .ToList();

            var overdueMaterials =
                materials
                    .Where(material =>
                        material.DeliveryDate != null &&
                        material.DeliveryDate < today &&
                        !IsCompleted(
                            material.ProcessStatus))
                    .OrderBy(material =>
                        material.DeliveryDate)
                    .ThenBy(material =>
                        material.MaterialId)
                    .ToList();

            var deliveryWithinSevenDays =
                materials
                    .Where(material =>
                        material.DeliveryDate != null &&
                        material.DeliveryDate >= today &&
                        material.DeliveryDate <=
                            nextWeek &&
                        !IsCompleted(
                            material.ProcessStatus))
                    .OrderBy(material =>
                        material.DeliveryDate)
                    .ThenBy(material =>
                        material.MaterialId)
                    .ToList();

            var pendingProcess =
                materials
                    .Where(material =>
                        IsStatus(
                            material.ProcessStatus,
                            "Pending") ||
                        IsStatus(
                            material.ProcessStatus,
                            "In Process") ||
                        IsStatus(
                            material.ProcessStatus,
                            "Hold"))
                    .OrderBy(material =>
                        material.DeliveryDate ??
                        DateTime.MaxValue)
                    .ThenBy(material =>
                        material.MaterialId)
                    .ToList();

            var dispatchPending =
                materials
                    .Where(material =>
                        IsStatus(
                            material.DispatchStatus,
                            "Pending") ||
                        IsStatus(
                            material.DispatchStatus,
                            "Partial"))
                    .OrderBy(material =>
                        material.DeliveryDate ??
                        DateTime.MaxValue)
                    .ThenBy(material =>
                        material.MaterialId)
                    .ToList();

            var dispatchDateAlerts =
                materials
                    .Where(material =>
                        material.DispatchDate != null &&
                        material.DispatchDate <=
                            nextWeek &&
                        !IsStatus(
                            material.DispatchStatus,
                            "Dispatched"))
                    .OrderBy(material =>
                        material.DispatchDate)
                    .ThenBy(material =>
                        material.MaterialId)
                    .ToList();

            var lowStock =
                materials
                    .Where(material =>
                        material.PendingQuantity > 0 &&
                        material
                            .StockShortageQuantity > 0 &&
                        !IsCompleted(
                            material.ProcessStatus))
                    .OrderByDescending(material =>
                        material
                            .StockShortageQuantity)
                    .ThenBy(material =>
                        material.DeliveryDate ??
                        DateTime.MaxValue)
                    .ThenBy(material =>
                        material.MaterialId)
                    .ToList();

            var summary =
                new AlertsSummaryDto
                {
                    ProcessStartAlerts =
                        processStartAlerts.Count,

                    OverdueMaterials =
                        overdueMaterials.Count,

                    DeliveryWithinSevenDays =
                        deliveryWithinSevenDays.Count,

                    PendingProcess =
                        pendingProcess.Count,

                    DispatchPending =
                        dispatchPending.Count,

                    DispatchDateAlerts =
                        dispatchDateAlerts.Count,

                    LowStock =
                        lowStock.Count,

                    TotalAlerts =
                        processStartAlerts.Count +
                        overdueMaterials.Count +
                        deliveryWithinSevenDays.Count +
                        pendingProcess.Count +
                        dispatchPending.Count +
                        dispatchDateAlerts.Count +
                        lowStock.Count
                };

            var categoryItems =
                SelectCategory(
                    normalizedCategory,
                    processStartAlerts,
                    overdueMaterials,
                    deliveryWithinSevenDays,
                    pendingProcess,
                    dispatchPending,
                    dispatchDateAlerts,
                    lowStock);

            var normalizedSearch =
                Clean(
                    search);

            if (normalizedSearch != null)
            {
                categoryItems =
                    categoryItems
                        .Where(material =>
                            Contains(
                                material.MaterialNo,
                                normalizedSearch) ||
                            Contains(
                                material.LineItem,
                                normalizedSearch) ||
                            Contains(
                                material.Description,
                                normalizedSearch) ||
                            Contains(
                                material.ProcessStatus,
                                normalizedSearch) ||
                            Contains(
                                material.DispatchStatus,
                                normalizedSearch) ||
                            Contains(
                                material.StockStatus,
                                normalizedSearch))
                        .ToList();
            }

            var normalizedMaterial = Clean(material);
            if (normalizedMaterial != null)
            {
                categoryItems = categoryItems
                    .Where(item => Contains(item.MaterialNo, normalizedMaterial) || Contains(item.LineItem, normalizedMaterial))
                    .ToList();
            }

            var normalizedDescription = Clean(description);
            if (normalizedDescription != null)
            {
                categoryItems = categoryItems
                    .Where(item => Contains(item.Description, normalizedDescription))
                    .ToList();
            }

            if (priorityDateFrom.HasValue)
            {
                var priorityStart = priorityDateFrom.Value.Date;
                categoryItems = categoryItems
                    .Where(item => GetPriorityDate(item, normalizedCategory) is DateTime priorityDate && priorityDate >= priorityStart)
                    .ToList();
            }

            if (priorityDateTo.HasValue)
            {
                var priorityEndExclusive = priorityDateTo.Value.Date.AddDays(1);
                categoryItems = categoryItems
                    .Where(item => GetPriorityDate(item, normalizedCategory) is DateTime priorityDate && priorityDate < priorityEndExclusive)
                    .ToList();
            }

            if (deliveryDateFrom.HasValue)
            {
                var deliveryStart = deliveryDateFrom.Value.Date;
                categoryItems = categoryItems
                    .Where(item => item.DeliveryDate.HasValue && item.DeliveryDate.Value >= deliveryStart)
                    .ToList();
            }

            if (deliveryDateTo.HasValue)
            {
                var deliveryEndExclusive = deliveryDateTo.Value.Date.AddDays(1);
                categoryItems = categoryItems
                    .Where(item => item.DeliveryDate.HasValue && item.DeliveryDate.Value < deliveryEndExclusive)
                    .ToList();
            }

            var normalizedProcessStatus = Clean(processStatus);
            if (normalizedProcessStatus != null)
            {
                categoryItems = categoryItems
                    .Where(item => IsStatus(item.ProcessStatus, normalizedProcessStatus))
                    .ToList();
            }

            var normalizedDispatchStatus = Clean(dispatchStatus);
            if (normalizedDispatchStatus != null)
            {
                categoryItems = categoryItems
                    .Where(item => IsStatus(item.DispatchStatus, normalizedDispatchStatus))
                    .ToList();
            }

            if (minAllocatedStock.HasValue)
            {
                categoryItems = categoryItems.Where(item => item.AllocatedStockQuantity >= minAllocatedStock.Value).ToList();
            }

            if (maxAllocatedStock.HasValue)
            {
                categoryItems = categoryItems.Where(item => item.AllocatedStockQuantity <= maxAllocatedStock.Value).ToList();
            }

            if (minLiveStock.HasValue)
            {
                categoryItems = categoryItems.Where(item => item.LiveStockQuantity >= minLiveStock.Value).ToList();
            }

            if (maxLiveStock.HasValue)
            {
                categoryItems = categoryItems.Where(item => item.LiveStockQuantity <= maxLiveStock.Value).ToList();
            }

            var normalizedStockStatus = Clean(stockStatus);
            if (normalizedStockStatus != null)
            {
                categoryItems = categoryItems.Where(item => Contains(item.StockStatus, normalizedStockStatus)).ToList();
            }
            categoryItems =
                ApplyAlertSort(
                    categoryItems,
                    normalizedCategory,
                    sortColumn,
                    sortDirection);

            var totalCount =
                categoryItems.Count;

            var totalPages =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        totalCount /
                        (double)pageSize));

            if (page > totalPages)
            {
                page =
                    totalPages;
            }

            var items =
                categoryItems
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(
                        pageSize)
                    .ToList();

            return Ok(
                new AlertsPagedResponseDto
                {
                    GeneratedAt =
                        DateTime.Now,

                    Summary =
                        summary,

                    Category =
                        normalizedCategory,

                    Items =
                        items,

                    Page =
                        page,

                    PageSize =
                        pageSize,

                    TotalCount =
                        totalCount,

                    TotalPages =
                        totalPages
                });
        }

        private static List<AlertMaterialDto>
            ApplyAlertSort(
                List<AlertMaterialDto> items,
                string category,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                Clean(
                    sortColumn)?
                    .ToLowerInvariant();

            var descending =
                string.Equals(
                    Clean(
                        sortDirection),
                    "desc",
                    StringComparison.OrdinalIgnoreCase);

            if (column == null)
            {
                return items;
            }

            return column switch
            {
                "material" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.MaterialNo,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.LineItem,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "material" =>
                    items
                        .OrderBy(
                            item =>
                                item.MaterialNo,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.LineItem,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "description" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.Description,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "description" =>
                    items
                        .OrderBy(
                            item =>
                                item.Description,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "prioritydate" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                GetPriorityDate(
                                    item,
                                    category)
                                    .HasValue)
                        .ThenByDescending(
                            item =>
                                GetPriorityDate(
                                    item,
                                    category))
                        .ThenByDescending(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "prioritydate" =>
                    items
                        .OrderByDescending(
                            item =>
                                GetPriorityDate(
                                    item,
                                    category)
                                    .HasValue)
                        .ThenBy(
                            item =>
                                GetPriorityDate(
                                    item,
                                    category))
                        .ThenBy(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "deliverydate" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.DeliveryDate
                                    .HasValue)
                        .ThenByDescending(
                            item =>
                                item.DeliveryDate)
                        .ThenByDescending(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "deliverydate" =>
                    items
                        .OrderByDescending(
                            item =>
                                item.DeliveryDate
                                    .HasValue)
                        .ThenBy(
                            item =>
                                item.DeliveryDate)
                        .ThenBy(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "process" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.ProcessStatus,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "process" =>
                    items
                        .OrderBy(
                            item =>
                                item.ProcessStatus,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "dispatch" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.DispatchStatus,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "dispatch" =>
                    items
                        .OrderBy(
                            item =>
                                item.DispatchStatus,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "stock" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.AllocatedStockQuantity)
                        .ThenByDescending(
                            item =>
                                item.LiveStockQuantity)
                        .ThenByDescending(
                            item =>
                                item.StockShortageQuantity)
                        .ThenByDescending(
                            item =>
                                item.MaterialId)
                        .ToList(),

                "stock" =>
                    items
                        .OrderBy(
                            item =>
                                item.AllocatedStockQuantity)
                        .ThenBy(
                            item =>
                                item.LiveStockQuantity)
                        .ThenBy(
                            item =>
                                item.StockShortageQuantity)
                        .ThenBy(
                            item =>
                                item.MaterialId)
                        .ToList(),

                _ =>
                    items
            };
        }


        private static List<AlertMaterialDto>
            SelectCategory(
                string category,
                List<AlertMaterialDto>
                    processStartAlerts,
                List<AlertMaterialDto>
                    overdueMaterials,
                List<AlertMaterialDto>
                    deliveryWithinSevenDays,
                List<AlertMaterialDto>
                    pendingProcess,
                List<AlertMaterialDto>
                    dispatchPending,
                List<AlertMaterialDto>
                    dispatchDateAlerts,
                List<AlertMaterialDto>
                    lowStock)
        {
            return category switch
            {
                "overdue" =>
                    overdueMaterials,

                "delivery-seven-days" =>
                    deliveryWithinSevenDays,

                "pending-process" =>
                    pendingProcess,

                "dispatch-pending" =>
                    dispatchPending,

                "dispatch-date" =>
                    dispatchDateAlerts,

                "low-stock" =>
                    lowStock,

                _ =>
                    processStartAlerts
            };
        }

        private static DateTime? GetPriorityDate(
            AlertMaterialDto material,
            string category)
        {
            return category switch
            {
                "process-start" => material.AlertDate,
                "dispatch-date" => material.DispatchDate,
                _ => material.DeliveryDate
            };
        }

        private static string NormalizeCategory(
            string? category)
        {
            var normalized =
                Clean(
                    category)?
                    .ToLowerInvariant();

            return normalized switch
            {
                "process-start" =>
                    "process-start",

                "overdue" =>
                    "overdue",

                "delivery-seven-days" =>
                    "delivery-seven-days",

                "pending-process" =>
                    "pending-process",

                "dispatch-pending" =>
                    "dispatch-pending",

                "dispatch-date" =>
                    "dispatch-date",

                "low-stock" =>
                    "low-stock",

                _ =>
                    DefaultCategory
            };
        }

        private static int NormalizePageSize(
            int pageSize)
        {
            return pageSize is
                25 or 50 or 100
                    ? pageSize
                    : DefaultPageSize;
        }

        private static string? Clean(
            string? value)
        {
            var cleaned =
                value?.Trim();

            return string.IsNullOrWhiteSpace(
                cleaned)
                    ? null
                    : cleaned;
        }

        private static bool Contains(
            string value,
            string search)
        {
            return value.Contains(
                search,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyStockAllocation(
            List<AlertMaterialDto> materials,
            ExternalStockLoadResult stockLoad)
        {
            foreach (
                var materialGroup in
                materials.GroupBy(
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

                var orderedMaterials =
                    materialGroup
                        .OrderBy(material =>
                            material.DeliveryDate ??
                            DateTime.MaxValue)
                        .ThenBy(material =>
                            material.MaterialId)
                        .ToList();

                foreach (
                    var material in
                    orderedMaterials)
                {
                    material.LiveStockQuantity =
                        stockSnapshot.LiveStock;

                    material.StockMinimumQuantity =
                        stockSnapshot.MinimumStock;

                    material.StockRecordCount =
                        stockSnapshot.RecordCount;

                    material.StockDataLive =
                        stockLoad.IsOnline;

                    var requiredQuantity =
                        Math.Max(
                            0,
                            material
                                .PendingQuantity);

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

                    material
                        .AllocatedStockQuantity =
                            allocatedQuantity;

                    material
                        .StockShortageQuantity =
                            shortageQuantity;

                    if (
                        !stockLoad.IsOnline &&
                        stockLoad.Items.Count == 0)
                    {
                        material.StockStatus =
                            "Stock API Offline";

                        material
                            .AllocatedStockQuantity =
                                0;

                        material
                            .StockShortageQuantity =
                                requiredQuantity;

                        continue;
                    }

                    if (
                        stockSnapshot.RecordCount ==
                        0)
                    {
                        material.StockStatus =
                            "Not Found";
                    }
                    else if (
                        requiredQuantity <= 0)
                    {
                        material.StockStatus =
                            "No Pending";
                    }
                    else if (
                        allocatedQuantity >=
                        requiredQuantity)
                    {
                        material.StockStatus =
                            "Available";
                    }
                    else if (
                        allocatedQuantity > 0)
                    {
                        material.StockStatus =
                            "Partial Stock";
                    }
                    else if (
                        stockSnapshot.LiveStock <= 0)
                    {
                        material.StockStatus =
                            "Out of Stock";
                    }
                    else
                    {
                        material.StockStatus =
                            "Shortage";
                    }
                }
            }
        }

        private static bool IsCompleted(
            string status)
        {
            return IsStatus(
                status,
                "Completed");
        }

        private static bool IsStatus(
            string status,
            string expected)
        {
            return string.Equals(
                status,
                expected,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
