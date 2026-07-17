using System;
using System.Collections.Generic;

namespace POTracker.ViewModels
{
    public class PendingMaterialIndexViewModel
    {
        public List<PendingMaterialRowViewModel> Items { get; set; } = new();

        public PendingMaterialSummaryViewModel Summary { get; set; } = new();

        // Search and filters
        public string? Search { get; set; }

        public string? PONumber { get; set; }

        public string? Handler { get; set; }

        public string? AssignedDashboard { get; set; }

        public string? ProcessStatus { get; set; }

        public string? InvoiceStatus { get; set; }

        public string? DispatchStatus { get; set; }

        public DateTime? PODateFrom { get; set; }

        public DateTime? PODateTo { get; set; }

        public DateTime? POReceivedDateFrom { get; set; }

        public DateTime? POReceivedDateTo { get; set; }

        public DateTime? DeliveryDateFrom { get; set; }

        public DateTime? DeliveryDateTo { get; set; }

        public bool OverdueOnly { get; set; }

        public bool DueThisWeekOnly { get; set; }

        // Dropdowns
        public List<PendingMaterialFilterOptionViewModel> Handlers { get; set; } = new();

        public List<PendingMaterialFilterOptionViewModel> AssignedDashboards { get; set; } = new();

        public List<PendingMaterialFilterOptionViewModel> ProcessStatuses { get; set; } = new();

        public List<PendingMaterialFilterOptionViewModel> InvoiceStatuses { get; set; } = new();

        public List<PendingMaterialFilterOptionViewModel> DispatchStatuses { get; set; } = new();

        // Pagination
        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 25;

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
                : Math.Min(Page * PageSize, TotalRecords);

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Search) ||
            !string.IsNullOrWhiteSpace(PONumber) ||
            !string.IsNullOrWhiteSpace(Handler) ||
            !string.IsNullOrWhiteSpace(AssignedDashboard) ||
            !string.IsNullOrWhiteSpace(ProcessStatus) ||
            !string.IsNullOrWhiteSpace(InvoiceStatus) ||
            !string.IsNullOrWhiteSpace(DispatchStatus) ||
            PODateFrom.HasValue ||
            PODateTo.HasValue ||
            POReceivedDateFrom.HasValue ||
            POReceivedDateTo.HasValue ||
            DeliveryDateFrom.HasValue ||
            DeliveryDateTo.HasValue ||
            OverdueOnly ||
            DueThisWeekOnly;

