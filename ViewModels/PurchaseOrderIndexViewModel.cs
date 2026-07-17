using System;
using System.Collections.Generic;

namespace POTracker.ViewModels
{
    /// <summary>
    /// Complete page model for the premium Purchase Orders list.
    /// Purchaser is intentionally not displayed.
    /// </summary>
    public class PurchaseOrderIndexViewModel
    {
        // ============================================================
        // PURCHASE ORDER ROWS
        // ============================================================

        public List<PurchaseOrderIndexRowViewModel>
            Items
        { get; set; } = new();

        // ============================================================
        // SUMMARY CARDS
        // ============================================================

        public PurchaseOrderIndexSummaryViewModel
            Summary
        { get; set; } = new();

        // ============================================================
        // SEARCH AND FILTER VALUES
        // ============================================================

        /// <summary>
        /// Searches PO number and handler/person.
        /// </summary>
        public string? Search { get; set; }

        public string? Handler { get; set; }

        public DateTime? PODateFrom { get; set; }

        public DateTime? PODateTo { get; set; }

        public DateTime? POReceivedDateFrom { get; set; }

        public DateTime? POReceivedDateTo { get; set; }

        public string? Status { get; set; }

        public string? MaterialStatus { get; set; }

        // ============================================================
        // FILTER DROPDOWNS
        // ============================================================

        public List<PurchaseOrderFilterOptionViewModel>
            Handlers
        { get; set; } = new();

        public List<PurchaseOrderFilterOptionViewModel>
            Statuses
        { get; set; } = new();

        public List<PurchaseOrderFilterOptionViewModel>
            MaterialStatuses
        { get; set; } = new();

        // ============================================================
        // PAGINATION
        // ============================================================

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;

        public int TotalRecords { get; set; }

        public int TotalPages =>
            PageSize <= 0
                ? 1
                : Math.Max(
                    1,
                    (int)Math.Ceiling(
                        TotalRecords / (double)PageSize));

        public bool HasPreviousPage => Page > 1;

        public bool HasNextPage => Page < TotalPages;

        public int FirstRecordNumber =>
            TotalRecords == 0
                ? 0
                : ((Page - 1) * PageSize) + 1;

        public int LastRecordNumber =>
            TotalRecords == 0
                ? 0
                : Math.Min(
                    Page * PageSize,
                    TotalRecords);

        // ============================================================
        // ACTIVE FILTER INFORMATION
        // ============================================================

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Search) ||
            !string.IsNullOrWhiteSpace(Handler) ||
            PODateFrom.HasValue ||
            PODateTo.HasValue ||
            POReceivedDateFrom.HasValue ||
            POReceivedDateTo.HasValue ||
            !string.IsNullOrWhiteSpace(Status) ||
            !string.IsNullOrWhiteSpace(MaterialStatus);

