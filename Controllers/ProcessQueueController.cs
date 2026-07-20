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

        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 50)
        {
            page = Math.Max(page, 1);

            var allowedPageSizes =
                new[] { 10, 25, 50, 100, 200 };

            if (!allowedPageSizes.Contains(pageSize))
            {
                pageSize = 50;
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var weekEndExclusive = today.AddDays(8);

            var baseQuery = _context.Materials
                .AsNoTracking();

            var summary = await baseQuery
                .GroupBy(x => 1)
                .Select(group => new
                {
                    TotalMaterials = group.Count(),

                    Overdue = group.Count(x =>
                        x.DeliveryDate != null &&
                        x.DeliveryDate < today &&
                        x.ProcessStatus != "Completed" &&
                        x.DispatchStatus != "Dispatched"),

                    StartToday = group.Count(x =>
                        x.ProcessStartDate != null &&
                        x.ProcessStartDate < tomorrow &&
                        x.ProcessStatus == "Pending"),

                    ThisWeek = group.Count(x =>
                        x.ProcessStartDate != null &&
                        x.ProcessStartDate >= tomorrow &&
                        x.ProcessStartDate < weekEndExclusive &&
                        x.ProcessStatus != "Completed"),

                    Future = group.Count(x =>
                        x.ProcessStartDate != null &&
                        x.ProcessStartDate >= weekEndExclusive &&
                        x.ProcessStatus != "Completed")
                })
                .FirstOrDefaultAsync();

            var totalMaterials =
                summary?.TotalMaterials ?? 0;

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalMaterials / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var overdue = await baseQuery
                .Include(x => x.PurchaseOrder)
                .Where(x =>
                    x.DeliveryDate != null &&
                    x.DeliveryDate < today &&
                    x.ProcessStatus != "Completed" &&
                    x.DispatchStatus != "Dispatched")
                .OrderBy(x => x.DeliveryDate)
                .ThenBy(x => x.Id)
                .Take(6)
                .ToListAsync();

            var startToday = await baseQuery
                .Include(x => x.PurchaseOrder)
                .Where(x =>
                    x.ProcessStartDate != null &&
                    x.ProcessStartDate < tomorrow &&
                    x.ProcessStatus == "Pending")
                .OrderBy(x => x.ProcessStartDate)
                .ThenBy(x => x.Id)
                .Take(6)
                .ToListAsync();

            var thisWeek = await baseQuery
                .Include(x => x.PurchaseOrder)
                .Where(x =>
                    x.ProcessStartDate != null &&
                    x.ProcessStartDate >= tomorrow &&
                    x.ProcessStartDate < weekEndExclusive &&
                    x.ProcessStatus != "Completed")
                .OrderBy(x => x.ProcessStartDate)
                .ThenBy(x => x.Id)
                .Take(6)
                .ToListAsync();

            var future = await baseQuery
                .Include(x => x.PurchaseOrder)
                .Where(x =>
                    x.ProcessStartDate != null &&
                    x.ProcessStartDate >= weekEndExclusive &&
                    x.ProcessStatus != "Completed")
                .OrderBy(x => x.ProcessStartDate)
                .ThenBy(x => x.Id)
                .Take(6)
                .ToListAsync();

            var materials = await baseQuery
                .Include(x => x.PurchaseOrder)
                .ThenInclude(x => x!.Purchaser)
                .OrderBy(x => x.ProcessStartDate ?? x.DeliveryDate)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Overdue = overdue;
            ViewBag.StartToday = startToday;
            ViewBag.ThisWeek = thisWeek;
            ViewBag.Future = future;
            ViewBag.OverdueCount = summary?.Overdue ?? 0;
            ViewBag.StartTodayCount = summary?.StartToday ?? 0;
            ViewBag.ThisWeekCount = summary?.ThisWeek ?? 0;
            ViewBag.FutureCount = summary?.Future ?? 0;
            ViewBag.TotalMaterials = totalMaterials;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;

            return View(materials);
        }
    }
}