        public int ActiveFilterCount
        {
            get
            {
                var count = 0;

                if (!string.IsNullOrWhiteSpace(Search)) count++;
                if (!string.IsNullOrWhiteSpace(PONumber)) count++;
                if (!string.IsNullOrWhiteSpace(Handler)) count++;
                if (!string.IsNullOrWhiteSpace(AssignedDashboard)) count++;
                if (!string.IsNullOrWhiteSpace(ProcessStatus)) count++;
                if (!string.IsNullOrWhiteSpace(InvoiceStatus)) count++;
                if (!string.IsNullOrWhiteSpace(DispatchStatus)) count++;
                if (PODateFrom.HasValue) count++;
                if (PODateTo.HasValue) count++;
                if (POReceivedDateFrom.HasValue) count++;
                if (POReceivedDateTo.HasValue) count++;
                if (DeliveryDateFrom.HasValue) count++;
                if (DeliveryDateTo.HasValue) count++;
                if (OverdueOnly) count++;
                if (DueThisWeekOnly) count++;

                return count;
            }
        }
    }

    public class PendingMaterialRowViewModel
    {
        public int MaterialId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string PONumber { get; set; } = string.Empty;

        public string Handler { get; set; } = string.Empty;

        public DateTime? PODate { get; set; }

        public DateTime? POReceivedDate { get; set; }

        public string LineItem { get; set; } = string.Empty;

        public string MaterialNo { get; set; } = string.Empty;

        public string? HsnSacCode { get; set; }

        public string? ShortDescription { get; set; }

        public string? BriefDescription { get; set; }

        public string? Unit { get; set; }

        public string? Department { get; set; }

        public string? DrawingNo { get; set; }

        public string? DrawingVersion { get; set; }

        public decimal POQuantity { get; set; }

        public decimal InvoiceQuantity { get; set; }

        public decimal InvoiceBalanceQuantity { get; set; }

        public decimal DispatchQuantity { get; set; }

        public decimal PendingDispatchQuantity { get; set; }

        public decimal StockQuantity { get; set; }

        public decimal UnitCost { get; set; }

        public decimal TotalValue { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public DateTime? ProcessStartDate { get; set; }

        public DateTime? AlertDate { get; set; }

        public string ProcessStatus { get; set; } = "Pending";

        public string InvoiceStatus { get; set; } = "Not Invoiced";

        public string DispatchStatus { get; set; } = "Pending";

        public string? AssignedDashboard { get; set; }

        public string? Remarks { get; set; }

        public string? PdfFilePath { get; set; }

        public string? DrawingFilePath { get; set; }

        public decimal CompletionPercentage
        {
            get
            {
                if (POQuantity <= 0)
                {
                    return 0;
                }

                var completed =
                    Math.Max(
                        InvoiceQuantity,
                        DispatchQuantity);

                return Math.Min(
                    100,
                    Math.Max(
                        0,
                        completed / POQuantity * 100));
            }
        }

        public int OverdueDays
        {
            get
            {
                if (!DeliveryDate.HasValue ||
                    PendingDispatchQuantity <= 0 ||
                    DeliveryDate.Value.Date >= DateTime.Today)
                {
                    return 0;
                }

                return (
                    DateTime.Today -
                    DeliveryDate.Value.Date).Days;
            }
        }

        public bool IsOverdue =>
            DeliveryDate.HasValue &&
            DeliveryDate.Value.Date < DateTime.Today &&
            PendingDispatchQuantity > 0;

        public bool IsDueToday =>
            DeliveryDate.HasValue &&
            DeliveryDate.Value.Date == DateTime.Today &&
            PendingDispatchQuantity > 0;

        public bool IsDueThisWeek
        {
            get
            {
                if (!DeliveryDate.HasValue ||
                    PendingDispatchQuantity <= 0)
                {
                    return false;
                }

                var today = DateTime.Today;
                var weekEnd = today.AddDays(7);

                return DeliveryDate.Value.Date >= today &&
                       DeliveryDate.Value.Date <= weekEnd;
            }
        }

        public string DueStatus
        {
            get
            {
                if (!DeliveryDate.HasValue) return "No Delivery Date";
                if (IsOverdue) return "Overdue";
                if (IsDueToday) return "Due Today";
                if (IsDueThisWeek) return "Due This Week";
                return "Upcoming";
            }
        }
    }

    public class PendingMaterialSummaryViewModel
    {
        public int TotalPendingMaterials { get; set; }

        public int OverdueMaterials { get; set; }

        public int DueTodayMaterials { get; set; }

        public int DueThisWeekMaterials { get; set; }

        public int WithoutDeliveryDate { get; set; }

        public decimal TotalPOQuantity { get; set; }

        public decimal TotalInvoiceQuantity { get; set; }

        public decimal TotalInvoiceBalanceQuantity { get; set; }

        public decimal TotalDispatchQuantity { get; set; }

        public decimal TotalPendingDispatchQuantity { get; set; }

        public decimal TotalPendingValue { get; set; }

        public List<PendingMaterialAssignmentSummaryViewModel>
            AssignmentSummary
        { get; set; } = new();
    }

    public class PendingMaterialAssignmentSummaryViewModel
    {
        public string AssignedDashboard { get; set; } = "Unassigned";

        public int MaterialCount { get; set; }

        public decimal PendingQuantity { get; set; }

        public int OverdueCount { get; set; }
    }

    public class PendingMaterialFilterOptionViewModel
    {
        public string Value { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public int Count { get; set; }

        public bool IsSelected { get; set; }
    }
}