namespace POTracker.Contracts.Api
{
    public sealed class StockListItemDto
    {
        public int Id { get; set; }

        public string MaterialCode { get; set; } =
            string.Empty;

        public string MaterialNo { get; set; } =
            string.Empty;

        public string Description { get; set; } =
            string.Empty;

        public string SupplierName { get; set; } =
            string.Empty;

        public string Location { get; set; } =
            string.Empty;

        public decimal CurrentStock { get; set; }

        public decimal MinimumStock { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalValue { get; set; }

        public bool IsLowStock { get; set; }

        public string Status { get; set; } =
            string.Empty;

        public DateTime? LastUpdated { get; set; }
    }

    public sealed class StockSummaryDto
    {
        public int TotalRecords { get; set; }

        public int UniqueMaterials { get; set; }

        public int LowStockRecords { get; set; }

        public int AvailableRecords { get; set; }

        public int UpdatedToday { get; set; }

        public decimal TotalCurrentStock { get; set; }

        public decimal TotalInventoryValue { get; set; }
    }

    public sealed class StockListResponseDto
    {
        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalRecords { get; set; }

        public int TotalPages { get; set; }

        public bool SourceOnline { get; set; }

        public string Message { get; set; } =
            string.Empty;

        public DateTime RefreshedAt { get; set; }

        public StockSummaryDto Summary { get; set; } =
            new();

        public List<StockListItemDto> Items { get; set; } =
            new();
    }
}
