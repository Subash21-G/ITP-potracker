namespace POTracker.Contracts.Api
{
    public sealed class MaterialListItemDto
    {
        public int Id { get; init; }

        public int PurchaseOrderId { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public string Handler { get; init; } = string.Empty;

        public DateTime? PODate { get; init; }

        public DateTime? POReceivedDate { get; init; }

        public string LineItem { get; init; } = string.Empty;

        public string MaterialNo { get; init; } = string.Empty;

        public string? HsnSacCode { get; init; }

        public string Description { get; init; } = string.Empty;

        public decimal Quantity { get; init; }

        public string Unit { get; init; } = string.Empty;

        public decimal UnitPrice { get; init; }

        public string PriceUnit { get; init; } = string.Empty;

        public decimal InvoiceQuantity { get; init; }

        public decimal DispatchQuantity { get; init; }

        public decimal PendingQuantity { get; init; }

        public string InvoiceStatus { get; init; } = string.Empty;

        public string ProcessStatus { get; init; } = string.Empty;

        public string DispatchStatus { get; init; } = string.Empty;

        public DateTime? DeliveryDate { get; init; }

        public string DrawingNo { get; init; } = string.Empty;

        public string AssignedDashboard { get; init; } = string.Empty;
        public decimal LiveStockQuantity { get; set; }

        public decimal AllocatedStockQuantity { get; set; }

        public decimal StockMinimumQuantity { get; set; }

        public decimal StockShortageQuantity { get; set; }

        public string StockStatus { get; set; } =
            "Not Found";

        public int StockRecordCount { get; set; }

        public bool StockDataLive { get; set; }
    }
}





