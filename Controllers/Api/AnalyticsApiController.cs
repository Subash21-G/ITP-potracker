using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/analytics")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class AnalyticsApiController :
        ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AnalyticsApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(AnalyticsResponseDto),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<AnalyticsResponseDto>>
            Index(
                CancellationToken cancellationToken = default)
        {
            var rows =
                await _context.Purchasers
                    .AsNoTracking()
                    .OrderBy(purchaser =>
                        purchaser.PurchaserName)
                    .Select(purchaser =>
                        new PurchaserAnalyticsRowDto
                        {
                            PurchaserId =
                                purchaser.Id,

                            PurchaserName =
                                purchaser.PurchaserName,

                            PurchaseOrderCount =
                                purchaser.PurchaseOrders.Count(),

                            MaterialCount =
                                purchaser.PurchaseOrders
                                    .SelectMany(
                                        purchaseOrder =>
                                            purchaseOrder.Materials)
                                    .Count(),

                            PendingMaterials =
                                purchaser.PurchaseOrders
                                    .SelectMany(
                                        purchaseOrder =>
                                            purchaseOrder.Materials)
                                    .Count(material =>
                                        material.ProcessStatus !=
                                        "Completed"),

                            CompletedMaterials =
                                purchaser.PurchaseOrders
                                    .SelectMany(
                                        purchaseOrder =>
                                            purchaseOrder.Materials)
                                    .Count(material =>
                                        material.ProcessStatus ==
                                        "Completed")
                        })
                    .ToListAsync(
                        cancellationToken);

            var overdueCount =
                await _context.Materials
                    .AsNoTracking()
                    .CountAsync(
                        material =>
                            material.DeliveryDate.HasValue &&
                            material.DeliveryDate.Value <
                            DateTime.Today &&
                            material.ProcessStatus !=
                            "Completed",
                        cancellationToken);

            return Ok(
                new AnalyticsResponseDto
                {
                    Summary =
                        new AnalyticsSummaryDto
                        {
                            PurchaserCount =
                                rows.Count,

                            TotalMaterials =
                                rows.Sum(row =>
                                    row.MaterialCount),

                            OverdueMaterials =
                                overdueCount,

                            PurchaseOrderCount =
                                rows.Sum(row =>
                                    row.PurchaseOrderCount)
                        },

                    Purchasers =
                        rows,

                    GeneratedAt =
                        DateTime.Now
                });
        }
    }
}