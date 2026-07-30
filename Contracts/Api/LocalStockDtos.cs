using System;
using System.Collections.Generic;

namespace POTracker.Contracts.Api
{
    public sealed class LocalStockListItemDto
    {
        public int Id { get; init; }

        public string MaterialNo { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string RMGrade { get; init; } = string.Empty;

        public string Location { get; init; } = string.Empty;

        public decimal AvailableQty { get; init; }

        public decimal MinimumQty { get; init; }

        public bool IsLowStock { get; init; }

        public string Status { get; init; } = string.Empty;

        public DateTime UpdatedDate { get; init; }
    }

    public sealed class LocalStockSummaryDto
    {
        public int TotalItems { get; init; }

        public int LowStockItems { get; init; }

        public int HealthyItems { get; init; }

        public int UpdatedToday { get; init; }
    }

    public sealed class LocalStockListResponseDto
    {
        public List<LocalStockListItemDto> Items { get; init; } = new();

        public LocalStockSummaryDto Summary { get; init; } = new();

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalRecords { get; init; }

        public int TotalPages { get; init; }
    }

    public sealed class LocalStockUpsertRequestDto
    {
        public string MaterialNo { get; init; } = string.Empty;

        public string? MaterialName { get; init; }

        public string? RMGrade { get; init; }

        public string? Location { get; init; }

        public decimal AvailableQty { get; init; }

        public decimal MinimumQty { get; init; }
    }

    public sealed class LocalStockSaveResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public LocalStockListItemDto? Stock { get; init; }
    }

    public sealed class LocalStockDeleteResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int StockId { get; init; }
    }
}