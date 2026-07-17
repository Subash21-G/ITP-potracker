using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnalyticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var purchasers = await _context.Purchasers
                .Include(x => x.PurchaseOrders)
                .ThenInclude(x => x.Materials)
                .OrderBy(x => x.PurchaserName)
                .ToListAsync();

            ViewBag.TotalMaterials = purchasers
                .SelectMany(x => x.PurchaseOrders)
                .SelectMany(x => x.Materials)
                .Count();

            ViewBag.OverdueCount = purchasers
                .SelectMany(x => x.PurchaseOrders)
                .SelectMany(x => x.Materials)
                .Count(x => x.DeliveryDate.HasValue
                    && x.DeliveryDate.Value.Date < DateTime.Today
                    && x.ProcessStatus != "Completed");

            return View(purchasers);
        }
    }
}
