using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsLoggedIn()
        {
            return HttpContext.Session
                .GetString("UserName") != null;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var nextWeek = today.AddDays(7);

            // =====================================================
            // BASIC TOTALS
            // =====================================================

            ViewBag.TotalPurchasers =
                await _context.Purchasers
                    .CountAsync();

            ViewBag.TotalPOs =
                await _context.PurchaseOrders
                    .CountAsync();

            ViewBag.LowStock =
                await _context.Stocks
                    .CountAsync(x =>
                        x.AvailableQty <=
                        x.MinimumQty);

            ViewBag.TodayAlerts =
                await _context.Alerts
                    .CountAsync(x =>
                        !x.IsRead &&
                        x.AlertDate < tomorrow);

            // =====================================================
            // MATERIAL SUMMARY
            // All material totals are calculated in one SQL query.
            // =====================================================

            var materialStats =
                await _context.Materials
                    .AsNoTracking()
                    .GroupBy(x => 1)
                    .Select(group => new
                    {
                        TotalMaterials =
                            group.Count(),

                        PendingProcess =
                            group.Count(x =>
                                x.ProcessStatus ==
                                "Pending"),

                        RunningProcess =
                            group.Count(x =>
                                x.ProcessStatus ==
                                    "In Process" ||
                                x.ProcessStatus ==
                                    "In Progress"),

                        CompletedProcess =
                            group.Count(x =>
                                x.ProcessStatus ==
                                "Completed"),

                        ProcessStartAlerts =
                            group.Count(x =>
                                x.AlertDate != null &&
                                x.AlertDate <= today &&
                                x.ProcessStatus !=
                                    "Completed"),

                        Overdue =
                            group.Count(x =>
                                x.DeliveryDate != null &&
                                x.DeliveryDate < today &&
                                x.ProcessStatus !=
                                    "Completed"),

                        DeliveryWithin7Days =
                            group.Count(x =>
                                x.DeliveryDate != null &&
                                x.DeliveryDate >= today &&
                                x.DeliveryDate <= nextWeek &&
                                x.ProcessStatus !=
                                    "Completed"),

                        DispatchCompleted =
                            group.Count(x =>
                                x.DispatchStatus ==
                                "Dispatched"),

                        DispatchPending =
                            group.Count(x =>
                                x.DispatchStatus ==
                                "Pending"),

                        DispatchPartial =
                            group.Count(x =>
                                x.DispatchStatus ==
                                "Partial"),

                        DispatchPendingOrPartial =
                            group.Count(x =>
                                x.DispatchStatus ==
                                    "Pending" ||
                                x.DispatchStatus ==
                                    "Partial"),

                        LowStockMaterials =
                            group.Count(x =>
                                x.StockQty <= 5),

                        FullyInvoiced =
                            group.Count(x =>
                                x.InvoiceStatus ==
                                "Fully Invoiced"),

                        PartiallyInvoiced =
                            group.Count(x =>
                                x.InvoiceStatus ==
                                "Partially Invoiced"),

                        NotInvoiced =
                            group.Count(x =>
                                x.InvoiceQty <= 0 ||
                                x.InvoiceStatus == "" ||
                                x.InvoiceStatus ==
                                    "Not Invoiced"),

                        TotalPOQty =
                            group.Sum(x =>
                                x.Qty),

                        TotalInvoiceQty =
                            group.Sum(x =>
                                x.InvoiceQty),

                        TotalDispatchQty =
                            group.Sum(x =>
                                x.DispatchQty),

                        TotalPendingQty =
                            group.Sum(x =>
                                x.PendingQty > 0
                                    ? x.PendingQty
                                    : 0m)
                    })
                    .FirstOrDefaultAsync();

            // =====================================================
            // PROCESS SUMMARY
            // =====================================================

            ViewBag.TotalMaterials =
                materialStats?.TotalMaterials ?? 0;

            ViewBag.PendingMaterials =
                materialStats?.PendingProcess ?? 0;

            ViewBag.PendingCount =
                materialStats?.PendingProcess ?? 0;

            ViewBag.RunningCount =
                materialStats?.RunningProcess ?? 0;

            ViewBag.CompletedCount =
                materialStats?.CompletedProcess ?? 0;

            // =====================================================
            // ALERT SUMMARY
            // =====================================================

            ViewBag.ProcessStartAlertCount =
                materialStats?.ProcessStartAlerts ?? 0;

            ViewBag.OverdueCount =
                materialStats?.Overdue ?? 0;

            ViewBag.DeliveryWithin7DaysCount =
                materialStats?.DeliveryWithin7Days ?? 0;

            ViewBag.LowStockMaterialCount =
                materialStats?.LowStockMaterials ?? 0;

            // =====================================================
            // DISPATCH SUMMARY
            // =====================================================

            ViewBag.DispatchPendingCount =
                materialStats?
                    .DispatchPendingOrPartial ?? 0;

            ViewBag.DispatchCompleted =
                materialStats?.DispatchCompleted ?? 0;

            ViewBag.DispatchPending =
                materialStats?.DispatchPending ?? 0;

            ViewBag.DispatchPartial =
                materialStats?.DispatchPartial ?? 0;

            // =====================================================
            // INVOICE SUMMARY
            // =====================================================

            ViewBag.FullyInvoiced =
                materialStats?.FullyInvoiced ?? 0;

            ViewBag.PartiallyInvoiced =
                materialStats?.PartiallyInvoiced ?? 0;

            ViewBag.NotInvoiced =
                materialStats?.NotInvoiced ?? 0;

            // =====================================================
            // QUANTITY SUMMARY
            // =====================================================

            ViewBag.TotalPOQty =
                materialStats?.TotalPOQty ?? 0m;

            ViewBag.TotalInvoiceQty =
                materialStats?.TotalInvoiceQty ?? 0m;

            ViewBag.TotalDispatchQty =
                materialStats?.TotalDispatchQty ?? 0m;

            ViewBag.TotalPendingQty =
                materialStats?.TotalPendingQty ?? 0m;

            // =====================================================
            // RECENT MATERIALS
            // =====================================================

            var recentMaterials =
                await _context.Materials
                    .AsNoTracking()
                    .Include(x =>
                        x.PurchaseOrder)
                    .ThenInclude(x =>
                        x!.Purchaser)
                    .OrderByDescending(x =>
                        x.Id)
                    .Take(8)
                    .ToListAsync();

            return View(recentMaterials);
        }
    }
}