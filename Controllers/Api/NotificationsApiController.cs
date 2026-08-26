using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/notifications")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class NotificationsApiController :
        ControllerBase
    {
        private const int DefaultPageSize = 25;

        private readonly ApplicationDbContext _context;

        public NotificationsApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        private static readonly HashSet<string>
            NotificationExcelFilterColumns =
                new(
                    new[]
                    {
                        "type",
                        "material",
                        "message",
                        "alertdate",
                        "mobile",
                        "status"
                    },
                    StringComparer.OrdinalIgnoreCase);

        [HttpGet]
        [ProducesResponseType(
            typeof(NotificationsPagedResponseDto),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<NotificationsPagedResponseDto>>
            Index(
                string? search,
                string? status,
                string? alertType,
                string? material,
                string? message,
                DateTime? alertDateFrom,
                DateTime? alertDateTo,
                string? mobileStatus,
                string? columnStatus,
                string? sortColumn,
                string? sortDirection,
                string? excelFilters,
                int page = 1,
                int pageSize = DefaultPageSize,
                CancellationToken cancellationToken = default)
        {
            var today =
                DateTime.Today;

            page =
                Math.Max(
                    page,
                    1);

            pageSize =
                NormalizePageSize(
                    pageSize);

            var query =
                ApplyNotificationFilters(
                    _context.Alerts
                        .AsNoTracking(),
                    search,
                    status,
                    alertType,
                    material,
                    message,
                    alertDateFrom,
                    alertDateTo,
                    mobileStatus,
                    columnStatus);

            var rows =
                await ProjectNotificationRows(
                        query)
                    .ToListAsync(
                        cancellationToken);

            var selections =
                ParseNotificationExcelFilters(
                    excelFilters);

            var filteredRows =
                rows
                    .Where(item =>
                        MatchesNotificationExcelFilters(
                            item,
                            selections))
                    .ToList();

            var summary =
                new NotificationSummaryDto
                {
                    TotalAlerts =
                        filteredRows.Count,

                    UnreadCount =
                        filteredRows.Count(item =>
                            !item.IsRead),

                    MobileQueue =
                        filteredRows.Count(item =>
                            !item.IsSentToMobile),

                    DueToday =
                        filteredRows.Count(item =>
                            item.AlertDate.Date <=
                            today)
                };

            var totalCount =
                summary.TotalAlerts;

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
                ApplyNotificationSort(
                        filteredRows,
                        sortColumn,
                        sortDirection)
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(
                        pageSize)
                    .ToList();

            return Ok(
                new NotificationsPagedResponseDto
                {
                    GeneratedAt =
                        DateTime.Now,

                    Summary =
                        summary,

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

        [HttpGet("excel-filter-values")]
        [ProducesResponseType(
            typeof(
                IReadOnlyList<
                    ExcelFilterValueDto>),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<
                IReadOnlyList<
                    ExcelFilterValueDto>>>
            GetExcelFilterValues(
                string column,
                string? search,
                string? status,
                string? alertType,
                string? material,
                string? message,
                DateTime? alertDateFrom,
                DateTime? alertDateTo,
                string? mobileStatus,
                string? columnStatus,
                string? excelFilters,
                CancellationToken cancellationToken = default)
        {
            var normalizedColumn =
                NormalizeNotificationColumn(
                    column);

            if (
                !NotificationExcelFilterColumns
                    .Contains(
                        normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Unsupported Notification Excel filter column."
                    });
            }

            var query =
                ApplyNotificationFilters(
                    _context.Alerts
                        .AsNoTracking(),
                    search,
                    status,
                    alertType,
                    material,
                    message,
                    alertDateFrom,
                    alertDateTo,
                    mobileStatus,
                    columnStatus);

            var rows =
                await ProjectNotificationRows(
                        query)
                    .ToListAsync(
                        cancellationToken);

            var selections =
                ParseNotificationExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var values =
                rows
                    .Where(item =>
                        MatchesNotificationExcelFilters(
                            item,
                            selections))
                    .Select(item =>
                        GetNotificationExcelCellValue(
                            item,
                            normalizedColumn))
                    .GroupBy(
                        value =>
                            value,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                        new ExcelFilterValueDto
                        {
                            Value =
                                group.Key,

                            Label =
                                string.IsNullOrEmpty(
                                    group.Key)
                                    ? "(Blank)"
                                    : group.Key,

                            Count =
                                group.Count()
                        })
                    .OrderBy(
                        item =>
                            item.Label,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            return Ok(values);
        }

        private static IQueryable<POTracker.Models.Alert>
            ApplyNotificationFilters(
                IQueryable<POTracker.Models.Alert> query,
                string? search,
                string? status,
                string? alertType,
                string? material,
                string? message,
                DateTime? alertDateFrom,
                DateTime? alertDateTo,
                string? mobileStatus,
                string? columnStatus)
        {
            var normalizedStatus =
                Clean(status);

            if (normalizedStatus != null)
            {
                normalizedStatus =
                    normalizedStatus.ToLowerInvariant();

                query =
                    normalizedStatus switch
                    {
                        "unread" =>
                            query.Where(
                                alert =>
                                    !alert.IsRead),

                        "read" =>
                            query.Where(
                                alert =>
                                    alert.IsRead),

                        _ =>
                            query
                    };
            }

            var normalizedSearch =
                Clean(search);

            if (normalizedSearch != null)
            {
                query =
                    query.Where(
                        alert =>
                            (
                                alert.Material != null &&
                                (
                                    alert.Material.MaterialNo ??
                                    string.Empty
                                ).Contains(
                                    normalizedSearch)
                            ) ||
                            alert.AlertType.Contains(
                                normalizedSearch) ||
                            alert.Message.Contains(
                                normalizedSearch));
            }

            var normalizedAlertType =
                Clean(alertType);

            if (normalizedAlertType != null)
            {
                query =
                    query.Where(alert =>
                        alert.AlertType.Contains(
                            normalizedAlertType));
            }

            var normalizedMaterial =
                Clean(material);

            if (normalizedMaterial != null)
            {
                query =
                    query.Where(alert =>
                        alert.Material != null &&
                        (
                            alert.Material.MaterialNo ??
                            string.Empty
                        ).Contains(
                            normalizedMaterial));
            }

            var normalizedMessage =
                Clean(message);

            if (normalizedMessage != null)
            {
                query =
                    query.Where(alert =>
                        alert.Message.Contains(
                            normalizedMessage));
            }

            if (alertDateFrom.HasValue)
            {
                var start =
                    alertDateFrom.Value.Date;

                query =
                    query.Where(alert =>
                        alert.AlertDate >= start);
            }

            if (alertDateTo.HasValue)
            {
                var endExclusive =
                    alertDateTo
                        .Value
                        .Date
                        .AddDays(1);

                query =
                    query.Where(alert =>
                        alert.AlertDate < endExclusive);
            }

            var normalizedMobileStatus =
                Clean(mobileStatus);

            if (normalizedMobileStatus != null)
            {
                normalizedMobileStatus =
                    normalizedMobileStatus
                        .ToLowerInvariant();

                if (
                    normalizedMobileStatus ==
                    "sent"
                )
                {
                    query =
                        query.Where(alert =>
                            alert.IsSentToMobile);
                }
                else if (
                    normalizedMobileStatus ==
                    "queued"
                )
                {
                    query =
                        query.Where(alert =>
                            !alert.IsSentToMobile);
                }
            }

            var normalizedColumnStatus =
                Clean(columnStatus);

            if (normalizedColumnStatus != null)
            {
                normalizedColumnStatus =
                    normalizedColumnStatus
                        .ToLowerInvariant();

                if (
                    normalizedColumnStatus ==
                    "unread"
                )
                {
                    query =
                        query.Where(alert =>
                            !alert.IsRead);
                }
                else if (
                    normalizedColumnStatus ==
                    "read"
                )
                {
                    query =
                        query.Where(alert =>
                            alert.IsRead);
                }
            }

            return query;
        }

        private static IQueryable<NotificationItemDto>
            ProjectNotificationRows(
                IQueryable<POTracker.Models.Alert> query)
        {
            return query.Select(
                alert =>
                    new NotificationItemDto
                    {
                        Id =
                            alert.Id,

                        MaterialId =
                            alert.MaterialId,

                        MaterialNo =
                            alert.Material != null
                                ? alert.Material.MaterialNo ??
                                  string.Empty
                                : string.Empty,

                        AlertType =
                            alert.AlertType,

                        Message =
                            alert.Message,

                        AlertDate =
                            alert.AlertDate,

                        IsRead =
                            alert.IsRead,

                        IsSentToMobile =
                            alert.IsSentToMobile
                    });
        }

        private static Dictionary<
            string,
            string[]>
            ParseNotificationExcelFilters(
                string? excelFilters)
        {
            if (
                string.IsNullOrWhiteSpace(
                    excelFilters)
            )
            {
                return new Dictionary<
                    string,
                    string[]>(
                    StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var parsed =
                    System.Text.Json.JsonSerializer
                        .Deserialize<
                            Dictionary<
                                string,
                                string[]>>(
                            excelFilters)
                    ?? new Dictionary<
                        string,
                        string[]>();

                return parsed
                    .Where(pair =>
                        NotificationExcelFilterColumns
                            .Contains(
                                NormalizeNotificationColumn(
                                    pair.Key)))
                    .ToDictionary(
                        pair =>
                            NormalizeNotificationColumn(
                                pair.Key),
                        pair =>
                            pair.Value ??
                            Array.Empty<string>(),
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (
                System.Text.Json.JsonException)
            {
                return new Dictionary<
                    string,
                    string[]>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool
            MatchesNotificationExcelFilters(
                NotificationItemDto item,
                IReadOnlyDictionary<
                    string,
                    string[]> selections)
        {
            foreach (
                var pair in selections)
            {
                var selectedValues =
                    pair.Value ??
                    Array.Empty<string>();

                if (
                    selectedValues.Length ==
                    0
                )
                {
                    return false;
                }

                var cellValue =
                    GetNotificationExcelCellValue(
                        item,
                        pair.Key);

                if (
                    !selectedValues.Any(
                        selected =>
                            string.Equals(
                                selected,
                                cellValue,
                                StringComparison.OrdinalIgnoreCase))
                )
                {
                    return false;
                }
            }

            return true;
        }

        private static string
            GetNotificationExcelCellValue(
                NotificationItemDto item,
                string column)
        {
            return NormalizeNotificationColumn(
                    column)
                switch
                {
                    "type" =>
                        item.AlertType,

                    "material" =>
                        item.MaterialNo,

                    "message" =>
                        item.Message,

                    "alertdate" =>
                        item.AlertDate.ToString(
                            "yyyy-MM-dd HH:mm"),

                    "mobile" =>
                        item.IsSentToMobile
                            ? "Sent"
                            : "Queued",

                    "status" =>
                        item.IsRead
                            ? "Read"
                            : "Unread",

                    _ =>
                        string.Empty
                };
        }

        private static IEnumerable<
            NotificationItemDto>
            ApplyNotificationSort(
                IEnumerable<
                    NotificationItemDto> items,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                NormalizeNotificationColumn(
                    sortColumn);

            var descending =
                string.Equals(
                    sortDirection?.Trim(),
                    "desc",
                    StringComparison.OrdinalIgnoreCase);

            if (
                string.IsNullOrEmpty(
                    column)
            )
            {
                return items
                    .OrderByDescending(item =>
                        item.AlertDate)
                    .ThenByDescending(item =>
                        item.Id);
            }

            return (
                column,
                descending
            ) switch
            {
                ("type", true) =>
                    items
                        .OrderByDescending(item =>
                            item.AlertType)
                        .ThenByDescending(item =>
                            item.Id),

                ("type", false) =>
                    items
                        .OrderBy(item =>
                            item.AlertType)
                        .ThenBy(item =>
                            item.Id),

                ("material", true) =>
                    items
                        .OrderByDescending(item =>
                            item.MaterialNo)
                        .ThenByDescending(item =>
                            item.Id),

                ("material", false) =>
                    items
                        .OrderBy(item =>
                            item.MaterialNo)
                        .ThenBy(item =>
                            item.Id),

                ("message", true) =>
                    items
                        .OrderByDescending(item =>
                            item.Message)
                        .ThenByDescending(item =>
                            item.Id),

                ("message", false) =>
                    items
                        .OrderBy(item =>
                            item.Message)
                        .ThenBy(item =>
                            item.Id),

                ("alertdate", true) =>
                    items
                        .OrderByDescending(item =>
                            item.AlertDate)
                        .ThenByDescending(item =>
                            item.Id),

                ("alertdate", false) =>
                    items
                        .OrderBy(item =>
                            item.AlertDate)
                        .ThenBy(item =>
                            item.Id),

                ("mobile", true) =>
                    items
                        .OrderByDescending(item =>
                            item.IsSentToMobile)
                        .ThenByDescending(item =>
                            item.Id),

                ("mobile", false) =>
                    items
                        .OrderBy(item =>
                            item.IsSentToMobile)
                        .ThenBy(item =>
                            item.Id),

                ("status", true) =>
                    items
                        .OrderByDescending(item =>
                            item.IsRead)
                        .ThenByDescending(item =>
                            item.Id),

                ("status", false) =>
                    items
                        .OrderBy(item =>
                            item.IsRead)
                        .ThenBy(item =>
                            item.Id),

                _ =>
                    items
                        .OrderByDescending(item =>
                            item.AlertDate)
                        .ThenByDescending(item =>
                            item.Id)
            };
        }

        private static string
            NormalizeNotificationColumn(
                string? column)
        {
            return column?
                .Trim()
                .ToLowerInvariant() ??
                string.Empty;
        }

        [HttpPost("{id:int}/read")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<
            ActionResult<
                MarkNotificationReadResponseDto>>
            MarkRead(
                int id,
                CancellationToken cancellationToken)
        {
            var alert =
                await _context.Alerts
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id == id,
                        cancellationToken);

            if (alert == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            "Notification was not found."
                    });
            }

            if (!alert.IsRead)
            {
                alert.IsRead = true;

                await _context.SaveChangesAsync(
                    cancellationToken);
            }

            return Ok(
                new MarkNotificationReadResponseDto
                {
                    Success =
                        true,

                    Message =
                        "Notification marked as read.",

                    NotificationId =
                        alert.Id,

                    IsRead =
                        alert.IsRead
                });
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
    }
}
