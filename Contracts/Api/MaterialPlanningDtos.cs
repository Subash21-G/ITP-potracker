namespace POTracker.Contracts.Api
{
    public sealed class MaterialPlanningSummaryDto
    {
        public int TotalMaterials { get; init; }

        public int Inhouse { get; init; }

        public int Outsourcing { get; init; }

        public int CostVariation { get; init; }

        public int Priority { get; init; }

        public int Urgent { get; init; }
    }

    public sealed class MaterialPlanningItemDto
    {
        public int Id { get; init; }

        public int PurchaseOrderId { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public string LineItem { get; init; } = string.Empty;

        public string MaterialNo { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public decimal Quantity { get; init; }

        public string Unit { get; init; } = string.Empty;

        public decimal Cost { get; init; }

        public string SourcingType { get; init; } = "Inhouse";

        public string OutsourcedTo { get; init; } = string.Empty;

        public bool HasCostVariation { get; init; }

        public decimal? VariationCost { get; init; }

        public string PriorityLevel { get; init; } = "Normal";

        public string ProcessStatus { get; init; } = string.Empty;

        public DateTime? DeliveryDate { get; init; }
    }

    public sealed class MaterialPlanningListResponseDto
    {
        public MaterialPlanningSummaryDto Summary { get; init; } = new();

        public IReadOnlyList<MaterialPlanningItemDto> Items { get; init; }
            = Array.Empty<MaterialPlanningItemDto>();

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalRecords { get; init; }

        public int TotalPages => Math.Max(
            1,
            (int)Math.Ceiling(TotalRecords / (double)PageSize));
    }
}
