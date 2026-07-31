using System;
using System.Collections.Generic;

namespace POTracker.Contracts.Api
{
    public sealed class AuditPagedResponseDto
    {
        public IReadOnlyList<AuditItemDto> Items { get; init; } =
            Array.Empty<AuditItemDto>();

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalCount { get; init; }

        public int TotalPages { get; init; }

        public bool HasPreviousPage =>
            Page > 1;

        public bool HasNextPage =>
            Page < TotalPages;

        public DateTime GeneratedAt { get; init; }
    }
}
