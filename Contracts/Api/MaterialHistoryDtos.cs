using System.ComponentModel.DataAnnotations;

namespace POTracker.Contracts.Api
{
    public sealed class MaterialHistoryItemDto
    {
        public int Id { get; set; }

        public int MaterialId { get; set; }

        public string MaterialNo { get; set; } = string.Empty;

        public string? LineItem { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Outcome { get; set; } = string.Empty;

        public string? Remark { get; set; }

        public DateTime OccurredDate { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }
    }

    public sealed class MaterialHistorySummaryDto
    {
        public int TotalEntries { get; set; }

        public int PerfectDeliveries { get; set; }

        public int ReworkDeliveries { get; set; }

        public int Rejected { get; set; }
    }

    public sealed class MaterialHistoryListResponseDto
    {
        public IReadOnlyList<MaterialHistoryItemDto> Items { get; set; } =
            Array.Empty<MaterialHistoryItemDto>();

        public MaterialHistorySummaryDto Summary { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalRecords { get; set; }

        public int TotalPages { get; set; }
    }

    public sealed class MaterialHistoryMaterialOptionDto
    {
        public int Id { get; set; }

        public string MaterialNo { get; set; } = string.Empty;

        public string? LineItem { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string DisplayLabel { get; set; } = string.Empty;
    }

    public sealed class MaterialHistoryExcelFilterValueDto
    {
        public string Value { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    public sealed class MaterialHistoryUpsertRequestDto
    {
        [Range(1, int.MaxValue)]
        public int MaterialId { get; set; }

        [Required]
        [StringLength(40)]
        public string Outcome { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Remark { get; set; }

        public DateTime? OccurredDate { get; set; }
    }

    public sealed class MaterialHistorySaveResponseDto
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public MaterialHistoryItemDto History { get; set; } = new();
    }
}
