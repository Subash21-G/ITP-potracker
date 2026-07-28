namespace POTracker.Contracts.Api
{
    public sealed class NotificationItemDto
    {
        public int Id { get; set; }

        public int MaterialId { get; set; }

        public string MaterialNo { get; set; } =
            string.Empty;

        public string AlertType { get; set; } =
            string.Empty;

        public string Message { get; set; } =
            string.Empty;

        public DateTime AlertDate { get; set; }

        public bool IsRead { get; set; }

        public bool IsSentToMobile { get; set; }
    }

    public sealed class NotificationSummaryDto
    {
        public int TotalAlerts { get; set; }

        public int UnreadCount { get; set; }

        public int MobileQueue { get; set; }

        public int DueToday { get; set; }
    }

    public sealed class NotificationsResponseDto
    {
        public NotificationSummaryDto Summary { get; set; } =
            new();

        public List<NotificationItemDto> Items { get; set; } =
            new();
    }

    public sealed class MarkNotificationReadResponseDto
    {
        public bool Success { get; set; }

        public string Message { get; set; } =
            string.Empty;

        public int NotificationId { get; set; }

        public bool IsRead { get; set; }
    }
}
