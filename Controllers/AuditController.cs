using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class AuditController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var materials = await _context.Materials
                .Include(x => x.PurchaseOrder)
                .OrderByDescending(x => x.Id)
                .Take(25)
                .ToListAsync();

            return View(materials);
        }
    }
}
