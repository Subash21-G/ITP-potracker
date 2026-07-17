using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class ProcessQueueController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProcessQueueController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var weekEnd = today.AddDays(7);

            var materials = await _context.Materials
                .Include(x => x.PurchaseOrder)
                .ThenInclude(x => x!.Purchaser)
                .OrderBy(x => x.ProcessStartDate ?? x.DeliveryDate)
                .ToListAsync();

            ViewBag.Overdue = materials
                .Where(x => x.DeliveryDate.HasValue
                    && x.DeliveryDate.Value.Date < today
                    && x.ProcessStatus != "Completed"
                    && x.DispatchStatus != "Dispatched")
                .ToList();

            ViewBag.StartToday = materials
                .Where(x => x.ProcessStartDate.HasValue
                    && x.ProcessStartDate.Value.Date <= today
                    && x.ProcessStatus == "Pending")
                .ToList();

            ViewBag.ThisWeek = materials
                .Where(x => x.ProcessStartDate.HasValue
                    && x.ProcessStartDate.Value.Date > today
                    && x.ProcessStartDate.Value.Date <= weekEnd
                    && x.ProcessStatus != "Completed")
                .ToList();

            ViewBag.Future = materials
                .Where(x => x.ProcessStartDate.HasValue
                    && x.ProcessStartDate.Value.Date > weekEnd
                    && x.ProcessStatus != "Completed")
                .ToList();

            return View(materials);
        }
    }
}
