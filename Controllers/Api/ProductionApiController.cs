using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/production")]
    public sealed class ProductionApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductionApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // PROCESS QUEUE
        // ============================================================

        [HttpGet("process-queue")]
        public async Task<ActionResult<ProcessQueueResponseDto>>
            ProcessQueue(
                string? search,
                int page = 1,
                int pageSize = 50)
        {
            page = Math.Max(1, page);

            var allowedPageSizes =
                new[] { 10, 25, 50, 100, 200 };

            if (!allowedPageSizes.Contains(pageSize))
            {
                pageSize = 50;
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var weekEndExclusive = today.AddDays(8);

            var baseQuery = _context.Materials
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchValue = search.Trim();

                baseQuery = baseQuery.Where(material =>
                    (material.MaterialNo != null &&
                     material.MaterialNo.Contains(searchValue)) ||
                    (material.ShortDescription != null &&
                     material.ShortDescription.Contains(searchValue)) ||
                    (material.AssignedDashboard != null &&
                     material.AssignedDashboard.Contains(searchValue)) ||
                    (material.PurchaseOrder != null &&
                     material.PurchaseOrder.PONumber.Contains(searchValue)) ||
                    (material.PurchaseOrder != null &&
                     material.PurchaseOrder.Purchaser != null &&
                     material.PurchaseOrder.Purchaser.PurchaserName
                         .Contains(searchValue)));
            }

            var summary = new ProcessQueueSummaryDto
            {
                TotalMaterials =
                    await baseQuery.CountAsync(),

                Overdue =
                    await baseQuery.CountAsync(material =>
                        material.DeliveryDate != null &&
                        material.DeliveryDate < today &&
                        material.ProcessStatus != "Completed" &&
                        material.DispatchStatus != "Dispatched"),

                StartToday =
                    await baseQuery.CountAsync(material =>
                        material.ProcessStartDate != null &&
                        material.ProcessStartDate >= today &&
                        material.ProcessStartDate < tomorrow &&
                        material.ProcessStatus == "Pending"),

                ThisWeek =
                    await baseQuery.CountAsync(material =>
                        material.ProcessStartDate != null &&
                        material.ProcessStartDate >= tomorrow &&
                        material.ProcessStartDate < weekEndExclusive &&
                        material.ProcessStatus != "Completed"),

                Future =
                    await baseQuery.CountAsync(material =>
                        material.ProcessStartDate != null &&
                        material.ProcessStartDate >= weekEndExclusive &&
                        material.ProcessStatus != "Completed")
            };

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    summary.TotalMaterials /
                    (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var overdue = await MapQueueItems(
                baseQuery.Where(material =>
                    material.DeliveryDate != null &&
                    material.DeliveryDate < today &&
                    material.ProcessStatus != "Completed" &&
                    material.DispatchStatus != "Dispatched")
                .OrderBy(material => material.DeliveryDate)
                .Take(6));

            var startToday = await MapQueueItems(
                baseQuery.Where(material =>
                    material.ProcessStartDate != null &&
                        material.ProcessStartDate >= today &&
                        material.ProcessStartDate < tomorrow &&
                        material.ProcessStatus == "Pending")
                .OrderBy(material => material.ProcessStartDate)
                .Take(6));

            var thisWeek = await MapQueueItems(
                baseQuery.Where(material =>
                    material.ProcessStartDate != null &&
                    material.ProcessStartDate >= tomorrow &&
                    material.ProcessStartDate < weekEndExclusive &&
                    material.ProcessStatus != "Completed")
                .OrderBy(material => material.ProcessStartDate)
                .Take(6));

            var future = await MapQueueItems(
                baseQuery.Where(material =>
                    material.ProcessStartDate != null &&
                    material.ProcessStartDate >= weekEndExclusive &&
                    material.ProcessStatus != "Completed")
                .OrderBy(material => material.ProcessStartDate)
                .Take(6));

            var items = await MapQueueItems(
                baseQuery
                    .OrderBy(material =>
                        material.ProcessStartDate ??
                        material.DeliveryDate)
                    .ThenByDescending(material =>
                        material.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize));

            return Ok(new ProcessQueueResponseDto
            {
                Summary = summary,
                Overdue = overdue,
                StartToday = startToday,
                ThisWeek = thisWeek,
                Future = future,
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = summary.TotalMaterials,
                TotalPages = totalPages
            });
        }

        // ============================================================
        // PRODUCTION CALENDAR
        // ============================================================

        [HttpGet("calendar-events")]
        public async Task<ActionResult<
            List<ProductionCalendarEventDto>>>
            CalendarEvents(
                DateTime? start,
                DateTime? end,
                CancellationToken cancellationToken)
        {
            var today = DateTime.Today;

            var rangeStart =
                (start ??
                 new DateTime(
                     today.Year,
                     today.Month,
                     1))
                .Date;

            var rangeEnd =
                (end ??
                 rangeStart.AddMonths(1))
                .Date;

            if (rangeEnd <= rangeStart)
            {
                rangeEnd =
                    rangeStart.AddMonths(1);
            }

            var includeOverdue =
                rangeStart <= today &&
                today < rangeEnd;

            var materials = await _context.Materials
                .AsNoTracking()
                .Where(material =>
                    (material.ProcessStartDate != null &&
                     material.ProcessStartDate >= rangeStart &&
                     material.ProcessStartDate < rangeEnd) ||

                    (material.DispatchDate != null &&
                     material.DispatchDate >= rangeStart &&
                     material.DispatchDate < rangeEnd) ||

                    (material.DeliveryDate != null &&
                     material.DeliveryDate >= rangeStart &&
                     material.DeliveryDate < rangeEnd) ||

                    (includeOverdue &&
                     material.DeliveryDate != null &&
                     material.DeliveryDate < today &&
                     material.ProcessStatus != "Completed" &&
                     material.DispatchStatus != "Dispatched"))
                .Select(material => new
                {
                    material.Id,
                    material.MaterialNo,
                    material.ProcessStartDate,
                    material.DispatchDate,
                    material.DeliveryDate,
                    material.ProcessStatus,
                    material.DispatchStatus
                })
                .ToListAsync(cancellationToken);

            var events =
                new List<ProductionCalendarEventDto>(
                    materials.Count * 2);

            foreach (var material in materials)
            {
                var materialNumber =
                    string.IsNullOrWhiteSpace(
                        material.MaterialNo)
                        ? $"Material #{material.Id}"
                        : material.MaterialNo;

                if (material.ProcessStartDate.HasValue &&
                    material.ProcessStartDate.Value >= rangeStart &&
                    material.ProcessStartDate.Value < rangeEnd)
                {
                    events.Add(new ProductionCalendarEventDto
                    {
                        Id = material.Id,
                        MaterialId = material.Id,
                        Title = "Start: " + materialNumber,
                        Start = material.ProcessStartDate.Value
                            .ToString("yyyy-MM-dd"),
                        Color = "#2563eb",
                        EventType = "Process Start"
                    });
                }

                if (material.DispatchDate.HasValue &&
                    material.DispatchDate.Value >= rangeStart &&
                    material.DispatchDate.Value < rangeEnd)
                {
                    events.Add(new ProductionCalendarEventDto
                    {
                        Id = material.Id,
                        MaterialId = material.Id,
                        Title = "Dispatch: " + materialNumber,
                        Start = material.DispatchDate.Value
                            .ToString("yyyy-MM-dd"),
                        Color = "#f59e0b",
                        EventType = "Dispatch"
                    });
                }

                if (material.DeliveryDate.HasValue &&
                    material.DeliveryDate.Value >= rangeStart &&
                    material.DeliveryDate.Value < rangeEnd)
                {
                    events.Add(new ProductionCalendarEventDto
                    {
                        Id = material.Id,
                        MaterialId = material.Id,
                        Title = "Delivery: " + materialNumber,
                        Start = material.DeliveryDate.Value
                            .ToString("yyyy-MM-dd"),
                        Color =
                            material.ProcessStatus == "Completed"
                                ? "#16a34a"
                                : "#22c55e",
                        EventType = "Delivery"
                    });
                }

                if (includeOverdue &&
                    material.DeliveryDate.HasValue &&
                    material.DeliveryDate.Value.Date < today &&
                    material.ProcessStatus != "Completed" &&
                    material.DispatchStatus != "Dispatched")
                {
                    events.Add(new ProductionCalendarEventDto
                    {
                        Id = material.Id,
                        MaterialId = material.Id,
                        Title = "Overdue: " + materialNumber,
                        Start = today.ToString("yyyy-MM-dd"),
                        Color = "#dc2626",
                        EventType = "Overdue"
                    });
                }
            }

            return Ok(events);
        }

        // ============================================================
        // MAPPING
        // ============================================================

        private static async Task<List<ProcessQueueItemDto>>
            MapQueueItems(
                IQueryable<POTracker.Models.Material> query)
        {
            return await query
                .Select(material =>
                    new ProcessQueueItemDto
                    {
                        MaterialId = material.Id,

                        PurchaseOrderId =
                            material.PurchaseOrderId,

                        PONumber =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder.PONumber
                                : string.Empty,

                        PurchaserName =
                            material.PurchaseOrder != null &&
                            material.PurchaseOrder.Purchaser != null
                                ? material.PurchaseOrder
                                    .Purchaser.PurchaserName
                                : string.Empty,

                        MaterialNo =
                            material.MaterialNo ??
                            string.Empty,

                        Description =
                            material.ShortDescription ??
                            material.BriefDescription ??
                            string.Empty,

                        ProcessStartDate =
                            material.ProcessStartDate,

                        DeliveryDate =
                            material.DeliveryDate,

                        DispatchDate =
                            material.DispatchDate,

                        AssignedDashboard =
                            material.AssignedDashboard ??
                            string.Empty,

                        ProcessStatus =
                            material.ProcessStatus,

                        DispatchStatus =
                            material.DispatchStatus
                    })
                .ToListAsync();
        }
    }
}

