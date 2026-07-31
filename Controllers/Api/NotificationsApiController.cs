using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/notifications")]
    public sealed class NotificationsApiController :
        ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationsApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<
            ActionResult<NotificationsResponseDto>>
            Index(
                CancellationToken cancellationToken)
        {
            var today =
                DateTime.Today;

            var items =
                await _context.Alerts
                    .AsNoTracking()
                    .Include(alert =>
                        alert.Material)
                    .OrderByDescending(alert =>
                        alert.AlertDate)
                    .Take(100)
                    .Select(alert =>
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
                                alert.IsSentToMobile,
                        })
                    .ToListAsync(
                        cancellationToken);

            var response =
                new NotificationsResponseDto
                {
                    Items =
                        items,

                    Summary =
                        new NotificationSummaryDto
                        {
                            TotalAlerts =
                                items.Count,

                            UnreadCount =
                                items.Count(item =>
                                    !item.IsRead),

                            MobileQueue =
                                items.Count(item =>
                                    !item.IsSentToMobile),

                            DueToday =
                                items.Count(item =>
                                    item.AlertDate.Date <=
                                    today),
                        },
                };

            return Ok(response);
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
                        alert.IsRead,
                });
        }
    }
}
