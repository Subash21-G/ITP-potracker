namespace POTracker.Contracts.Api
{
    public sealed class ReportPurchaserOptionDto
    {
        public int Id { get; set; }

        public string PurchaserName { get; set; } =
            string.Empty;
    }

    public sealed class ReportOptionsDto
    {
        public List<ReportPurchaserOptionDto> Purchasers { get; set; } =
            new();

        public List<string> ProcessStatuses { get; set; } =
            new();

        public List<string> DispatchStatuses { get; set; } =
            new();
    }

    public sealed class MaterialReportItemDto
    {
        public int MaterialId { get; set; }

        public int? PurchaseOrderId { get; set; }

        public string PONumber { get; set; } =
            string.Empty;

        public string PurchaserName { get; set; } =
            string.Empty;

        public string MaterialNo { get; set; } =
            string.Empty;

        public string Description { get; set; } =
            string.Empty;

        public decimal Qty { get; set; }

        public decimal DispatchQty { get; set; }

        public decimal PendingQty { get; set; }

        public decimal LiveStockQuantity { get; set; }

        public decimal AllocatedStockQuantity { get; set; }

        public decimal StockMinimumQuantity { get; set; }

        public decimal StockShortageQuantity { get; set; }

        public string StockStatus { get; set; } =
            "Not Found";

        public int StockRecordCount { get; set; }

        public bool StockDataLive { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public string ProcessStatus { get; set; } =
            string.Empty;

        public string DispatchStatus { get; set; } =
            string.Empty;

        public string RMGrade { get; set; } =
            string.Empty;

        public string RMSource { get; set; } =
            string.Empty;

        public string SurfaceFinish { get; set; } =
            string.Empty;

        public decimal Cost { get; set; }

        public string Remark { get; set; } =
            string.Empty;
    }

    public sealed class MaterialReportSummaryDto
    {
        public int TotalRecords { get; set; }

        public decimal TotalQty { get; set; }

        public decimal TotalDispatchQty { get; set; }

        public decimal TotalPendingQty { get; set; }

        public decimal TotalCost { get; set; }
    }

    public sealed class MaterialReportResponseDto
    {
        public MaterialReportSummaryDto Summary { get; set; } =
            new();

        public List<MaterialReportItemDto> Items { get; set; } =
            new();
    }
}

