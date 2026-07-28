using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Services;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/alerts")]
    public sealed class AlertsApiController :
        ControllerBase
    {
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
        public async Task<
            ActionResult<AlertsResponseDto>>
            GetAlerts(
                CancellationToken
                    cancellationToken = default)
        {
            var today =
                DateTime.Today;

            var nextWeek =
                today.AddDays(7);

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

            var response =
                new AlertsResponseDto
                {
                    GeneratedAt =
                        DateTime.Now,

                    ProcessStartAlerts =
                        processStartAlerts,

                    OverdueMaterials =
                        overdueMaterials,

                    DeliveryWithinSevenDays =
                        deliveryWithinSevenDays,

                    PendingProcess =
                        pendingProcess,

                    DispatchPending =
                        dispatchPending,

                    DispatchDateAlerts =
                        dispatchDateAlerts,

                    LowStock =
                        lowStock
                };

            response.Summary =
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

            return Ok(response);
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
