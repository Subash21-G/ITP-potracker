namespace POTracker.Contracts.Api
{
    public sealed class DispatchListResponseDto
    {
        public List<DispatchMaterialListItemDto> Items { get; set; } = new();

        public DispatchSummaryDto Summary { get; set; } = new();
    }

    public sealed class DispatchSummaryDto
    {
        public int TotalPendingMaterials { get; set; }

        public int PartialMaterials { get; set; }

        public decimal TotalPOQuantity { get; set; }

        public decimal TotalDispatchedQuantity { get; set; }

        public decimal TotalPendingQuantity { get; set; }
    }

    public sealed class DispatchMaterialListItemDto
    {
        public int MaterialId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string PurchaserName { get; set; } = string.Empty;

        public string LineItem { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal POQuantity { get; set; }

        public decimal DispatchQuantity { get; set; }

        public decimal PendingQuantity { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public DateTime? LastDispatchDate { get; set; }

        public string DispatchStatus { get; set; } = string.Empty;
    }

    public sealed class DispatchDetailsResponseDto
    {
        public DispatchMaterialDetailsDto Material { get; set; } = new();

        public List<DispatchHistoryItemDto> Dispatches { get; set; } = new();
    }

    public sealed class DispatchMaterialDetailsDto
    {
        public int MaterialId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string PurchaserName { get; set; } = string.Empty;

        public string LineItem { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal POQuantity { get; set; }

        public decimal InvoiceQuantity { get; set; }

        public decimal DispatchQuantity { get; set; }

        public decimal PendingQuantity { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public DateTime? LastDispatchDate { get; set; }

        public string DispatchStatus { get; set; } = string.Empty;
    }

    public sealed class DispatchHistoryItemDto
    {
        public int Id { get; set; }

        public int MaterialId { get; set; }

        public string DispatchNo { get; set; } = string.Empty;

        public DateTime DispatchDate { get; set; }

        public decimal DispatchQuantity { get; set; }

        public string Remarks { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }
    }

    public sealed class DispatchMutationResponseDto
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public int MaterialId { get; set; }

        public int? DispatchId { get; set; }

        public decimal DispatchQuantity { get; set; }

        public decimal PendingQuantity { get; set; }

        public string DispatchStatus { get; set; } = string.Empty;
    }

    public sealed class DispatchBulkDeleteRequestDto
    {
        public int MaterialId { get; set; }

        public List<int> DispatchIds { get; set; } = new();
    }
}
