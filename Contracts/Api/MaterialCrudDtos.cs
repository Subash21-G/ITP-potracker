using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POTracker.Contracts.Api
{
    /// <summary>
    /// Purchase-order option displayed in the Angular material form.
    /// </summary>
    public sealed class MaterialPurchaseOrderOptionDto
    {
        public int Id { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public string Handler { get; init; } = string.Empty;

        public DateTime? PODate { get; init; }

        public DateTime? ReceivedDate { get; init; }
    }

    /// <summary>
    /// Purchase order containing the material number shown on a material detail page.
    /// </summary>
    public sealed class RelatedMaterialPurchaseOrderDto
    {
        public int PurchaseOrderId { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public string Handler { get; init; } = string.Empty;

        public string PurchaserName { get; init; } = string.Empty;

        public int MaterialCount { get; init; }
    }

    /// <summary>
    /// Complete material details returned to Angular.
    /// Invoice and dispatch quantities are read-only because they are
    /// controlled by invoice import and dispatch modules.
    /// </summary>
    public sealed class MaterialDetailsDto
    {
        public int Id { get; init; }

        public int PurchaseOrderId { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public string Handler { get; init; } = string.Empty;

        public string PurchaserName { get; init; } = string.Empty;

        // PO ITEM DETAILS
        public string LineItem { get; init; } = string.Empty;

        public string ItemType { get; init; } = string.Empty;

        public string MaterialNo { get; init; } = string.Empty;

        public string HsnSacCode { get; init; } = string.Empty;

        public string ShortDescription { get; init; } = string.Empty;

        public string BriefDescription { get; init; } = string.Empty;

        public string Receiver { get; init; } = string.Empty;

        public string UnloadingPoint { get; init; } = string.Empty;

        // QUANTITY DETAILS
        public decimal Qty { get; init; }

        public string Unit { get; init; } = string.Empty;

        public string Dep { get; init; } = string.Empty;

        public decimal DispatchQty { get; init; }

        public decimal PendingQty { get; init; }


        public decimal LiveStockQuantity { get; set; }

        public decimal AllocatedStockQuantity { get; set; }

        public decimal StockMinimumQuantity { get; set; }

        public decimal StockShortageQuantity { get; set; }

        public string StockStatus { get; set; } = "Not Found";

        public int StockRecordCount { get; set; }

        public bool StockDataLive { get; set; }

        // INVOICE DETAILS
        public decimal InvoiceQty { get; init; }

        public decimal BalanceQty { get; init; }

        public string InvoiceStatus { get; init; } = string.Empty;

        // COST DETAILS
        public decimal Cost { get; init; }

        public decimal ValueOfGoods { get; init; }

        public string PriceUnit { get; init; } = string.Empty;

        public decimal TotalValue { get; init; }

        public decimal CGSTRate { get; init; }

        public decimal CGSTAmount { get; init; }

        public decimal SGSTRate { get; init; }

        public decimal SGSTAmount { get; init; }

        public decimal IGSTRate { get; init; }

        public decimal IGSTAmount { get; init; }

        public decimal TotalValueWithTax { get; init; }

        // RAW MATERIAL
        public string RMGrade { get; init; } = string.Empty;

        public string RMSource { get; init; } = string.Empty;

        public string RMProcess { get; init; } = string.Empty;

        // SURFACE / MACHINING
        public string SurfaceFinish { get; init; } = string.Empty;

        public string MachiningType { get; init; } = string.Empty;

        // PROCESS TIME
        public string ProcessTime { get; init; } = string.Empty;

        public string MaterialProcessTime { get; init; } = string.Empty;

        // DATES
        public DateTime? PODate { get; init; }

        public DateTime? ReceivingDate { get; init; }

        public DateTime? DeliveryDate { get; init; }

        public DateTime? AlertDate { get; init; }

        public DateTime? DispatchDate { get; init; }

        public DateTime? DrawingUpdatedDate { get; init; }

        public DateTime? ProcessStartDate { get; init; }

        // DRAWING / FILES
        public string DrawingNo { get; init; } = string.Empty;

        public string DrawingVersion { get; init; } = string.Empty;

        public string DrawingFilePath { get; set; } = string.Empty;

        public string PdfFilePath { get; set; } = string.Empty;

        // STATUS
        public string ProcessStatus { get; init; } = string.Empty;

        public string DispatchStatus { get; init; } = string.Empty;

        public string AssignedDashboard { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;

        public DateTime CreatedDate { get; init; }

        public DateTime? UpdatedDate { get; init; }

        public int InvoiceItemCount { get; init; }

        public int DispatchRecordCount { get; init; }

        public bool CanDelete =>
            InvoiceItemCount == 0 &&
            DispatchRecordCount == 0;
    }

    /// <summary>
    /// Editable values accepted by Angular create and edit forms.
    /// DispatchQty, InvoiceQty and their calculated statuses are not
    /// accepted here because their own modules control those values.
    /// </summary>
    public sealed class MaterialUpsertRequestDto
    {
        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "Purchase order is required.")]
        public int PurchaseOrderId { get; set; }

        [StringLength(50)]
        public string? LineItem { get; set; }

        [StringLength(10)]
        public string? ItemType { get; set; }

        [StringLength(150)]
        public string? MaterialNo { get; set; }

        [StringLength(100)]
        public string? HsnSacCode { get; set; }

        [StringLength(500)]
        public string? ShortDescription { get; set; }

        public string? BriefDescription { get; set; }

        [StringLength(200)]
        public string? Receiver { get; set; }

        [StringLength(200)]
        public string? UnloadingPoint { get; set; }

        [Range(
            typeof(decimal),
            "0.01",
            "9999999999999999",
            ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Qty { get; set; }

        [StringLength(50)]
        public string? Unit { get; set; }

        [StringLength(100)]
        public string? Dep { get; set; }

        // COST DETAILS
        [Range(
            typeof(decimal),
            "0",
            "9999999999999999",
            ErrorMessage = "Cost cannot be negative.")]
        public decimal Cost { get; set; }

        [Range(
            typeof(decimal),
            "0",
            "9999999999999999",
            ErrorMessage = "Value of goods cannot be negative.")]
        public decimal ValueOfGoods { get; set; }

        [StringLength(50)]
        public string? PriceUnit { get; set; }

        [Range(
            typeof(decimal),
            "0",
            "9999999999999999",
            ErrorMessage = "Total value cannot be negative.")]
        public decimal TotalValue { get; set; }

        // RAW MATERIAL
        [StringLength(200)]
        public string? RMGrade { get; set; }

        [StringLength(200)]
        public string? RMSource { get; set; }

        [StringLength(200)]
        public string? RMProcess { get; set; }

        // SURFACE / MACHINING
        [StringLength(200)]
        public string? SurfaceFinish { get; set; }

        [StringLength(200)]
        public string? MachiningType { get; set; }

        // PROCESS TIME
        [StringLength(100)]
        public string? ProcessTime { get; set; }

        [StringLength(100)]
        public string? MaterialProcessTime { get; set; }

        // DATES
        public DateTime? PODate { get; set; }

        public DateTime? ReceivingDate { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public DateTime? DispatchDate { get; set; }

        public DateTime? DrawingUpdatedDate { get; set; }

        // DRAWING
        [StringLength(200)]
        public string? DrawingNo { get; set; }

        [StringLength(100)]
        public string? DrawingVersion { get; set; }

        // STATUS
        [StringLength(50)]
        public string? ProcessStatus { get; set; }

        [StringLength(100)]
        public string? AssignedDashboard { get; set; }

        public string? Remark { get; set; }
    }

    /// <summary>
    /// Standard create/edit response returned to Angular.
    /// </summary>
    public sealed class MaterialSaveResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int MaterialId { get; init; }

        public int MatchedPendingInvoiceRows { get; init; }

        public MaterialDetailsDto? Material { get; init; }
    }

    /// <summary>
    /// Standard delete response returned to Angular.
    /// </summary>
    public sealed class MaterialDeleteResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int MaterialId { get; init; }
    }
    /// <summary>
    /// Material identifiers selected for bulk deletion.
    /// </summary>
    public sealed class MaterialBulkDeleteRequestDto
    {
        public List<int> MaterialIds { get; init; } = new();
    }

    /// <summary>
    /// Result returned after attempting to delete multiple materials.
    /// Linked invoice and dispatch materials are safely skipped.
    /// </summary>
    public sealed class MaterialBulkDeleteResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int RequestedCount { get; init; }

        public int DeletedCount { get; init; }

        public int SkippedCount { get; init; }

        public int MissingCount { get; init; }

        public int InvoiceLinkedCount { get; init; }

        public int DispatchLinkedCount { get; init; }

        public List<int> DeletedMaterialIds { get; init; } = new();
    }
}