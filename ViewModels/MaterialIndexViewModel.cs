using System;
using System.Collections.Generic;

namespace POTracker.ViewModels
{
    /// <summary>
    /// Complete page model for the premium Materials list.
    /// Purchaser is intentionally not included in the UI.
    /// </summary>
    public class MaterialIndexViewModel
    {
        // ============================================================
        // MATERIAL ROWS
        // ============================================================

        public List<MaterialIndexRowViewModel> Items { get; set; } = new();

        // ============================================================
        // SUMMARY CARDS
        // ============================================================

        public MaterialIndexSummaryViewModel Summary { get; set; } = new();

        // ============================================================
        // SEARCH AND FILTER VALUES
        // ============================================================

        /// <summary>
        /// Searches material number, description, line item,
        /// drawing number and HSN/SAC.
        /// </summary>
        public string? Search { get; set; }

        public string? PONumber { get; set; }

        /// <summary>
        /// Buyer contact / material handling person.
        /// Example: Manoj S
        /// </summary>
        public string? Handler { get; set; }

        public DateTime? PODateFrom { get; set; }

        public DateTime? PODateTo { get; set; }

        public DateTime? POReceivedDateFrom { get; set; }

        public DateTime? POReceivedDateTo { get; set; }

        public string? InvoiceStatus { get; set; }

        public string? DispatchStatus { get; set; }

        public string? StockStatus { get; set; }

        // ============================================================
        // FILTER DROPDOWNS
        // ============================================================

        public List<MaterialFilterOptionViewModel> Handlers { get; set; } = new();

        public List<MaterialFilterOptionViewModel> InvoiceStatuses { get; set; } = new();

        public List<MaterialFilterOptionViewModel> DispatchStatuses { get; set; } = new();

        public List<MaterialFilterOptionViewModel> StockStatuses { get; set; } = new();

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
            !string.IsNullOrWhiteSpace(PONumber) ||
            !string.IsNullOrWhiteSpace(Handler) ||
            PODateFrom.HasValue ||
            PODateTo.HasValue ||
            POReceivedDateFrom.HasValue ||
            POReceivedDateTo.HasValue ||
            !string.IsNullOrWhiteSpace(InvoiceStatus) ||
            !string.IsNullOrWhiteSpace(DispatchStatus) ||
            !string.IsNullOrWhiteSpace(StockStatus);

        public int ActiveFilterCount
        {
            get
            {
                var count = 0;

                if (!string.IsNullOrWhiteSpace(Search))
                {
                    count++;
                }

                if (!string.IsNullOrWhiteSpace(PONumber))
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

                if (!string.IsNullOrWhiteSpace(InvoiceStatus))
                {
                    count++;
                }

                if (!string.IsNullOrWhiteSpace(DispatchStatus))
                {
                    count++;
                }

                if (!string.IsNullOrWhiteSpace(StockStatus))
                {
                    count++;
                }

                return count;
            }
        }
    }

    /// <summary>
    /// One material row displayed in the table and Excel export.
    /// </summary>
    public class MaterialIndexRowViewModel
    {
        // ============================================================
        // IDENTIFIERS
        // ============================================================

        public int MaterialId { get; set; }

        public int PurchaseOrderId { get; set; }

        // ============================================================
        // PURCHASE ORDER INFORMATION
        // ============================================================

        public string PONumber { get; set; } = string.Empty;

        /// <summary>
        /// Material handling person / buyer contact.
        /// </summary>
        public string Handler { get; set; } = string.Empty;

        public DateTime? PODate { get; set; }

        /// <summary>
        /// Date of emission from the imported PDF.
        /// </summary>
        public DateTime? POReceivedDate { get; set; }

        // ============================================================
        // MATERIAL INFORMATION
        // ============================================================

        public string LineItem { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string? HsnSacCode { get; set; }

        public string Description { get; set; } = string.Empty;

        public string? BriefDescription { get; set; }

        public string Unit { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }

        public string PriceUnit { get; set; } = string.Empty;

        public string? DrawingNo { get; set; }

        // ============================================================
        // QUANTITIES
        // ============================================================

        public decimal POQuantity { get; set; }

        public decimal InvoiceQuantity { get; set; }

        public decimal BalanceQuantity { get; set; }

        public decimal DispatchQuantity { get; set; }

        public decimal PendingDispatchQuantity { get; set; }

        public decimal LiveStockQuantity { get; set; }

        public decimal ShortageQuantity { get; set; }

        // ============================================================
        // STATUS
        // ============================================================

        public string InvoiceStatus { get; set; } = "Not Invoiced";

        public string DispatchStatus { get; set; } = "Pending";

        public string StockStatus { get; set; } = "Not Found";

        public string ProcessStatus { get; set; } = "Pending";

        // ============================================================
        // DISPLAY HELPERS
        // ============================================================

        public bool IsFullyInvoiced =>
            BalanceQuantity <= 0 ||
            InvoiceStatus.Equals(
                "Fully Invoiced",
                StringComparison.OrdinalIgnoreCase);

        public bool IsFullyDispatched =>
            PendingDispatchQuantity <= 0 ||
            DispatchStatus.Equals(
                "Dispatched",
                StringComparison.OrdinalIgnoreCase);

        public bool HasLiveStock =>
            LiveStockQuantity > 0;

        public bool HasShortage =>
            ShortageQuantity > 0;

        public decimal InvoiceCompletionPercentage
        {
            get
            {
                if (POQuantity <= 0)
                {
                    return 0;
                }

                var percentage =
                    InvoiceQuantity / POQuantity * 100;

                return Math.Min(
                    100,
                    Math.Max(0, percentage));
            }
        }

        public decimal DispatchCompletionPercentage
        {
            get
            {
                if (POQuantity <= 0)
                {
                    return 0;
                }

                var percentage =
                    DispatchQuantity / POQuantity * 100;

                return Math.Min(
                    100,
                    Math.Max(0, percentage));
            }
        }

        // ============================================================
        // OPTIONAL FILE LINKS
        // ============================================================

        public string? DrawingFilePath { get; set; }

        public string? PdfFilePath { get; set; }
    }

    /// <summary>
    /// Top summary cards shown above the Materials table.
    /// </summary>
    public class MaterialIndexSummaryViewModel
    {
        public int TotalMaterials { get; set; }

        public int PendingDispatchMaterials { get; set; }

        public int NotInvoicedMaterials { get; set; }

        public int InStockMaterials { get; set; }

        public int ShortageMaterials { get; set; }

        public decimal TotalPOQuantity { get; set; }

        public decimal TotalBalanceQuantity { get; set; }

        public decimal TotalPendingDispatchQuantity { get; set; }

        public decimal TotalLiveStockQuantity { get; set; }

        /// <summary>
        /// True when live-stock totals represent only the currently
        /// displayed page. Database-based totals always represent the
        /// complete filtered result set.
        /// </summary>
        public bool StockSummaryIsCurrentPage { get; set; }
    }

    /// <summary>
    /// Generic dropdown option for Handler and status filters.
    /// </summary>
    public class MaterialFilterOptionViewModel
    {
        public string Value { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public int Count { get; set; }

        public bool IsSelected { get; set; }
    }
}

