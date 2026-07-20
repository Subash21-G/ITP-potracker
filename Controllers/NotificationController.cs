using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var alerts = await _context.Alerts
                .Include(x => x.Material)
                .OrderByDescending(x => x.AlertDate)
                .Take(100)
                .ToListAsync();

            ViewBag.UnreadCount = alerts.Count(x => !x.IsRead);

            return View(alerts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var alert = await _context.Alerts.FindAsync(id);

            if (alert != null)
            {
                alert.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
