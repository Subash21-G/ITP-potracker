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

            var allAlerts =
                _context.Alerts
                    .AsNoTracking();

            var summary =
                new NotificationSummaryDto
                {
                    TotalAlerts =
                        await allAlerts.CountAsync(
                            cancellationToken),

                    UnreadCount =
                        await allAlerts.CountAsync(
                            alert =>
                                !alert.IsRead,
                            cancellationToken),

                    MobileQueue =
                        await allAlerts.CountAsync(
                            alert =>
                                !alert.IsSentToMobile,
                            cancellationToken),

                    DueToday =
                        await allAlerts.CountAsync(
                            alert =>
                                alert.AlertDate.Date <=
                                today,
                            cancellationToken)
                };

            var query =
                allAlerts;

            var normalizedStatus =
                Clean(status)?
                    .ToLowerInvariant();

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

            var totalCount =
                await query.CountAsync(
                    cancellationToken);

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
