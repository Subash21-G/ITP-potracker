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
                _context.Alerts
                    .AsNoTracking();

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

            var summary =
                new NotificationSummaryDto
                {
                    TotalAlerts =
                        await query.CountAsync(
                            cancellationToken),

                    UnreadCount =
                        await query.CountAsync(
                            alert =>
                                !alert.IsRead,
                            cancellationToken),

                    MobileQueue =
                        await query.CountAsync(
                            alert =>
                                !alert.IsSentToMobile,
                            cancellationToken),

                    DueToday =
                        await query.CountAsync(
                            alert =>
                                alert.AlertDate.Date <=
                                today,
                            cancellationToken)
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
                await query
                    .OrderByDescending(
                        alert =>
                            alert.AlertDate)
                    .ThenByDescending(
                        alert =>
                            alert.Id)
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(
                        pageSize)
                    .Select(
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
                            })
                    .ToListAsync(
                        cancellationToken);

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
