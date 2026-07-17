using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers
{
    public class StockController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StockController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var stocks = _context.Stocks.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                stocks = stocks.Where(x =>
                    x.MaterialNo.Contains(search) ||
                    (x.MaterialName != null && x.MaterialName.Contains(search)) ||
                    (x.RMGrade != null && x.RMGrade.Contains(search)));
            }

            ViewBag.Search = search;
            ViewBag.LowStockCount = await stocks
                .CountAsync(x => x.AvailableQty <= x.MinimumQty);

            return View(await stocks
                .OrderBy(x => x.AvailableQty <= x.MinimumQty ? 0 : 1)
                .ThenBy(x => x.MaterialNo)
                .ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Stock stock)
        {
            if (ModelState.IsValid)
            {
                stock.UpdatedDate = DateTime.Now;
                _context.Stocks.Add(stock);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(stock);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);

            if (stock == null)
            {
                return NotFound();
            }

            return View(stock);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Stock stock)
        {
            if (ModelState.IsValid)
            {
                stock.UpdatedDate = DateTime.Now;
                _context.Stocks.Update(stock);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(stock);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);

            if (stock != null)
            {
                _context.Stocks.Remove(stock);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
