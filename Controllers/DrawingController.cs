using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class DrawingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DrawingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var materials = await _context.Materials
                .Include(x => x.PurchaseOrder)
                .ThenInclude(x => x!.Purchaser)
                .OrderByDescending(x => x.DrawingUpdatedDate ?? x.CreatedDate)
                .ToListAsync();

            ViewBag.MissingDrawings = materials.Count(x => string.IsNullOrWhiteSpace(x.DrawingFilePath));
            ViewBag.UpdatedThisWeek = materials.Count(x =>
                x.DrawingUpdatedDate.HasValue &&
                x.DrawingUpdatedDate.Value.Date >= DateTime.Today.AddDays(-7));

            return View(materials);
        }
    }
}
