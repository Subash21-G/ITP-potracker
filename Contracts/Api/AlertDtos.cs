namespace POTracker.Contracts.Api
{
    public sealed class AlertMaterialDto
    {
        public int MaterialId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string LineItem { get; set; } =
            string.Empty;

        public string MaterialNo { get; set; } =
            string.Empty;

        public string Description { get; set; } =
            string.Empty;

        public DateTime? DeliveryDate { get; set; }

        public DateTime? AlertDate { get; set; }

        public DateTime? DispatchDate { get; set; }

        public string ProcessStatus { get; set; } =
            string.Empty;

        public string DispatchStatus { get; set; } =
            string.Empty;

        public decimal PendingQuantity { get; set; }

        public decimal LiveStockQuantity { get; set; }

        public decimal AllocatedStockQuantity { get; set; }

        public decimal StockMinimumQuantity { get; set; }

        public decimal StockShortageQuantity { get; set; }

        public string StockStatus { get; set; } =
            "Not Found";

        public int StockRecordCount { get; set; }

        public bool StockDataLive { get; set; }
    }

    public sealed class AlertsSummaryDto
    {
        public int ProcessStartAlerts { get; set; }

        public int OverdueMaterials { get; set; }

        public int DeliveryWithinSevenDays { get; set; }

        public int PendingProcess { get; set; }

        public int DispatchPending { get; set; }

        public int DispatchDateAlerts { get; set; }

        public int LowStock { get; set; }

        public int TotalAlerts { get; set; }
    }

    public sealed class AlertsResponseDto
    {
        public DateTime GeneratedAt { get; set; }

        public AlertsSummaryDto Summary { get; set; } =
            new();

        public List<AlertMaterialDto>
            ProcessStartAlerts
        { get; set; } = new();

        public List<AlertMaterialDto>
            OverdueMaterials
        { get; set; } = new();

        public List<AlertMaterialDto>
            DeliveryWithinSevenDays
        { get; set; } = new();

        public List<AlertMaterialDto>
            PendingProcess
        { get; set; } = new();

        public List<AlertMaterialDto>
            DispatchPending
        { get; set; } = new();

        public List<AlertMaterialDto>
            DispatchDateAlerts
        { get; set; } = new();

        public List<AlertMaterialDto>
            LowStock
        { get; set; } = new();
    }
}
