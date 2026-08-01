using System;
using System.Collections.Generic;

namespace POTracker.Contracts.Api
{
    public sealed class NotificationsPagedResponseDto
    {
        public DateTime GeneratedAt { get; init; }

        public NotificationSummaryDto Summary { get; init; } =
            new();

        public IReadOnlyList<NotificationItemDto> Items
        { get; init; } =
            Array.Empty<NotificationItemDto>();

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalCount { get; init; }

        public int TotalPages { get; init; }

        public bool HasPreviousPage =>
            Page > 1;

        public bool HasNextPage =>
            Page < TotalPages;
    }
}
