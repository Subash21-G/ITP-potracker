using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetString("UserName") != null;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            DateTime today = DateTime.Today;
            DateTime nextWeek = today.AddDays(7);

            ViewBag.TotalPurchasers = await _context.Purchasers.CountAsync();

            ViewBag.TotalPOs = await _context.PurchaseOrders.CountAsync();

            ViewBag.TotalMaterials = await _context.Materials.CountAsync();

            ViewBag.PendingMaterials = await _context.Materials
                .CountAsync(x => x.ProcessStatus == "Pending");

            ViewBag.LowStock = await _context.Stocks
                .CountAsync(x => x.AvailableQty <= x.MinimumQty);

            ViewBag.TodayAlerts = await _context.Alerts
                .CountAsync(x => x.AlertDate.Date <= today && !x.IsRead);

            ViewBag.ProcessStartAlertCount = await _context.Materials
                .CountAsync(x => x.AlertDate != null
                    && x.AlertDate <= today
                    && x.ProcessStatus != "Completed");

            ViewBag.OverdueCount = await _context.Materials
                .CountAsync(x => x.DeliveryDate != null
                    && x.DeliveryDate < today
                    && x.ProcessStatus != "Completed");

            ViewBag.DeliveryWithin7DaysCount = await _context.Materials
                .CountAsync(x => x.DeliveryDate != null
                    && x.DeliveryDate >= today
                    && x.DeliveryDate <= nextWeek
                    && x.ProcessStatus != "Completed");

            ViewBag.DispatchPendingCount = await _context.Materials
                .CountAsync(x => x.DispatchStatus == "Pending"
                    || x.DispatchStatus == "Partial");

            ViewBag.LowStockMaterialCount = await _context.Materials
                .CountAsync(x => x.StockQty <= 5);

            var recentMaterials = await _context.Materials
                .Include(x => x.PurchaseOrder)
                .ThenInclude(x => x!.Purchaser)
                .OrderByDescending(x => x.Id)
                .Take(8)
                .ToListAsync();

            ViewBag.CompletedCount = await _context.Materials
    .CountAsync(x => x.ProcessStatus == "Completed");

            ViewBag.RunningCount = await _context.Materials
                .CountAsync(x => x.ProcessStatus == "In Process");

            ViewBag.PendingCount = await _context.Materials
                .CountAsync(x => x.ProcessStatus == "Pending");

            ViewBag.DispatchCompleted = await _context.Materials
                .CountAsync(x => x.DispatchStatus == "Dispatched");

            ViewBag.DispatchPending = await _context.Materials
                .CountAsync(x => x.DispatchStatus == "Pending");

            ViewBag.DispatchPartial = await _context.Materials
                .CountAsync(x => x.DispatchStatus == "Partial");

            return View(recentMaterials);
        }
    }
}