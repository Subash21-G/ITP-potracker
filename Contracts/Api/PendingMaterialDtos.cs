namespace POTracker.Contracts.Api
{
    public sealed class PendingMaterialsResponseDto
    {
        public List<PendingMaterialItemDto> Items { get; set; }
            = new();

        public PendingMaterialSummaryDto Summary { get; set; }
            = new();

        public PendingMaterialFilterDataDto Filters { get; set; }
            = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalRecords { get; set; }

        public int TotalPages { get; set; }
    }

    public sealed class PendingMaterialItemDto
    {
        public int MaterialId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string PONumber { get; set; }
            = string.Empty;

        public string Handler { get; set; }
            = string.Empty;

        public DateTime? PODate { get; set; }

        public DateTime? POReceivedDate { get; set; }

        public string LineItem { get; set; }
            = string.Empty;

        public string MaterialNo { get; set; }
            = string.Empty;

        public string? HsnSacCode { get; set; }

        public string? ShortDescription { get; set; }

        public string? BriefDescription { get; set; }

        public string? Unit { get; set; }

        public string? Department { get; set; }

        public string? DrawingNo { get; set; }

        public string? DrawingVersion { get; set; }

        public decimal POQuantity { get; set; }

        public decimal InvoiceQuantity { get; set; }

        public decimal InvoiceBalanceQuantity { get; set; }

        public decimal DispatchQuantity { get; set; }

        public decimal PendingDispatchQuantity { get; set; }

        public decimal StockQuantity { get; set; }

        public decimal UnitCost { get; set; }

        public decimal TotalValue { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public DateTime? ProcessStartDate { get; set; }

        public DateTime? AlertDate { get; set; }

        public string ProcessStatus { get; set; }
            = string.Empty;

        public string InvoiceStatus { get; set; }
            = string.Empty;

        public string DispatchStatus { get; set; }
            = string.Empty;

        public string? AssignedDashboard { get; set; }

        public string? Remarks { get; set; }

        public string? PdfFilePath { get; set; }

        public string? DrawingFilePath { get; set; }

        public decimal CompletionPercentage { get; set; }

        public int OverdueDays { get; set; }

        public bool IsOverdue { get; set; }

        public bool IsDueToday { get; set; }

        public bool IsDueThisWeek { get; set; }

        public string DueStatus { get; set; }
            = string.Empty;
    }

    public sealed class PendingMaterialSummaryDto
    {
        public int TotalPendingMaterials { get; set; }

        public int OverdueMaterials { get; set; }

        public int DueTodayMaterials { get; set; }

        public int DueThisWeekMaterials { get; set; }

        public int WithoutDeliveryDate { get; set; }

        public decimal TotalPOQuantity { get; set; }

        public decimal TotalInvoiceQuantity { get; set; }

        public decimal TotalInvoiceBalanceQuantity { get; set; }

        public decimal TotalDispatchQuantity { get; set; }

        public decimal TotalPendingDispatchQuantity { get; set; }

        public decimal TotalPendingValue { get; set; }

        public List<PendingMaterialAssignmentDto>
            AssignmentSummary
        { get; set; } = new();
    }

    public sealed class PendingMaterialAssignmentDto
    {
        public string AssignedDashboard { get; set; }
            = string.Empty;

        public int MaterialCount { get; set; }

        public decimal PendingQuantity { get; set; }

        public int OverdueCount { get; set; }
    }

    public sealed class PendingMaterialFilterDataDto
    {
        public List<PendingMaterialFilterOptionDto>
            Handlers
        { get; set; } = new();

        public List<PendingMaterialFilterOptionDto>
            AssignedDashboards
        { get; set; } = new();

        public List<PendingMaterialFilterOptionDto>
            ProcessStatuses
        { get; set; } = new();

        public List<PendingMaterialFilterOptionDto>
            InvoiceStatuses
        { get; set; } = new();

        public List<PendingMaterialFilterOptionDto>
            DispatchStatuses
        { get; set; } = new();
    }

    public sealed class PendingMaterialFilterOptionDto
    {
        public string Value { get; set; }
            = string.Empty;

        public string Text { get; set; }
            = string.Empty;

        public int Count { get; set; }
    }
}
