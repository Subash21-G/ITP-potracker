using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POTracker.Controllers
{
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("Role") == "Admin";
        }

        public async Task<IActionResult> Index()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.Purchasers = await _context.Purchasers
                .OrderBy(x => x.PurchaserName)
                .ToListAsync();

            return View();
        }

        public async Task<IActionResult> Preview(
            DateTime? fromDate,
            DateTime? toDate,
            string? processStatus,
            string? dispatchStatus,
            int? purchaserId)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.ProcessStatus = processStatus;
            ViewBag.DispatchStatus = dispatchStatus;
            ViewBag.PurchaserId = purchaserId;

            var materials = await GetFilteredMaterials(
                fromDate,
                toDate,
                processStatus,
                dispatchStatus,
                purchaserId).ToListAsync();

            return View(materials);
        }

        private IQueryable<Material> GetFilteredMaterials(
            DateTime? fromDate,
            DateTime? toDate,
            string? processStatus,
            string? dispatchStatus,
            int? purchaserId)
        {
            var query = _context.Materials
                .Include(x => x.PurchaseOrder)
                .ThenInclude(x => x!.Purchaser)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(x =>
                    x.DeliveryDate != null &&
                    x.DeliveryDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(x =>
                    x.DeliveryDate != null &&
                    x.DeliveryDate <= toDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(processStatus))
            {
                query = query.Where(x =>
                    x.ProcessStatus == processStatus);
            }

            if (!string.IsNullOrWhiteSpace(dispatchStatus))
            {
                query = query.Where(x =>
                    x.DispatchStatus == dispatchStatus);
            }

            if (purchaserId.HasValue && purchaserId.Value > 0)
            {
                query = query.Where(x =>
                    x.PurchaseOrder != null &&
                    x.PurchaseOrder.PurchaserId == purchaserId.Value);
            }

            return query.OrderByDescending(x => x.Id);
        }

        public async Task<IActionResult> MaterialsExcel(
            DateTime? fromDate,
            DateTime? toDate,
            string? processStatus,
            string? dispatchStatus,
            int? purchaserId)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index", "Dashboard");
            }

            var materials = await GetFilteredMaterials(
                fromDate,
                toDate,
                processStatus,
                dispatchStatus,
                purchaserId).ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Materials Report");

            string[] headers =
            {
                "PO Number", "Purchaser", "Material No", "Description",
                "Qty", "Dispatch Qty", "Pending Qty", "Stock Qty",
                "Delivery Date", "Process Status", "Dispatch Status",
                "RM Grade", "RM Source", "Surface Finish", "Cost", "Remark"
            };

            for (int col = 0; col < headers.Length; col++)
            {
                worksheet.Cell(1, col + 1).Value = headers[col];
                worksheet.Cell(1, col + 1).Style.Font.Bold = true;
            }

            int row = 2;

            foreach (var item in materials)
            {
                worksheet.Cell(row, 1).Value = item.PurchaseOrder?.PONumber ?? "-";
                worksheet.Cell(row, 2).Value = item.PurchaseOrder?.Purchaser?.PurchaserName ?? "-";
                worksheet.Cell(row, 3).Value = item.MaterialNo;
                worksheet.Cell(row, 4).Value = item.ShortDescription;
                worksheet.Cell(row, 5).Value = item.Qty;
                worksheet.Cell(row, 6).Value = item.DispatchQty;
                worksheet.Cell(row, 7).Value = item.PendingQty;
                worksheet.Cell(row, 8).Value = item.StockQty;
                worksheet.Cell(row, 9).Value = item.DeliveryDate?.ToString("dd-MM-yyyy") ?? "-";
                worksheet.Cell(row, 10).Value = item.ProcessStatus;
                worksheet.Cell(row, 11).Value = item.DispatchStatus;
                worksheet.Cell(row, 12).Value = item.RMGrade;
                worksheet.Cell(row, 13).Value = item.RMSource;
                worksheet.Cell(row, 14).Value = item.SurfaceFinish;
                worksheet.Cell(row, 15).Value = item.Cost;
                worksheet.Cell(row, 16).Value = item.Remark;

                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Materials_Report.xlsx");
        }

        public async Task<IActionResult> MaterialsPdf(
            DateTime? fromDate,
            DateTime? toDate,
            string? processStatus,
            string? dispatchStatus,
            int? purchaserId)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index", "Dashboard");
            }

            QuestPDF.Settings.License = LicenseType.Community;

            var materials = await GetFilteredMaterials(
                fromDate,
                toDate,
                processStatus,
                dispatchStatus,
                purchaserId).ToListAsync();

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(25);
                    page.Size(PageSizes.A4.Landscape());

                    page.Header()
                        .Text("Materials Report")
                        .FontSize(20)
                        .Bold();

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("PO No").Bold();
                            header.Cell().Text("Purchaser").Bold();
                            header.Cell().Text("Material").Bold();
                            header.Cell().Text("Description").Bold();
                            header.Cell().Text("Qty").Bold();
                            header.Cell().Text("Delivery").Bold();
                            header.Cell().Text("Process").Bold();
                            header.Cell().Text("Dispatch").Bold();
                        });

                        foreach (var item in materials)
                        {
                            table.Cell().Text(item.PurchaseOrder?.PONumber ?? "-");
                            table.Cell().Text(item.PurchaseOrder?.Purchaser?.PurchaserName ?? "-");
                            table.Cell().Text(item.MaterialNo ?? "-");
                            table.Cell().Text(item.ShortDescription ?? "-");
                            table.Cell().Text(item.Qty.ToString());
                            table.Cell().Text(item.DeliveryDate?.ToString("dd-MM-yyyy") ?? "-");
                            table.Cell().Text(item.ProcessStatus ?? "-");
                            table.Cell().Text(item.DispatchStatus ?? "-");
                        }
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Generated on ");
                            x.Span(DateTime.Now.ToString("dd-MM-yyyy hh:mm tt"));
                        });
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", "Materials_Report.pdf");
        }
    }
}