using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Users()
        {
            return View(await _context.AppUsers
                .OrderBy(x => x.FullName)
                .ToListAsync());
        }

        public IActionResult Settings()
        {
            return View();
        }
    }
}