        public int ActiveFilterCount
        {
            get
            {
                var count = 0;

                if (!string.IsNullOrWhiteSpace(Search))
                {
                    count++;
                }

                if (!string.IsNullOrWhiteSpace(Handler))
                {
                    count++;
                }

                if (PODateFrom.HasValue)
                {
                    count++;
                }

                if (PODateTo.HasValue)
                {
                    count++;
                }

                if (POReceivedDateFrom.HasValue)
                {
                    count++;
                }

                if (POReceivedDateTo.HasValue)
                {
                    count++;
                }

                if (!string.IsNullOrWhiteSpace(Status))
                {
                    count++;
                }

                if (!string.IsNullOrWhiteSpace(
                        MaterialStatus))
                {
                    count++;
                }

                return count;
            }
        }
    }

    /// <summary>
    /// One Purchase Order row displayed in the list and Excel export.
    /// </summary>
    public class PurchaseOrderIndexRowViewModel
    {
        // ============================================================
        // PURCHASE ORDER DETAILS
        // ============================================================

        public int PurchaseOrderId { get; set; }

        public string PONumber { get; set; } = string.Empty;

        /// <summary>
        /// Buyer contact / material handling person.
        /// Example: Manoj S
        /// </summary>
        public string Handler { get; set; } = string.Empty;

        /// <summary>
        /// Date of Order from the PO PDF.
        /// </summary>
        public DateTime? PODate { get; set; }

        /// <summary>
        /// Date of emission from the PO PDF.
        /// This is treated as PO Received Date.
        /// </summary>
        public DateTime? POReceivedDate { get; set; }

        public string Status { get; set; } = "Open";

        public string? PdfFilePath { get; set; }

        public DateTime CreatedDate { get; set; }

        // ============================================================
        // MATERIAL COUNTS
        // ============================================================

        public int TotalMaterials { get; set; }

        public int PendingMaterials { get; set; }

        public int CompletedMaterials { get; set; }

        public int NotInvoicedMaterials { get; set; }

        public int PendingDispatchMaterials { get; set; }

        public int OverdueMaterials { get; set; }

        // ============================================================
        // QUANTITY SUMMARY
        // ============================================================

        public decimal TotalPOQuantity { get; set; }

        public decimal TotalInvoiceQuantity { get; set; }

        public decimal TotalInvoiceBalanceQuantity { get; set; }

        public decimal TotalDispatchQuantity { get; set; }

        public decimal TotalPendingDispatchQuantity { get; set; }

        // ============================================================
        // DATE SUMMARY
        // ============================================================

        public DateTime? EarliestDeliveryDate { get; set; }

        public DateTime? LatestDeliveryDate { get; set; }

        // ============================================================
        // DISPLAY HELPERS
        // ============================================================

        public string HandlerDisplay =>
            string.IsNullOrWhiteSpace(Handler)
                ? "Unassigned"
                : Handler;

        public string PODateDisplay =>
            PODate?.ToString("dd-MMM-yyyy") ??
            "Not set";

        public string POReceivedDateDisplay =>
            POReceivedDate?.ToString("dd-MMM-yyyy") ??
            "Not set";

        public string MaterialStatus
        {
            get
            {
                if (TotalMaterials <= 0)
                {
                    return "No Materials";
                }

                if (PendingMaterials <= 0)
                {
                    return "Completed";
                }

                if (CompletedMaterials > 0)
                {
                    return "Partially Completed";
                }

                return "Pending";
            }
        }

        public decimal CompletionPercentage
        {
            get
            {
                if (TotalMaterials <= 0)
                {
                    return 0;
                }

                var percentage =
                    CompletedMaterials /
                    (decimal)TotalMaterials *
                    100;

                return Math.Min(
                    100,
                    Math.Max(
                        0,
                        percentage));
            }
        }

        public bool HasPendingMaterials =>
            PendingMaterials > 0;

        public bool HasOverdueMaterials =>
            OverdueMaterials > 0;

        public bool HasPdf =>
            !string.IsNullOrWhiteSpace(
                PdfFilePath);
    }

    /// <summary>
    /// Purchase Order list summary cards.
    /// </summary>
    public class PurchaseOrderIndexSummaryViewModel
    {
        public int TotalPurchaseOrders { get; set; }

        public int OpenPurchaseOrders { get; set; }

        public int CompletedPurchaseOrders { get; set; }

        public int PurchaseOrdersWithPendingMaterials { get; set; }

        public int PurchaseOrdersWithOverdueMaterials { get; set; }

        public int PurchaseOrdersWithoutHandler { get; set; }

        public decimal TotalPOQuantity { get; set; }

        public decimal TotalInvoiceBalanceQuantity { get; set; }

        public decimal TotalPendingDispatchQuantity { get; set; }
    }

    /// <summary>
    /// Generic dropdown option.
    /// </summary>
    public class PurchaseOrderFilterOptionViewModel
    {
        public string Value { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public int Count { get; set; }

        public bool IsSelected { get; set; }
    }
}