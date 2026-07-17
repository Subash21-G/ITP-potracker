using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace POTracker.ViewModels
{
    /// <summary>
    /// Model displayed on the Excel material-import preview page.
    /// </summary>
    public class MaterialImportPreviewViewModel
    {
        public string BatchId { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<MaterialImportRowViewModel> Rows { get; set; } = new();

        [JsonIgnore]
        public int TotalRows => Rows?.Count ?? 0;

        [JsonIgnore]
        public int ValidRows =>
            Rows?.Count(row => row.IsValid) ?? 0;

        [JsonIgnore]
        public int InvalidRows =>
            Rows?.Count(row => !row.IsValid) ?? 0;

        [JsonIgnore]
        public decimal TotalQuantity =>
            Rows?
                .Where(row => row.IsValid)
                .Sum(row => row.Qty) ?? 0;

        [JsonIgnore]
        public decimal TotalValue =>
            Rows?
                .Where(row => row.IsValid)
                .Sum(row => row.TotalValue) ?? 0;
    }

    /// <summary>
    /// Temporary server-side batch saved before the user confirms import.
    /// </summary>
    public class MaterialImportBatchViewModel
    {
        public string BatchId { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<MaterialImportRowViewModel> Rows { get; set; } = new();
    }

    /// <summary>
    /// One material row extracted from the uploaded Excel worksheet.
    /// </summary>
    public class MaterialImportRowViewModel
    {
        public int RowNumber { get; set; }

        // ============================================================
        // PURCHASER AND PURCHASE ORDER
        // ============================================================

        public string PurchaserName { get; set; } = string.Empty;

        public string? ContactPerson { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public DateTime? PODate { get; set; }

        public DateTime? ReceivedDate { get; set; }

        public string? BuyerContactPerson { get; set; }

        // ============================================================
        // MATERIAL DETAILS
        // ============================================================

        public string LineItem { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string? HsnSacCode { get; set; }

        public string? ShortDescription { get; set; }

        public string? BriefDescription { get; set; }

        public decimal Qty { get; set; }

        public string? Unit { get; set; }

        public string? Dep { get; set; }

        // ============================================================
        // COST DETAILS
        // ============================================================

        public decimal ValueOfGoods { get; set; }

        public string? PriceUnit { get; set; }

        public decimal TotalValue { get; set; }

        // ============================================================
        // DELIVERY AND DRAWING
        // ============================================================

        public DateTime? DeliveryDate { get; set; }

        public string? DrawingNo { get; set; }

        // ============================================================
        // ASSIGNMENT AND REMARK
        // ============================================================

        public string? AssignedDashboard { get; set; }

        public string? Remark { get; set; }

        // ============================================================
        // ROW VALIDATION
        // ============================================================

        public List<string> Errors { get; set; } = new();

        [JsonIgnore]
        public bool IsValid =>
            Errors == null || Errors.Count == 0;

        [JsonIgnore]
        public string ErrorMessage =>
            Errors == null || Errors.Count == 0
                ? string.Empty
                : string.Join(" | ", Errors);
    }
}