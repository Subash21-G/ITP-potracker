using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class AlertController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AlertController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            DateTime today = DateTime.Today;
            DateTime nextWeek = today.AddDays(7);

            ViewBag.ProcessStartAlerts = await _context.Materials
                .Where(x => x.AlertDate != null
                    && x.AlertDate <= today
                    && x.ProcessStatus != "Completed")
                .OrderBy(x => x.AlertDate)
                .ToListAsync();

            ViewBag.OverdueMaterials = await _context.Materials
                .Where(x => x.DeliveryDate != null
                    && x.DeliveryDate < today
                    && x.ProcessStatus != "Completed")
                .OrderBy(x => x.DeliveryDate)
                .ToListAsync();

            ViewBag.DeliveryWithin7Days = await _context.Materials
                .Where(x => x.DeliveryDate != null
                    && x.DeliveryDate >= today
                    && x.DeliveryDate <= nextWeek
                    && x.ProcessStatus != "Completed")
                .OrderBy(x => x.DeliveryDate)
                .ToListAsync();

            ViewBag.PendingProcess = await _context.Materials
                .Where(x => x.ProcessStatus == "Pending"
                    || x.ProcessStatus == "In Process"
                    || x.ProcessStatus == "Hold")
                .OrderBy(x => x.DeliveryDate)
                .ToListAsync();

            ViewBag.DispatchPending = await _context.Materials
                .Where(x => x.DispatchStatus == "Pending"
                    || x.DispatchStatus == "Partial")
                .OrderBy(x => x.DeliveryDate)
                .ToListAsync();

            ViewBag.DispatchDateAlerts = await _context.Materials
                .Where(x => x.DispatchDate != null
                    && x.DispatchDate <= nextWeek
                    && x.DispatchStatus != "Dispatched")
                .OrderBy(x => x.DispatchDate)
                .ToListAsync();

            ViewBag.LowStock = await _context.Materials
                .Where(x => x.StockQty <= 5)
                .OrderBy(x => x.StockQty)
                .ToListAsync();

            return View();
        }
    }
}