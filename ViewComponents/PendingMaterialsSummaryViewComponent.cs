using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.ViewModels;

namespace POTracker.ViewComponents
{
    public class PendingMaterialsSummaryViewComponent
        : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public PendingMaterialsSummaryViewComponent(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // ========================================================
            // LOAD ALL MATERIALS THAT STILL REQUIRE ACTION
            // ========================================================

            var materials = await _context.Materials
                .AsNoTracking()
                .Include(x => x.PurchaseOrder)
                .Where(x =>
                    x.PendingQty > 0 ||
                    x.BalanceQty > 0 ||
                    x.DispatchStatus != "Dispatched" ||
                    x.InvoiceStatus != "Fully Invoiced" ||
                    x.ProcessStatus != "Completed")
                .ToListAsync();

            // ========================================================
            // MAP DATABASE DATA TO DASHBOARD ROWS
            // Purchaser is intentionally not included.
            // ========================================================

            var rows = materials
                .Select(material =>
                    new DashboardPendingMaterialRowViewModel
                    {
                        MaterialId =
                            material.Id,

                        PurchaseOrderId =
                            material.PurchaseOrderId,

                        PONumber =
                            material.PurchaseOrder?
                                .PONumber ?? "",

                        Handler =
                            material.PurchaseOrder?
                                .BuyerContactPerson ?? "",

                        PODate =
                            material.PurchaseOrder?
                                .PODate ??
                            material.PODate,

                        POReceivedDate =
                            material.PurchaseOrder?
                                .ReceivedDate ??
                            material.ReceivingDate,

                        MaterialNo =
                         material.MaterialNo ?? "",

                        Description =
                            !string.IsNullOrWhiteSpace(
                                material.ShortDescription)
                                ? material.ShortDescription
                                : material.BriefDescription ?? "",

                        Unit =
                            material.Unit ?? "",

                        LineItem =
                            material.LineItem,

                        DrawingNo =
                            material.DrawingNo,

                        POQuantity =
                            material.Qty,

                        InvoiceBalanceQuantity =
                            Math.Max(
                                0,
                                material.BalanceQty),

                        PendingQuantity =
                            Math.Max(
                                0,
                                material.PendingQty),

                        DeliveryDate =
                            material.DeliveryDate,

                        AssignedDashboard =
                            material.AssignedDashboard ?? "",

                        ProcessStatus =
                            string.IsNullOrWhiteSpace(
                                material.ProcessStatus)
                                ? "Pending"
                                : material.ProcessStatus,

                        InvoiceStatus =
                            string.IsNullOrWhiteSpace(
                                material.InvoiceStatus)
                                ? "Not Invoiced"
                                : material.InvoiceStatus,

                        DispatchStatus =
                            string.IsNullOrWhiteSpace(
                                material.DispatchStatus)
                                ? "Pending"
                                : material.DispatchStatus
                    })
                .ToList();

            // ========================================================
            // BUILD DASHBOARD SUMMARY
            // ========================================================

            var model =
                new DashboardPendingSummaryViewModel
                {
                    TotalPendingMaterials =
                        rows.Count,

                    OverdueMaterials =
                        rows.Count(row =>
                            row.IsOverdue),

                    DueTodayMaterials =
                        rows.Count(row =>
                            row.IsDueToday),

                    DueThisWeekMaterials =
                        rows.Count(row =>
                            row.IsDueThisWeek),

                    UnassignedMaterials =
                        rows.Count(row =>
                            string.IsNullOrWhiteSpace(
                                row.AssignedDashboard)),

                    PendingInvoiceQuantity =
                        rows.Sum(row =>
                            row.InvoiceBalanceQuantity),

                    PendingDispatchQuantity =
                        rows.Sum(row =>
                            row.PendingQuantity),

                    // Most urgent six materials are shown
                    // on the dashboard widget.
                    PriorityItems =
                        rows
                            .OrderBy(row =>
                                !row.DeliveryDate.HasValue
                                    ? 4
                                    : row.IsOverdue
                                        ? 0
                                        : row.IsDueToday
                                            ? 1
                                            : row.IsDueThisWeek
                                                ? 2
                                                : 3)
                            .ThenBy(row =>
                                row.DeliveryDate)
                            .ThenByDescending(row =>
                                row.PendingQuantity)
                            .ThenBy(row =>
                                row.PONumber)
                            .Take(6)
                            .ToList()
                };

            return View(model);
        }
    }
}