using System.ComponentModel.DataAnnotations;

namespace POTracker.Contracts.Api
{
    /// <summary>
    /// Purchaser option displayed in Angular purchase-order forms.
    /// </summary>
    public sealed class PurchaseOrderPurchaserOptionDto
    {
        public int Id { get; init; }

        public string PurchaserName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Complete purchase-order details returned to Angular.
    /// </summary>
    public sealed class PurchaseOrderCrudDetailsDto
    {
        public int Id { get; init; }

        public int PurchaserId { get; init; }

        public string PurchaserName { get; init; } = string.Empty;

        public string PONumber { get; init; } = string.Empty;

        public DateTime? PODate { get; init; }

        /// <summary>
        /// PO received / date of emission.
        /// </summary>
        public DateTime? ReceivedDate { get; init; }

        public string BuyerContactPerson { get; init; } = string.Empty;

        public string PdfFilePath { get; init; } = string.Empty;

        public DateTime CreatedDate { get; init; }

        public int MaterialCount { get; init; }

        public decimal TotalQuantity { get; init; }

        public decimal TotalInvoiceQuantity { get; init; }

        public decimal TotalDispatchQuantity { get; init; }

        public decimal TotalPendingQuantity { get; init; }

        public decimal TotalValue { get; init; }

        /// <summary>
        /// A PO can be deleted only when it has no material rows.
        /// </summary>
        public bool CanDelete => MaterialCount == 0;
    }

    /// <summary>
    /// Editable values accepted by Angular create and edit forms.
    /// </summary>
    public sealed class PurchaseOrderUpsertRequestDto
    {
        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "Purchaser is required.")]
        public int PurchaserId { get; set; }

        [Required(
            ErrorMessage = "PO number is required.")]
        [StringLength(
            100,
            ErrorMessage = "PO number must not exceed 100 characters.")]
        public string PONumber { get; set; } = string.Empty;

        public DateTime? PODate { get; set; }

        /// <summary>
        /// PO received / date of emission.
        /// </summary>
        public DateTime? ReceivedDate { get; set; }

        [StringLength(
            200,
            ErrorMessage = "Buyer contact person must not exceed 200 characters.")]
        public string? BuyerContactPerson { get; set; }
    }

    /// <summary>
    /// Standard create/edit response returned to Angular.
    /// </summary>
    public sealed class PurchaseOrderSaveResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int PurchaseOrderId { get; init; }

        public PurchaseOrderCrudDetailsDto? PurchaseOrder { get; init; }
    }

    /// <summary>
    /// Standard delete response returned to Angular.
    /// </summary>
    public sealed class PurchaseOrderDeleteResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int PurchaseOrderId { get; init; }
    }
}
