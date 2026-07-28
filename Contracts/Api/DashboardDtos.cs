namespace POTracker.Contracts.Api
{
    /// <summary>
    /// Complete dashboard response returned to the Angular application.
    /// </summary>
    public sealed class DashboardResponseDto
    {
        public DateTime GeneratedAt { get; set; }

        public DashboardOverviewDto Overview { get; set; }
            = new();

        public DashboardQuantitySummaryDto Quantities { get; set; }
            = new();

        public DashboardProcessSummaryDto Process { get; set; }
            = new();

        public DashboardDispatchSummaryDto Dispatch { get; set; }
            = new();

        public DashboardInvoiceSummaryDto Invoice { get; set; }
            = new();

        public List<DashboardRecentMaterialDto> RecentMaterials
        { get; set; } = new();
    }

    /// <summary>
    /// Main dashboard cards.
    /// </summary>
    public sealed class DashboardOverviewDto
    {
        public int TotalPurchasers { get; set; }

        public int TotalPurchaseOrders { get; set; }

        public int TotalMaterials { get; set; }

        public int PendingMaterials { get; set; }

        public int LowStock { get; set; }

        public bool StockDataLive { get; set; }

        public string StockMessage { get; set; } =
            string.Empty;

        public int TodayAlerts { get; set; }

        public int OverdueMaterials { get; set; }

        public int DeliveryWithin7Days { get; set; }

        public int ProcessStartAlerts { get; set; }

        public int DispatchPending { get; set; }
    }

    /// <summary>
    /// PO, invoice and dispatch quantity totals.
    /// </summary>
    public sealed class DashboardQuantitySummaryDto
    {
        public decimal TotalPoQty { get; set; }

        public decimal TotalInvoiceQty { get; set; }

        public decimal TotalDispatchQty { get; set; }

        public decimal TotalPendingQty { get; set; }
    }

    /// <summary>
    /// Material process status totals.
    /// </summary>
    public sealed class DashboardProcessSummaryDto
    {
        public int Pending { get; set; }

        public int InProgress { get; set; }

        public int Completed { get; set; }
    }

    /// <summary>
    /// Material dispatch status totals.
    /// </summary>
    public sealed class DashboardDispatchSummaryDto
    {
        public int Completed { get; set; }

        public int Partial { get; set; }

        public int Pending { get; set; }
    }

    /// <summary>
    /// Material invoice status totals.
    /// </summary>
    public sealed class DashboardInvoiceSummaryDto
    {
        public int FullyInvoiced { get; set; }

        public int PartiallyInvoiced { get; set; }

        public int NotInvoiced { get; set; }
    }

    /// <summary>
    /// Latest material rows displayed in the Angular dashboard.
    /// </summary>
    public sealed class DashboardRecentMaterialDto
    {
        public int Id { get; set; }

        public int PurchaseOrderId { get; set; }

        public string PoNumber { get; set; } = string.Empty;

        public string PurchaserName { get; set; } = string.Empty;

        public string LineItem { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal PoQty { get; set; }

        public decimal InvoiceQty { get; set; }

        public decimal DispatchQty { get; set; }

        public decimal PendingQty { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public string InvoiceStatus { get; set; } = string.Empty;

        public string DispatchStatus { get; set; } = string.Empty;

        public string ProcessStatus { get; set; } = string.Empty;
    }
}

