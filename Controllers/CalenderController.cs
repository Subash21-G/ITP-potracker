using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class CalendarController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CalendarController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<JsonResult> GetEvents()
        {
            var materials = await _context.Materials.ToListAsync();
            var today = DateTime.Today;

            var events = materials.SelectMany(m =>
            {
                var itemEvents = new List<object>();

                if (m.ProcessStartDate.HasValue)
                {
                    itemEvents.Add(new
                    {
                        id = m.Id,
                        title = "Start: " + m.MaterialNo,
                        start = m.ProcessStartDate.Value.ToString("yyyy-MM-dd"),
                        color = "#2563eb"
                    });
                }

                if (m.DispatchDate.HasValue)
                {
                    itemEvents.Add(new
                    {
                        id = m.Id,
                        title = "Dispatch: " + m.MaterialNo,
                        start = m.DispatchDate.Value.ToString("yyyy-MM-dd"),
                        color = "#f59e0b"
                    });
                }

                if (m.DeliveryDate.HasValue)
                {
                    itemEvents.Add(new
                    {
                        id = m.Id,
                        title = "Delivery: " + m.MaterialNo,
                        start = m.DeliveryDate.Value.ToString("yyyy-MM-dd"),
                        color = m.ProcessStatus == "Completed" ? "#16a34a" : "#22c55e"
                    });
                }

                if (m.DeliveryDate.HasValue &&
                    m.DeliveryDate.Value.Date < today &&
                    m.ProcessStatus != "Completed" &&
                    m.DispatchStatus != "Dispatched")
                {
                    itemEvents.Add(new
                    {
                        id = m.Id,
                        title = "Overdue: " + m.MaterialNo,
                        start = today.ToString("yyyy-MM-dd"),
                        color = "#dc2626"
                    });
                }

                return itemEvents;
            });

            return Json(events);
        }
    }
}
