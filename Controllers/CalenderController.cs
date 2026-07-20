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

        [HttpGet]
        public async Task<JsonResult> GetEvents(
            DateTime? start,
            DateTime? end,
            CancellationToken cancellationToken)
        {
            var today = DateTime.Today;

            // FullCalendar sends an inclusive start and exclusive end.
            // Fallback dates also protect direct calls to this endpoint.
            var rangeStart =
                (start ?? new DateTime(today.Year, today.Month, 1))
                .Date;

            var rangeEnd =
                (end ?? rangeStart.AddMonths(1))
                .Date;

            if (rangeEnd <= rangeStart)
            {
                rangeEnd = rangeStart.AddMonths(1);
            }

            // Overdue events are displayed on today's date, matching the
            // existing workflow. They are needed only when today is visible.
            var includeOverdue =
                rangeStart <= today &&
                today < rangeEnd;

            var materials = await _context.Materials
                .AsNoTracking()
                .Where(material =>
                    (material.ProcessStartDate != null &&
                     material.ProcessStartDate >= rangeStart &&
                     material.ProcessStartDate < rangeEnd) ||

                    (material.DispatchDate != null &&
                     material.DispatchDate >= rangeStart &&
                     material.DispatchDate < rangeEnd) ||

                    (material.DeliveryDate != null &&
                     material.DeliveryDate >= rangeStart &&
                     material.DeliveryDate < rangeEnd) ||

                    (includeOverdue &&
                     material.DeliveryDate != null &&
                     material.DeliveryDate < today &&
                     material.ProcessStatus != "Completed" &&
                     material.DispatchStatus != "Dispatched"))
                .Select(material => new
                {
                    material.Id,
                    material.MaterialNo,
                    material.ProcessStartDate,
                    material.DispatchDate,
                    material.DeliveryDate,
                    material.ProcessStatus,
                    material.DispatchStatus
                })
                .ToListAsync(cancellationToken);

            var events = new List<object>(materials.Count * 2);

            foreach (var material in materials)
            {
                var materialNumber =
                    string.IsNullOrWhiteSpace(material.MaterialNo)
                        ? $"Material #{material.Id}"
                        : material.MaterialNo;

                if (material.ProcessStartDate.HasValue &&
                    material.ProcessStartDate.Value >= rangeStart &&
                    material.ProcessStartDate.Value < rangeEnd)
                {
                    events.Add(new
                    {
                        id = material.Id,
                        title = "Start: " + materialNumber,
                        start = material.ProcessStartDate.Value
                            .ToString("yyyy-MM-dd"),
                        color = "#2563eb",
                        allDay = true
                    });
                }

                if (material.DispatchDate.HasValue &&
                    material.DispatchDate.Value >= rangeStart &&
                    material.DispatchDate.Value < rangeEnd)
                {
                    events.Add(new
                    {
                        id = material.Id,
                        title = "Dispatch: " + materialNumber,
                        start = material.DispatchDate.Value
                            .ToString("yyyy-MM-dd"),
                        color = "#f59e0b",
                        allDay = true
                    });
                }

                if (material.DeliveryDate.HasValue &&
                    material.DeliveryDate.Value >= rangeStart &&
                    material.DeliveryDate.Value < rangeEnd)
                {
                    events.Add(new
                    {
                        id = material.Id,
                        title = "Delivery: " + materialNumber,
                        start = material.DeliveryDate.Value
                            .ToString("yyyy-MM-dd"),
                        color = material.ProcessStatus == "Completed"
                            ? "#16a34a"
                            : "#22c55e",
                        allDay = true
                    });
                }

                if (includeOverdue &&
                    material.DeliveryDate.HasValue &&
                    material.DeliveryDate.Value.Date < today &&
                    material.ProcessStatus != "Completed" &&
                    material.DispatchStatus != "Dispatched")
                {
                    events.Add(new
                    {
                        id = material.Id,
                        title = "Overdue: " + materialNumber,
                        start = today.ToString("yyyy-MM-dd"),
                        color = "#dc2626",
                        allDay = true
                    });
                }
            }

            return Json(events);
        }
    }
}