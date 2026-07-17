using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers
{
    public class PurchaserController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PurchaserController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetString("UserName") != null;
        }

        public async Task<IActionResult> Index(string search)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            var purchasers = _context.Purchasers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                purchasers = purchasers.Where(x =>
                    x.PurchaserName.Contains(search));
            }

            ViewBag.Search = search;

            return View(await purchasers
                .OrderByDescending(x => x.Id)
                .ToListAsync());
        }

        public IActionResult Create()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Purchaser purchaser)
        {
            if (ModelState.IsValid)
            {
                _context.Purchasers.Add(purchaser);

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(purchaser);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var purchaser = await _context.Purchasers.FindAsync(id);

            if (purchaser == null)
                return NotFound();

            return View(purchaser);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Purchaser purchaser)
        {
            if (ModelState.IsValid)
            {
                _context.Purchasers.Update(purchaser);

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(purchaser);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var purchaser = await _context.Purchasers.FindAsync(id);

            if (purchaser != null)
            {
                _context.Purchasers.Remove(purchaser);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}