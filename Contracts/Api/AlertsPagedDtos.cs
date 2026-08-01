using System;
using System.Collections.Generic;

namespace POTracker.Contracts.Api
{
    public sealed class AlertsPagedResponseDto
    {
        public DateTime GeneratedAt { get; init; }

        public AlertsSummaryDto Summary { get; init; } =
            new();

        public string Category { get; init; } =
            string.Empty;

        public IReadOnlyList<AlertMaterialDto> Items
        { get; init; } =
            Array.Empty<AlertMaterialDto>();

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
