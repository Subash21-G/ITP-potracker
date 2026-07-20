namespace POTracker.Contracts.Api
{
    public sealed class PurchaseOrderListResponseDto
    {
        public IReadOnlyList<PurchaseOrderListItemDto> Items { get; init; }
            = Array.Empty<PurchaseOrderListItemDto>();

        public PurchaseOrderListSummaryDto Summary { get; init; }
            = new();

        public IReadOnlyList<string> Handlers { get; init; }
            = Array.Empty<string>();

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalRecords { get; init; }

        public int TotalPages => Math.Max(
            1,
            (int)Math.Ceiling(
                TotalRecords / (double)PageSize));

        public bool HasPreviousPage => Page > 1;

        public bool HasNextPage => Page < TotalPages;
    }

    public sealed class PurchaseOrderListItemDto
    {
        public int PurchaseOrderId { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public string Handler { get; init; } = string.Empty;

        public DateTime? PODate { get; init; }

        public DateTime? POReceivedDate { get; init; }

        public string Status { get; init; } = "Open";

        public string MaterialStatus { get; init; } = "No Materials";

        public string? PdfFilePath { get; init; }

        public DateTime CreatedDate { get; init; }

        public int TotalMaterials { get; init; }

        public int PendingMaterials { get; init; }

        public int CompletedMaterials { get; init; }

        public int NotInvoicedMaterials { get; init; }

        public int PendingDispatchMaterials { get; init; }

        public int OverdueMaterials { get; init; }

        public decimal TotalPOQuantity { get; init; }

        public decimal TotalInvoiceQuantity { get; init; }

        public decimal TotalInvoiceBalanceQuantity { get; init; }

        public decimal TotalDispatchQuantity { get; init; }

        public decimal TotalPendingDispatchQuantity { get; init; }

        public DateTime? EarliestDeliveryDate { get; init; }

        public DateTime? LatestDeliveryDate { get; init; }

        public decimal CompletionPercentage { get; init; }
    }

    public sealed class PurchaseOrderListSummaryDto
    {
        public int TotalPurchaseOrders { get; init; }

        public int OpenPurchaseOrders { get; init; }

        public int CompletedPurchaseOrders { get; init; }

        public int PurchaseOrdersWithPendingMaterials { get; init; }

        public int PurchaseOrdersWithOverdueMaterials { get; init; }

        public int PurchaseOrdersWithoutHandler { get; init; }

        public decimal TotalPOQuantity { get; init; }

        public decimal TotalInvoiceBalanceQuantity { get; init; }

        public decimal TotalPendingDispatchQuantity { get; init; }
    }
}
