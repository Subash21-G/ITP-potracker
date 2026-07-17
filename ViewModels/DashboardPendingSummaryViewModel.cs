using System;
using System.Collections.Generic;

namespace POTracker.ViewModels
{
    /// <summary>
    /// Summary data displayed inside the dashboard
    /// Pending Materials widget.
    /// </summary>
    public class DashboardPendingSummaryViewModel
    {
        // ============================================================
        // SUMMARY CARDS
        // ============================================================

        public int TotalPendingMaterials { get; set; }

        public int OverdueMaterials { get; set; }

        public int DueTodayMaterials { get; set; }

        public int DueThisWeekMaterials { get; set; }

        public int UnassignedMaterials { get; set; }

        public decimal PendingInvoiceQuantity { get; set; }

        public decimal PendingDispatchQuantity { get; set; }

        // ============================================================
        // PRIORITY MATERIAL LIST
        // ============================================================

        public List<DashboardPendingMaterialRowViewModel>
            PriorityItems
        { get; set; } = new();
    }

    /// <summary>
    /// One pending-material row displayed in the dashboard widget.
    /// Purchaser is intentionally removed.
    /// </summary>
    public class DashboardPendingMaterialRowViewModel
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
        /// Material handler / buyer contact person.
        /// Example: Manoj S
        /// </summary>
        public string Handler { get; set; } = string.Empty;

        /// <summary>
        /// Date of Order from the PO PDF.
        /// </summary>
        public DateTime? PODate { get; set; }

        /// <summary>
        /// Date of emission from the PO PDF.
        /// This is treated as the PO Received Date.
        /// </summary>
        public DateTime? POReceivedDate { get; set; }

        // ============================================================
        // MATERIAL INFORMATION
        // ============================================================

        public string MaterialNo { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Unit { get; set; } = string.Empty;

        public string? LineItem { get; set; }

        public string? DrawingNo { get; set; }

        // ============================================================
        // QUANTITIES
        // ============================================================

        public decimal POQuantity { get; set; }

        public decimal InvoiceBalanceQuantity { get; set; }

        public decimal PendingQuantity { get; set; }

        // ============================================================
        // DELIVERY AND RESPONSIBILITY
        // ============================================================

        public DateTime? DeliveryDate { get; set; }

        public string AssignedDashboard { get; set; } = string.Empty;

        public string ProcessStatus { get; set; } = "Pending";

        public string InvoiceStatus { get; set; } = "Not Invoiced";

        public string DispatchStatus { get; set; } = "Pending";

        // ============================================================
        // DELIVERY STATUS HELPERS
        // ============================================================

        public bool IsOverdue =>
            DeliveryDate.HasValue &&
            DeliveryDate.Value.Date < DateTime.Today &&
            PendingQuantity > 0;

        public bool IsDueToday =>
            DeliveryDate.HasValue &&
            DeliveryDate.Value.Date == DateTime.Today &&
            PendingQuantity > 0;

        public bool IsDueThisWeek
        {
            get
            {
                if (!DeliveryDate.HasValue ||
                    PendingQuantity <= 0)
                {
                    return false;
                }

                var today = DateTime.Today;
                var weekEnd = today.AddDays(7);

                return DeliveryDate.Value.Date >= today &&
                       DeliveryDate.Value.Date <= weekEnd;
            }
        }

        public int OverdueDays
        {
            get
            {
                if (!IsOverdue ||
                    !DeliveryDate.HasValue)
                {
                    return 0;
                }

                return (
                    DateTime.Today -
                    DeliveryDate.Value.Date).Days;
            }
        }

        public string DueText
        {
            get
            {
                if (!DeliveryDate.HasValue)
                {
                    return "No delivery date";
                }

                if (IsOverdue)
                {
                    return $"{OverdueDays} day(s) late";
                }

                if (IsDueToday)
                {
                    return "Due today";
                }

                if (IsDueThisWeek)
                {
                    return "Due this week";
                }

                return "Upcoming";
            }
        }

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

        public string AssignedPersonDisplay =>
            string.IsNullOrWhiteSpace(
                AssignedDashboard)
                ? "Unassigned"
                : AssignedDashboard;
    }
}