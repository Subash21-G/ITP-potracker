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
                string? material,
                string? description,
                string? poNumber,
                string? purchaser,
                DateTime? processStartFrom,
                DateTime? processStartTo,
                DateTime? deliveryFrom,
                DateTime? deliveryTo,
                string? owner,
                string? processStatus,
                string? dispatchStatus,
                int page = 1,
                int pageSize = 50)
        {
            page =
                Math.Max(
                    1,
                    page);

            var allowedPageSizes =
                new[]
                {
                    10,
                    25,
                    50,
                    100,
                    200
                };

            if (!allowedPageSizes.Contains(pageSize))
            {
                pageSize = 50;
            }

            var today =
                DateTime.Today;

            var tomorrow =
                today.AddDays(1);

            var weekEndExclusive =
                today.AddDays(8);

            var baseQuery =
                _context.Materials
                    .AsNoTracking()
                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchValue =
                    search.Trim();

                baseQuery =
                    baseQuery.Where(materialRow =>
                        (
                            materialRow.MaterialNo != null &&
                            materialRow.MaterialNo.Contains(
                                searchValue)
                        ) ||
                        (
                            materialRow.ShortDescription != null &&
                            materialRow.ShortDescription.Contains(
                                searchValue)
                        ) ||
                        (
                            materialRow.AssignedDashboard != null &&
                            materialRow.AssignedDashboard.Contains(
                                searchValue)
                        ) ||
                        (
                            materialRow.PurchaseOrder != null &&
                            materialRow.PurchaseOrder.PONumber.Contains(
                                searchValue)
                        ) ||
                        (
                            materialRow.PurchaseOrder != null &&
                            materialRow.PurchaseOrder.Purchaser != null &&
                            materialRow.PurchaseOrder.Purchaser
                                .PurchaserName
                                .Contains(
                                    searchValue)
                        ));
            }

            baseQuery =
                ApplyProcessQueueColumnFilters(
                    baseQuery,
                    material,
                    description,
                    poNumber,
                    purchaser,
                    processStartFrom,
                    processStartTo,
                    deliveryFrom,
                    deliveryTo,
                    owner,
                    processStatus,
                    dispatchStatus);

            var summary =
                new ProcessQueueSummaryDto
                {
                    TotalMaterials =
                        await baseQuery.CountAsync(),

                    Overdue =
                        await baseQuery.CountAsync(materialRow =>
                            materialRow.DeliveryDate != null &&
                            materialRow.DeliveryDate < today &&
                            materialRow.ProcessStatus != "Completed" &&
                            materialRow.DispatchStatus != "Dispatched"),

                    StartToday =
                        await baseQuery.CountAsync(materialRow =>
                            materialRow.ProcessStartDate != null &&
                            materialRow.ProcessStartDate < tomorrow &&
                            materialRow.ProcessStatus == "Pending"),

                    ThisWeek =
                        await baseQuery.CountAsync(materialRow =>
                            materialRow.ProcessStartDate != null &&
                            materialRow.ProcessStartDate >= tomorrow &&
                            materialRow.ProcessStartDate <
                                weekEndExclusive &&
                            materialRow.ProcessStatus != "Completed"),

                    Future =
                        await baseQuery.CountAsync(materialRow =>
                            materialRow.ProcessStartDate != null &&
                            materialRow.ProcessStartDate >=
                                weekEndExclusive &&
                            materialRow.ProcessStatus != "Completed")
                };

            var totalPages =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        summary.TotalMaterials /
                        (double)pageSize));

            if (page > totalPages)
            {
                page =
                    totalPages;
            }

            var overdue =
                await MapQueueItems(
                    baseQuery
                        .Where(materialRow =>
                            materialRow.DeliveryDate != null &&
                            materialRow.DeliveryDate < today &&
                            materialRow.ProcessStatus != "Completed" &&
                            materialRow.DispatchStatus != "Dispatched")
                        .OrderBy(materialRow =>
                            materialRow.DeliveryDate)
                        .Take(6));

            var startToday =
                await MapQueueItems(
                    baseQuery
                        .Where(materialRow =>
                            materialRow.ProcessStartDate != null &&
                            materialRow.ProcessStartDate < tomorrow &&
                            materialRow.ProcessStatus == "Pending")
                        .OrderBy(materialRow =>
                            materialRow.ProcessStartDate)
                        .Take(6));

            var thisWeek =
                await MapQueueItems(
                    baseQuery
                        .Where(materialRow =>
                            materialRow.ProcessStartDate != null &&
                            materialRow.ProcessStartDate >= tomorrow &&
                            materialRow.ProcessStartDate <
                                weekEndExclusive &&
                            materialRow.ProcessStatus != "Completed")
                        .OrderBy(materialRow =>
                            materialRow.ProcessStartDate)
                        .Take(6));

            var future =
                await MapQueueItems(
                    baseQuery
                        .Where(materialRow =>
                            materialRow.ProcessStartDate != null &&
                            materialRow.ProcessStartDate >=
                                weekEndExclusive &&
                            materialRow.ProcessStatus != "Completed")
                        .OrderBy(materialRow =>
                            materialRow.ProcessStartDate)
                        .Take(6));

            var items =
                await MapQueueItems(
                    baseQuery
                        .OrderBy(materialRow =>
                            materialRow.ProcessStartDate ??
                            materialRow.DeliveryDate)
                        .ThenByDescending(materialRow =>
                            materialRow.Id)
                        .Skip(
                            (page - 1) *
                            pageSize)
                        .Take(pageSize));

            return Ok(
                new ProcessQueueResponseDto
                {
                    Summary =
                        summary,

                    Overdue =
                        overdue,

                    StartToday =
                        startToday,

                    ThisWeek =
                        thisWeek,

                    Future =
                        future,

                    Items =
                        items,

                    Page =
                        page,

                    PageSize =
                        pageSize,

                    TotalCount =
                        summary.TotalMaterials,

                    TotalPages =
                        totalPages
                });
        }

        private static IQueryable<POTracker.Models.Material>
            ApplyProcessQueueColumnFilters(
                IQueryable<POTracker.Models.Material> query,
                string? material,
                string? description,
                string? poNumber,
                string? purchaser,
                DateTime? processStartFrom,
                DateTime? processStartTo,
                DateTime? deliveryFrom,
                DateTime? deliveryTo,
                string? owner,
                string? processStatus,
                string? dispatchStatus)
        {
            if (!string.IsNullOrWhiteSpace(material))
            {
                var value =
                    material.Trim();

                query =
                    query.Where(materialRow =>
                        materialRow.MaterialNo != null &&
                        materialRow.MaterialNo.Contains(
                            value));
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                var value =
                    description.Trim();

                query =
                    query.Where(materialRow =>
                        (
                            materialRow.ShortDescription != null &&
                            materialRow.ShortDescription.Contains(
                                value)
                        ) ||
                        (
                            materialRow.BriefDescription != null &&
                            materialRow.BriefDescription.Contains(
                                value)
                        ));
            }

            if (!string.IsNullOrWhiteSpace(poNumber))
            {
                var value =
                    poNumber.Trim();

                query =
                    query.Where(materialRow =>
                        materialRow.PurchaseOrder != null &&
                        materialRow.PurchaseOrder.PONumber.Contains(
                            value));
            }

            if (!string.IsNullOrWhiteSpace(purchaser))
            {
                var value =
                    purchaser.Trim();

                query =
                    query.Where(materialRow =>
                        materialRow.PurchaseOrder != null &&
                        materialRow.PurchaseOrder.Purchaser != null &&
                        materialRow.PurchaseOrder.Purchaser
                            .PurchaserName
                            .Contains(
                                value));
            }

            if (processStartFrom.HasValue)
            {
                var start =
                    processStartFrom.Value.Date;

                query =
                    query.Where(materialRow =>
                        materialRow.ProcessStartDate.HasValue &&
                        materialRow.ProcessStartDate.Value >=
                            start);
            }

            if (processStartTo.HasValue)
            {
                var endExclusive =
                    processStartTo
                        .Value
                        .Date
                        .AddDays(1);

                query =
                    query.Where(materialRow =>
                        materialRow.ProcessStartDate.HasValue &&
                        materialRow.ProcessStartDate.Value <
                            endExclusive);
            }

            if (deliveryFrom.HasValue)
            {
                var start =
                    deliveryFrom.Value.Date;

                query =
                    query.Where(materialRow =>
                        materialRow.DeliveryDate.HasValue &&
                        materialRow.DeliveryDate.Value >=
                            start);
            }

            if (deliveryTo.HasValue)
            {
                var endExclusive =
                    deliveryTo
                        .Value
                        .Date
                        .AddDays(1);

                query =
                    query.Where(materialRow =>
                        materialRow.DeliveryDate.HasValue &&
                        materialRow.DeliveryDate.Value <
                            endExclusive);
            }

            if (!string.IsNullOrWhiteSpace(owner))
            {
                var value =
                    owner.Trim();

                query =
                    query.Where(materialRow =>
                        materialRow.AssignedDashboard != null &&
                        materialRow.AssignedDashboard.Contains(
                            value));
            }

            if (!string.IsNullOrWhiteSpace(processStatus))
            {
                var value =
                    processStatus.Trim();

                query =
                    query.Where(materialRow =>
                        materialRow.ProcessStatus.Contains(
                            value));
            }

            if (!string.IsNullOrWhiteSpace(dispatchStatus))
            {
                var value =
                    dispatchStatus.Trim();

                query =
                    query.Where(materialRow =>
                        materialRow.DispatchStatus.Contains(
                            value));
            }

            return query;
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
                string? search,
                string? eventTypes,
                string? statuses,
                string? handler,
                string? department,
                string? poNumber,
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
                rangeEnd = rangeStart.AddMonths(1);
            }

            var includeOverdue =
                rangeStart <= today &&
                today < rangeEnd;

            var selectedStatuses =
                ParseCsv(statuses);

            var selectedEventTypes =
                ParseCsv(eventTypes);

            var query = _context.Materials
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
                     material.DispatchStatus != "Dispatched"));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                query = query.Where(material =>
                    (material.MaterialNo != null &&
                     material.MaterialNo.Contains(term)) ||
                    (material.ShortDescription != null &&
                     material.ShortDescription.Contains(term)) ||
                    (material.BriefDescription != null &&
                     material.BriefDescription.Contains(term)) ||
                    (material.AssignedDashboard != null &&
                     material.AssignedDashboard.Contains(term)) ||
                    (material.Dep != null &&
                     material.Dep.Contains(term)) ||
                    (material.PurchaseOrder != null &&
                     material.PurchaseOrder.PONumber.Contains(term)));
            }

            if (selectedStatuses.Length > 0)
            {
                query = query.Where(material =>
                    selectedStatuses.Contains(
                        material.ProcessStatus) ||
                    selectedStatuses.Contains(
                        material.DispatchStatus));
            }

            if (!string.IsNullOrWhiteSpace(handler))
            {
                var selectedHandler = handler.Trim();

                query = query.Where(material =>
                    material.AssignedDashboard != null &&
                    material.AssignedDashboard == selectedHandler);
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                var selectedDepartment = department.Trim();

                query = query.Where(material =>
                    material.Dep != null &&
                    material.Dep == selectedDepartment);
            }

            if (!string.IsNullOrWhiteSpace(poNumber))
            {
                var selectedPoNumber = poNumber.Trim();

                query = query.Where(material =>
                    material.PurchaseOrder != null &&
                    material.PurchaseOrder.PONumber.Contains(
                        selectedPoNumber));
            }

            var materials = await query
                .Select(material =>
                    new CalendarMaterialRow
                    {
                        MaterialId = material.Id,
                        PurchaseOrderId = material.PurchaseOrderId,
                        PONumber =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder.PONumber
                                : string.Empty,
                        MaterialNo = material.MaterialNo ?? string.Empty,
                        Description =
                            material.ShortDescription ??
                            material.BriefDescription ??
                            string.Empty,
                        Unit = material.Unit ?? string.Empty,
                        OrderedQty = material.Qty,
                        DispatchQty = material.DispatchQty,
                        PendingQty = material.PendingQty,
                        ProcessStartDate = material.ProcessStartDate,
                        DispatchDate = material.DispatchDate,
                        DeliveryDate = material.DeliveryDate,
                        ProcessStatus = material.ProcessStatus,
                        DispatchStatus = material.DispatchStatus,
                        Handler = material.AssignedDashboard ?? string.Empty,
                        Department = material.Dep ?? string.Empty,
                        Remarks = material.Remark ?? string.Empty
                    })
                .ToListAsync(cancellationToken);

            var events =
                new List<ProductionCalendarEventDto>(
                    materials.Count * 3);

            foreach (var material in materials)
            {
                if (material.ProcessStartDate.HasValue &&
                    material.ProcessStartDate.Value >= rangeStart &&
                    material.ProcessStartDate.Value < rangeEnd)
                {
                    events.Add(
                        CreateCalendarEvent(
                            material,
                            "Process Start",
                            material.ProcessStartDate.Value,
                            "#2563eb",
                            today));
                }

                if (material.DispatchDate.HasValue &&
                    material.DispatchDate.Value >= rangeStart &&
                    material.DispatchDate.Value < rangeEnd)
                {
                    events.Add(
                        CreateCalendarEvent(
                            material,
                            "Dispatch",
                            material.DispatchDate.Value,
                            "#f97316",
                            today));
                }

                if (material.DeliveryDate.HasValue &&
                    material.DeliveryDate.Value >= rangeStart &&
                    material.DeliveryDate.Value < rangeEnd)
                {
                    var completed = IsCompleted(material);

                    events.Add(
                        CreateCalendarEvent(
                            material,
                            completed
                                ? "Completed"
                                : "Delivery",
                            material.DeliveryDate.Value,
                            completed
                                ? "#64748b"
                                : "#16a34a",
                            today));
                }

                if (includeOverdue &&
                    material.DeliveryDate.HasValue &&
                    material.DeliveryDate.Value.Date < today &&
                    !IsCompleted(material))
                {
                    events.Add(
                        CreateCalendarEvent(
                            material,
                            "Overdue",
                            today,
                            "#dc2626",
                            today));
                }
            }

            if (selectedEventTypes.Length > 0)
            {
                events = events
                    .Where(calendarEvent =>
                        selectedEventTypes.Contains(
                            calendarEvent.EventType,
                            StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }

            return Ok(
                events
                    .OrderBy(calendarEvent =>
                        calendarEvent.Start)
                    .ThenBy(calendarEvent =>
                        calendarEvent.PONumber)
                    .ThenBy(calendarEvent =>
                        calendarEvent.MaterialNo)
                    .ToList());
        }

        [HttpGet("calendar-dashboard")]
        public async Task<ActionResult<
            ProductionCalendarDashboardDto>>
            CalendarDashboard(
                CancellationToken cancellationToken)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var nextSevenDays = today.AddDays(8);
            var monthStart =
                new DateTime(
                    today.Year,
                    today.Month,
                    1);
            var nextMonth = monthStart.AddMonths(1);

            var materials = await _context.Materials
                .AsNoTracking()
                .Select(material =>
                    new CalendarMaterialRow
                    {
                        MaterialId = material.Id,
                        PurchaseOrderId = material.PurchaseOrderId,
                        PONumber =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder.PONumber
                                : string.Empty,
                        MaterialNo = material.MaterialNo ?? string.Empty,
                        Description =
                            material.ShortDescription ??
                            material.BriefDescription ??
                            string.Empty,
                        Unit = material.Unit ?? string.Empty,
                        OrderedQty = material.Qty,
                        DispatchQty = material.DispatchQty,
                        PendingQty = material.PendingQty,
                        ProcessStartDate = material.ProcessStartDate,
                        DispatchDate = material.DispatchDate,
                        DeliveryDate = material.DeliveryDate,
                        ProcessStatus = material.ProcessStatus,
                        DispatchStatus = material.DispatchStatus,
                        Handler = material.AssignedDashboard ?? string.Empty,
                        Department = material.Dep ?? string.Empty,
                        Remarks = material.Remark ?? string.Empty
                    })
                .ToListAsync(cancellationToken);

            var activeMaterials = materials
                .Where(material => !IsCompleted(material))
                .ToList();

            var overdueItems = activeMaterials
                .Where(material =>
                    material.DeliveryDate.HasValue &&
                    material.DeliveryDate.Value.Date < today)
                .OrderBy(material =>
                    material.DeliveryDate)
                .ToList();

            var upcomingItems = activeMaterials
                .Where(material =>
                    material.DeliveryDate.HasValue &&
                    material.DeliveryDate.Value.Date >= today &&
                    material.DeliveryDate.Value.Date < nextSevenDays)
                .OrderBy(material =>
                    material.DeliveryDate)
                .ThenBy(material =>
                    material.PONumber)
                .Take(6)
                .Select(material =>
                    new ProductionCalendarUpcomingItemDto
                    {
                        MaterialId = material.MaterialId,
                        PurchaseOrderId = material.PurchaseOrderId,
                        PONumber = material.PONumber,
                        MaterialNo = material.MaterialNo,
                        EventType = "Delivery",
                        EventDate =
                            material.DeliveryDate!.Value.Date,
                        PendingQty =
                            GetPendingQuantity(material),
                        Unit = material.Unit
                    })
                .ToList();

            var handlerWorkload = activeMaterials
                .GroupBy(material =>
                    string.IsNullOrWhiteSpace(
                        material.Handler)
                        ? "Unassigned"
                        : material.Handler.Trim())
                .Select(group =>
                    new ProductionCalendarHandlerWorkloadDto
                    {
                        Handler = group.Key,
                        ActiveMaterials = group.Count(),
                        Level =
                            group.Count() >= 10
                                ? "High"
                                : group.Count() >= 5
                                    ? "Medium"
                                    : "Low"
                    })
                .OrderByDescending(item =>
                    item.ActiveMaterials)
                .ThenBy(item => item.Handler)
                .Take(6)
                .ToList();

            var completedThisMonth = materials.Count(material =>
                IsCompleted(material) &&
                material.DeliveryDate.HasValue &&
                material.DeliveryDate.Value >= monthStart &&
                material.DeliveryDate.Value < nextMonth);

            var dashboard =
                new ProductionCalendarDashboardDto
                {
                    Overdue = overdueItems.Count,

                    DueToday = activeMaterials.Count(material =>
                        material.DeliveryDate.HasValue &&
                        material.DeliveryDate.Value.Date == today),

                    NextSevenDays = activeMaterials.Count(material =>
                        material.DeliveryDate.HasValue &&
                        material.DeliveryDate.Value.Date >= tomorrow &&
                        material.DeliveryDate.Value.Date < nextSevenDays),

                    CompletedThisMonth = completedThisMonth,

                    TotalMaterials = materials.Count,

                    ProcessStartEvents = materials.Count(material =>
                        material.ProcessStartDate.HasValue &&
                        material.ProcessStartDate.Value >= monthStart &&
                        material.ProcessStartDate.Value < nextMonth),

                    DispatchEvents = materials.Count(material =>
                        material.DispatchDate.HasValue &&
                        material.DispatchDate.Value >= monthStart &&
                        material.DispatchDate.Value < nextMonth),

                    DeliveryEvents = materials.Count(material =>
                        !IsCompleted(material) &&
                        material.DeliveryDate.HasValue &&
                        material.DeliveryDate.Value >= monthStart &&
                        material.DeliveryDate.Value < nextMonth),

                    OverdueEvents = overdueItems.Count,
                    RevisedEvents = 0,
                    CompletedEvents = completedThisMonth,
                    Upcoming = upcomingItems,

                    OverdueItems = overdueItems
                        .Take(6)
                        .Select(material =>
                            new ProductionCalendarOverdueItemDto
                            {
                                MaterialId = material.MaterialId,
                                PurchaseOrderId =
                                    material.PurchaseOrderId,
                                PONumber = material.PONumber,
                                MaterialNo = material.MaterialNo,
                                DeliveryDate =
                                    material.DeliveryDate!.Value.Date,
                                DaysOverdue =
                                    (today -
                                     material.DeliveryDate.Value.Date)
                                    .Days
                            })
                        .ToList(),

                    HandlerWorkload = handlerWorkload
                };

            return Ok(dashboard);
        }

        private static ProductionCalendarEventDto
            CreateCalendarEvent(
                CalendarMaterialRow material,
                string eventType,
                DateTime eventDate,
                string color,
                DateTime today)
        {
            var poLabel =
                string.IsNullOrWhiteSpace(
                    material.PONumber)
                    ? $"Material {material.MaterialId}"
                    : $"PO {material.PONumber}";

            var status =
                IsCompleted(material)
                    ? "Completed"
                    : material.DispatchStatus.Equals(
                        "Partial",
                        StringComparison.OrdinalIgnoreCase)
                        ? "Partially Dispatched"
                        : material.ProcessStatus;

            var daysOverdue =
                material.DeliveryDate.HasValue &&
                material.DeliveryDate.Value.Date < today &&
                !IsCompleted(material)
                    ? (today -
                       material.DeliveryDate.Value.Date)
                      .Days
                    : 0;

            return new ProductionCalendarEventDto
            {
                Id =
                    $"material-{material.MaterialId}-" +
                    eventType
                        .ToLowerInvariant()
                        .Replace(" ", "-"),
                MaterialId = material.MaterialId,
                PurchaseOrderId = material.PurchaseOrderId,
                PONumber = material.PONumber,
                MaterialNo = material.MaterialNo,
                Description = material.Description,
                Unit = material.Unit,
                Title = $"{eventType}: {poLabel}",
                Start = eventDate.ToString("yyyy-MM-dd"),
                Color = color,
                EventType = eventType,
                Status = status,
                OrderedQty = material.OrderedQty,
                DispatchQty = material.DispatchQty,
                PendingQty = GetPendingQuantity(material),
                ProcessStartDate = material.ProcessStartDate,
                DispatchDate = material.DispatchDate,
                DeliveryDate = material.DeliveryDate,
                Handler = material.Handler,
                Department = material.Department,
                Remarks = material.Remarks,
                DaysOverdue = daysOverdue
            };
        }

        private static decimal GetPendingQuantity(
            CalendarMaterialRow material)
        {
            if (material.PendingQty > 0)
            {
                return material.PendingQty;
            }

            return Math.Max(
                0,
                material.OrderedQty -
                material.DispatchQty);
        }

        private static bool IsCompleted(
            CalendarMaterialRow material)
        {
            return
                material.ProcessStatus.Equals(
                    "Completed",
                    StringComparison.OrdinalIgnoreCase) ||
                material.DispatchStatus.Equals(
                    "Dispatched",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string[] ParseCsv(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value
                    .Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
        }

        private sealed class CalendarMaterialRow
        {
            public int MaterialId { get; init; }

            public int PurchaseOrderId { get; init; }

            public string PONumber { get; init; } = string.Empty;

            public string MaterialNo { get; init; } = string.Empty;

            public string Description { get; init; } = string.Empty;

            public string Unit { get; init; } = string.Empty;

            public decimal OrderedQty { get; init; }

            public decimal DispatchQty { get; init; }

            public decimal PendingQty { get; init; }

            public DateTime? ProcessStartDate { get; init; }

            public DateTime? DispatchDate { get; init; }

            public DateTime? DeliveryDate { get; init; }

            public string ProcessStatus { get; init; } = string.Empty;

            public string DispatchStatus { get; init; } = string.Empty;

            public string Handler { get; init; } = string.Empty;

            public string Department { get; init; } = string.Empty;

            public string Remarks { get; init; } = string.Empty;
        }        // ============================================================
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

