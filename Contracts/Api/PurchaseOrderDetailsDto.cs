namespace POTracker.Contracts.Api
{
    public sealed class PurchaseOrderDetailsDto
    {
        public int PurchaseOrderId { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public DateTime? PODate { get; init; }

        public DateTime? POReceivedDate { get; init; }

        public string Handler { get; init; } = string.Empty;

        public string? BuyerEmail { get; init; }

        public string? VendorCode { get; init; }

        public string? VendorName { get; init; }

        public string? PaymentTerms { get; init; }

        public string? DeliveryCondition { get; init; }

        public string? Currency { get; init; }

        public decimal BasicValue { get; init; }

        public decimal TaxValue { get; init; }

        public decimal GrandTotal { get; init; }

        public string? ImportFormat { get; init; }

        public string? PdfFilePath { get; init; }

        public string Status { get; init; } = "Open";

        public DateTime CreatedDate { get; init; }

        public DateTime? UpdatedDate { get; init; }

        public PurchaseOrderDetailsSummaryDto Summary { get; init; }
            = new();

        public PagedResult<PurchaseOrderMaterialDto> Materials { get; init; }
            = new();
    }

    public sealed class PurchaseOrderDetailsSummaryDto
    {
        public int TotalMaterials { get; init; }

        public int CompletedMaterials { get; init; }

        public int PendingMaterials { get; init; }

        public int OverdueMaterials { get; init; }

        public decimal TotalPOQuantity { get; init; }

        public decimal TotalInvoiceBalance { get; init; }

        public decimal TotalDispatchBalance { get; init; }
    }

    public sealed class PurchaseOrderMaterialDto
    {
        public int Id { get; init; }

        public string LineItem { get; init; } = string.Empty;

        public string MaterialNo { get; init; } = string.Empty;

        public string? HsnSacCode { get; init; }

        public string Description { get; init; } = string.Empty;

        public string DrawingNo { get; init; } = string.Empty;

        public decimal Quantity { get; init; }

        public string Unit { get; init; } = string.Empty;

        public decimal UnitPrice { get; init; }

        public decimal InvoiceQuantity { get; init; }

        public decimal InvoiceBalanceQuantity { get; init; }

        public decimal DispatchQuantity { get; init; }

        public decimal DispatchBalanceQuantity { get; init; }

        public DateTime? DeliveryDate { get; init; }

        public string ProcessStatus { get; init; } = "Pending";

        public string InvoiceStatus { get; init; } = "Not Invoiced";

        public string DispatchStatus { get; init; } = "Pending";

        public bool IsOverdue { get; init; }
    }
}

