using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;
using POTracker.ViewModels;

namespace POTracker.Controllers
{
    public partial class PendingMaterialController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PendingMaterialController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetString("UserName") != null;
        }

        private static async Task<PendingMaterialSummaryViewModel>
            BuildSummaryAsync(
                IQueryable<Material> query)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var weekEndExclusive = today.AddDays(8);

            var totals = await query
                .GroupBy(x => 1)
                .Select(group => new
                {
                    Total = group.Count(),

                    Overdue = group.Count(x =>
                        x.DeliveryDate.HasValue &&
                        x.DeliveryDate < today &&
                        x.PendingQty > 0),

                    DueToday = group.Count(x =>
                        x.DeliveryDate.HasValue &&
                        x.DeliveryDate >= today &&
                        x.DeliveryDate < tomorrow &&
                        x.PendingQty > 0),

                    DueThisWeek = group.Count(x =>
                        x.DeliveryDate.HasValue &&
                        x.DeliveryDate >= today &&
                        x.DeliveryDate < weekEndExclusive &&
                        x.PendingQty > 0),

                    WithoutDeliveryDate = group.Count(x =>
                        !x.DeliveryDate.HasValue),

                    TotalPOQuantity = group.Sum(x => x.Qty),
                    TotalInvoiceQuantity = group.Sum(x => x.InvoiceQty),
                    TotalInvoiceBalanceQuantity = group.Sum(x =>
                        x.BalanceQty < 0 ? 0 : x.BalanceQty),
                    TotalDispatchQuantity = group.Sum(x => x.DispatchQty),
                    TotalPendingDispatchQuantity = group.Sum(x =>
                        x.PendingQty < 0 ? 0 : x.PendingQty),
                    TotalPendingValue = group.Sum(x =>
                        (x.PendingQty < 0 ? 0 : x.PendingQty) * x.Cost)
                })
                .FirstOrDefaultAsync();

            var assignmentSummary = await query
                .GroupBy(x =>
                    string.IsNullOrWhiteSpace(x.AssignedDashboard)
                        ? "Unassigned"
                        : x.AssignedDashboard!.Trim())
                .Select(group =>
                    new PendingMaterialAssignmentSummaryViewModel
                    {
                        AssignedDashboard = group.Key,
                        MaterialCount = group.Count(),
                        PendingQuantity = group.Sum(x =>
                            x.PendingQty < 0 ? 0 : x.PendingQty),
                        OverdueCount = group.Count(x =>
                            x.DeliveryDate.HasValue &&
                            x.DeliveryDate < today &&
                            x.PendingQty > 0)
                    })
                .OrderByDescending(x => x.MaterialCount)
                .ThenBy(x => x.AssignedDashboard)
                .ToListAsync();

            return new PendingMaterialSummaryViewModel
            {
                TotalPendingMaterials = totals?.Total ?? 0,
                OverdueMaterials = totals?.Overdue ?? 0,
                DueTodayMaterials = totals?.DueToday ?? 0,
                DueThisWeekMaterials = totals?.DueThisWeek ?? 0,
                WithoutDeliveryDate = totals?.WithoutDeliveryDate ?? 0,
                TotalPOQuantity = totals?.TotalPOQuantity ?? 0,
                TotalInvoiceQuantity = totals?.TotalInvoiceQuantity ?? 0,
                TotalInvoiceBalanceQuantity =
                    totals?.TotalInvoiceBalanceQuantity ?? 0,
                TotalDispatchQuantity = totals?.TotalDispatchQuantity ?? 0,
                TotalPendingDispatchQuantity =
                    totals?.TotalPendingDispatchQuantity ?? 0,
                TotalPendingValue = totals?.TotalPendingValue ?? 0,
                AssignmentSummary = assignmentSummary
            };
        }

        private IQueryable<Material> CreatePendingQuery()
        {
            return _context.Materials
                .AsNoTracking()
                .Include(x => x.PurchaseOrder)
                .Where(x =>
                    x.PendingQty > 0 ||
                    x.BalanceQty > 0 ||
                    x.DispatchStatus != "Dispatched" ||
                    x.InvoiceStatus != "Fully Invoiced" ||
                    x.ProcessStatus != "Completed");
        }

        private static IQueryable<Material> ApplyFilters(
            IQueryable<Material> query,
            string? search,
            string? poNumber,
            string? handler,
            string? assignedDashboard,
            string? processStatus,
            string? invoiceStatus,
            string? dispatchStatus,
            DateTime? poDateFrom,
            DateTime? poDateTo,
            DateTime? poReceivedDateFrom,
            DateTime? poReceivedDateTo,
            DateTime? deliveryDateFrom,
            DateTime? deliveryDateTo,
            bool overdueOnly,
            bool dueThisWeekOnly)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

 query = query.Where(x =>
    (x.MaterialNo != null &&
     x.MaterialNo.Contains(search)) ||
    (x.LineItem != null &&
     x.LineItem.Contains(search)) ||
    (x.HsnSacCode != null &&
     x.HsnSacCode.Contains(search)) ||
    (x.ShortDescription != null &&
     x.ShortDescription.Contains(search)) ||
    (x.BriefDescription != null &&
     x.BriefDescription.Contains(search)) ||
    (x.DrawingNo != null &&
     x.DrawingNo.Contains(search)) ||
    (x.PurchaseOrder != null &&
     x.PurchaseOrder.PONumber.Contains(search)) ||
    (x.PurchaseOrder != null &&
     x.PurchaseOrder.BuyerContactPerson != null &&
     x.PurchaseOrder.BuyerContactPerson.Contains(search)));         }

            if (!string.IsNullOrWhiteSpace(poNumber))
            {
                poNumber = poNumber.Trim();

                query = query.Where(x =>
                    x.PurchaseOrder != null &&
                    x.PurchaseOrder.PONumber.Contains(poNumber));
            }

            if (!string.IsNullOrWhiteSpace(handler))
            {
                handler = handler.Trim();

                query = query.Where(x =>
                    x.PurchaseOrder != null &&
                    x.PurchaseOrder.BuyerContactPerson != null &&
                    x.PurchaseOrder.BuyerContactPerson.Contains(handler));
            }

            if (!string.IsNullOrWhiteSpace(assignedDashboard))
            {
                query = query.Where(x =>
                    x.AssignedDashboard == assignedDashboard);
            }

            if (!string.IsNullOrWhiteSpace(processStatus))
            {
                query = query.Where(x =>
                    x.ProcessStatus == processStatus);
            }

            if (!string.IsNullOrWhiteSpace(invoiceStatus))
            {
                query = query.Where(x =>
                    x.InvoiceStatus == invoiceStatus);
            }

            if (!string.IsNullOrWhiteSpace(dispatchStatus))
            {
                query = query.Where(x =>
                    x.DispatchStatus == dispatchStatus);
            }

            if (poDateFrom.HasValue)
            {
                var date = poDateFrom.Value.Date;

                query = query.Where(x =>
                    (x.PurchaseOrder != null &&
                     x.PurchaseOrder.PODate.HasValue &&
                     x.PurchaseOrder.PODate.Value.Date >= date) ||
                    ((x.PurchaseOrder == null ||
                      !x.PurchaseOrder.PODate.HasValue) &&
                     x.PODate.HasValue &&
                     x.PODate.Value.Date >= date));
            }

            if (poDateTo.HasValue)
            {
                var date = poDateTo.Value.Date;

                query = query.Where(x =>
                    (x.PurchaseOrder != null &&
                     x.PurchaseOrder.PODate.HasValue &&
                     x.PurchaseOrder.PODate.Value.Date <= date) ||
                    ((x.PurchaseOrder == null ||
                      !x.PurchaseOrder.PODate.HasValue) &&
                     x.PODate.HasValue &&
                     x.PODate.Value.Date <= date));
            }

            if (poReceivedDateFrom.HasValue)
            {
                var date = poReceivedDateFrom.Value.Date;

                query = query.Where(x =>
                    (x.PurchaseOrder != null &&
                     x.PurchaseOrder.ReceivedDate.HasValue &&
                     x.PurchaseOrder.ReceivedDate.Value.Date >= date) ||
                    ((x.PurchaseOrder == null ||
                      !x.PurchaseOrder.ReceivedDate.HasValue) &&
                     x.ReceivingDate.HasValue &&
                     x.ReceivingDate.Value.Date >= date));
            }

            if (poReceivedDateTo.HasValue)
            {
                var date = poReceivedDateTo.Value.Date;

                query = query.Where(x =>
                    (x.PurchaseOrder != null &&
                     x.PurchaseOrder.ReceivedDate.HasValue &&
                     x.PurchaseOrder.ReceivedDate.Value.Date <= date) ||
                    ((x.PurchaseOrder == null ||
                      !x.PurchaseOrder.ReceivedDate.HasValue) &&
                     x.ReceivingDate.HasValue &&
                     x.ReceivingDate.Value.Date <= date));
            }

            if (deliveryDateFrom.HasValue)
            {
                var date = deliveryDateFrom.Value.Date;

                query = query.Where(x =>
                    x.DeliveryDate.HasValue &&
                    x.DeliveryDate.Value.Date >= date);
            }

            if (deliveryDateTo.HasValue)
            {
                var date = deliveryDateTo.Value.Date;

                query = query.Where(x =>
                    x.DeliveryDate.HasValue &&
                    x.DeliveryDate.Value.Date <= date);
            }

            if (overdueOnly)
            {
                var today = DateTime.Today;

                query = query.Where(x =>
                    x.DeliveryDate.HasValue &&
                    x.DeliveryDate.Value.Date < today &&
                    x.PendingQty > 0);
            }

            if (dueThisWeekOnly)
            {
                var today = DateTime.Today;
                var weekEnd = today.AddDays(7);

                query = query.Where(x =>
                    x.DeliveryDate.HasValue &&
                    x.DeliveryDate.Value.Date >= today &&
                    x.DeliveryDate.Value.Date <= weekEnd &&
                    x.PendingQty > 0);
            }

            return query;
        }

        private static PendingMaterialRowViewModel MapRow(
            Material material)
        {
            return new PendingMaterialRowViewModel
            {
                MaterialId = material.Id,
                PurchaseOrderId = material.PurchaseOrderId,

                PONumber =
                    material.PurchaseOrder?.PONumber ?? "",

                Handler =
                    material.PurchaseOrder?.BuyerContactPerson ?? "",

                PODate =
                    material.PurchaseOrder?.PODate ??
                    material.PODate,

                POReceivedDate =
                    material.PurchaseOrder?.ReceivedDate ??
                    material.ReceivingDate,

                LineItem = material.LineItem ?? "",
                MaterialNo = material.MaterialNo ?? "",
                HsnSacCode = material.HsnSacCode,
                ShortDescription = material.ShortDescription,
                BriefDescription = material.BriefDescription,
                Unit = material.Unit,
                Department = material.Dep,
                DrawingNo = material.DrawingNo,
                DrawingVersion = material.DrawingVersion,

                POQuantity = material.Qty,
                InvoiceQuantity = material.InvoiceQty,
                InvoiceBalanceQuantity = Math.Max(0, material.BalanceQty),
                DispatchQuantity = material.DispatchQty,
                PendingDispatchQuantity = Math.Max(0, material.PendingQty),
                StockQuantity = material.StockQty,

                UnitCost = material.Cost,
                TotalValue = material.TotalValue,

                DeliveryDate = material.DeliveryDate,
                ProcessStartDate = material.ProcessStartDate,
                AlertDate = material.AlertDate,

                ProcessStatus =
                    string.IsNullOrWhiteSpace(material.ProcessStatus)
                        ? "Pending"
                        : material.ProcessStatus,

                InvoiceStatus =
                    string.IsNullOrWhiteSpace(material.InvoiceStatus)
                        ? "Not Invoiced"
                        : material.InvoiceStatus,

                DispatchStatus =
                    string.IsNullOrWhiteSpace(material.DispatchStatus)
                        ? "Pending"
                        : material.DispatchStatus,

                AssignedDashboard = material.AssignedDashboard,
                Remarks = material.Remark,
                PdfFilePath = material.PDFFilePath,
                DrawingFilePath = material.DrawingFilePath
            };
        }

        private static PendingMaterialSummaryViewModel BuildSummary(
            IReadOnlyCollection<PendingMaterialRowViewModel> rows)
        {
            var summary = new PendingMaterialSummaryViewModel
            {
                TotalPendingMaterials = rows.Count,
                OverdueMaterials = rows.Count(x => x.IsOverdue),
                DueTodayMaterials = rows.Count(x => x.IsDueToday),
                DueThisWeekMaterials = rows.Count(x => x.IsDueThisWeek),
                WithoutDeliveryDate = rows.Count(x => !x.DeliveryDate.HasValue),

                TotalPOQuantity = rows.Sum(x => x.POQuantity),
                TotalInvoiceQuantity = rows.Sum(x => x.InvoiceQuantity),
                TotalInvoiceBalanceQuantity =
                    rows.Sum(x => x.InvoiceBalanceQuantity),
                TotalDispatchQuantity =
                    rows.Sum(x => x.DispatchQuantity),
                TotalPendingDispatchQuantity =
                    rows.Sum(x => x.PendingDispatchQuantity),
                TotalPendingValue =
                    rows.Sum(x =>
                        x.PendingDispatchQuantity * x.UnitCost)
            };

            summary.AssignmentSummary = rows
                .GroupBy(x =>
                    string.IsNullOrWhiteSpace(x.AssignedDashboard)
                        ? "Unassigned"
                        : x.AssignedDashboard.Trim())
                .Select(group =>
                    new PendingMaterialAssignmentSummaryViewModel
                    {
                        AssignedDashboard = group.Key,
                        MaterialCount = group.Count(),
                        PendingQuantity =
                            group.Sum(x => x.PendingDispatchQuantity),
                        OverdueCount =
                            group.Count(x => x.IsOverdue)
                    })
                .OrderByDescending(x => x.MaterialCount)
                .ThenBy(x => x.AssignedDashboard)
                .ToList();

            return summary;
        }

        private static async Task<DropdownData>
            LoadDropdownDataAsync(
                IQueryable<Material> baseQuery)
        {
            var handlers = await baseQuery
                .Where(x =>
                    x.PurchaseOrder != null &&
                    x.PurchaseOrder.BuyerContactPerson != null &&
                    x.PurchaseOrder.BuyerContactPerson != "")
                .GroupBy(x =>
                    x.PurchaseOrder!.BuyerContactPerson!)
                .Select(group => new
                {
                    Value = group.Key,
                    Count = group.Count()
                })
                .OrderBy(x => x.Value)
                .ToListAsync();

            var assigned = await baseQuery
                .Where(x =>
                    x.AssignedDashboard != null &&
                    x.AssignedDashboard != "")
                .GroupBy(x => x.AssignedDashboard!)
                .Select(group => new
                {
                    Value = group.Key,
                    Count = group.Count()
                })
                .OrderBy(x => x.Value)
                .ToListAsync();

            var process = await baseQuery
                .Where(x =>
                    x.ProcessStatus != null &&
                    x.ProcessStatus != "")
                .GroupBy(x => x.ProcessStatus)
                .Select(group => new
                {
                    Value = group.Key,
                    Count = group.Count()
                })
                .OrderBy(x => x.Value)
                .ToListAsync();

            var invoice = await baseQuery
                .Where(x =>
                    x.InvoiceStatus != null &&
                    x.InvoiceStatus != "")
                .GroupBy(x => x.InvoiceStatus)
                .Select(group => new
                {
                    Value = group.Key,
                    Count = group.Count()
                })
                .OrderBy(x => x.Value)
                .ToListAsync();

            var dispatch = await baseQuery
                .Where(x =>
                    x.DispatchStatus != null &&
                    x.DispatchStatus != "")
                .GroupBy(x => x.DispatchStatus)
                .Select(group => new
                {
                    Value = group.Key,
                    Count = group.Count()
                })
                .OrderBy(x => x.Value)
                .ToListAsync();
            return new DropdownData
            {
                Handlers = handlers
                    .Select(x =>
                        new PendingMaterialFilterOptionViewModel
                        {
                            Value = x.Value,
                            Text = x.Value,
                            Count = x.Count
                        })
                    .ToList(),

                AssignedDashboards = assigned
                    .Select(x =>
                        new PendingMaterialFilterOptionViewModel
                        {
                            Value = x.Value,
                            Text = x.Value,
                            Count = x.Count
                        })
                    .ToList(),

                ProcessStatuses = process
                    .Select(x =>
                        new PendingMaterialFilterOptionViewModel
                        {
                            Value = x.Value,
                            Text = x.Value,
                            Count = x.Count
                        })
                    .ToList(),

                InvoiceStatuses = invoice
                    .Select(x =>
                        new PendingMaterialFilterOptionViewModel
                        {
                            Value = x.Value,
                            Text = x.Value,
                            Count = x.Count
                        })
                    .ToList(),

                DispatchStatuses = dispatch
                    .Select(x =>
                        new PendingMaterialFilterOptionViewModel
                        {
                            Value = x.Value,
                            Text = x.Value,
                            Count = x.Count
                        })
                    .ToList()
            };
        }

        private static void MarkSelectedOptions(
            PendingMaterialIndexViewModel model)
        {
            MarkSelected(model.Handlers, model.Handler);
            MarkSelected(
                model.AssignedDashboards,
                model.AssignedDashboard);
            MarkSelected(
                model.ProcessStatuses,
                model.ProcessStatus);
            MarkSelected(
                model.InvoiceStatuses,
                model.InvoiceStatus);
            MarkSelected(
                model.DispatchStatuses,
                model.DispatchStatus);
        }

        private static void MarkSelected(
            IEnumerable<PendingMaterialFilterOptionViewModel> options,
            string? selectedValue)
        {
            foreach (var option in options)
            {
                option.IsSelected =
                    !string.IsNullOrWhiteSpace(selectedValue) &&
                    string.Equals(
                        option.Value,
                        selectedValue,
                        StringComparison.OrdinalIgnoreCase);
            }
        }

        private static int NormalizePageSize(int pageSize)
        {
            var sizes = new[] { 10, 25, 50, 100, 200 };

            return sizes.Contains(pageSize)
                ? pageSize
                : 25;
        }

        private static void WriteExcel(
            IXLWorksheet worksheet,
            IReadOnlyList<PendingMaterialRowViewModel> rows)
        {
            worksheet.Cell("A1").Value =
                "PO TRACKER - PENDING MATERIALS";

            worksheet.Range("A1:W1").Merge();

            worksheet.Cell("A2").Value =
                $"Generated: {DateTime.Now:dd-MM-yyyy hh:mm tt}";

            worksheet.Range("A2:W2").Merge();

            worksheet.Cell("A3").Value =
                $"Total pending materials: {rows.Count}";

            worksheet.Range("A3:W3").Merge();

            var headers = new[]
            {
                "S.No",
                "PO Number",
                "Handler / Person",
                "PO Date",
                "PO Received Date",
                "Line Item",
                "Material Number",
                "Description",
                "PO Qty",
                "Invoice Qty",
                "Invoice Balance",
                "Dispatch Qty",
                "Pending Dispatch Qty",
                "Unit",
                "Process Status",
                "Invoice Status",
                "Dispatch Status",
                "Delivery Date",
                "Due Status",
                "Overdue Days",
                "Assigned Person",
                "Price Per Qty",
                "Drawing Number"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(5, i + 1).Value = headers[i];
            }

            var excelRow = 6;

            for (var index = 0; index < rows.Count; index++)
            {
                var item = rows[index];

                worksheet.Cell(excelRow, 1).Value = index + 1;
                worksheet.Cell(excelRow, 2).Value = item.PONumber;
                worksheet.Cell(excelRow, 3).Value =
                    string.IsNullOrWhiteSpace(item.Handler)
                        ? "Unassigned"
                        : item.Handler;

                if (item.PODate.HasValue)
                {
                    worksheet.Cell(excelRow, 4).Value =
                        item.PODate.Value;
                }

                if (item.POReceivedDate.HasValue)
                {
                    worksheet.Cell(excelRow, 5).Value =
                        item.POReceivedDate.Value;
                }

                worksheet.Cell(excelRow, 6).Value = item.LineItem;
                worksheet.Cell(excelRow, 7).Value = item.MaterialNo;
                worksheet.Cell(excelRow, 8).Value =
                    item.ShortDescription ??
                    item.BriefDescription ??
                    "";
                worksheet.Cell(excelRow, 9).Value = item.POQuantity;
                worksheet.Cell(excelRow, 10).Value = item.InvoiceQuantity;
                worksheet.Cell(excelRow, 11).Value =
                    item.InvoiceBalanceQuantity;
                worksheet.Cell(excelRow, 12).Value =
                    item.DispatchQuantity;
                worksheet.Cell(excelRow, 13).Value =
                    item.PendingDispatchQuantity;
                worksheet.Cell(excelRow, 14).Value = item.Unit ?? "";
                worksheet.Cell(excelRow, 15).Value =
                    item.ProcessStatus;
                worksheet.Cell(excelRow, 16).Value =
                    item.InvoiceStatus;
                worksheet.Cell(excelRow, 17).Value =
                    item.DispatchStatus;

                if (item.DeliveryDate.HasValue)
                {
                    worksheet.Cell(excelRow, 18).Value =
                        item.DeliveryDate.Value;
                }

                worksheet.Cell(excelRow, 19).Value =
                    item.DueStatus;
                worksheet.Cell(excelRow, 20).Value =
                    item.OverdueDays;
                worksheet.Cell(excelRow, 21).Value =
                    item.AssignedDashboard ?? "Unassigned";
                worksheet.Cell(excelRow, 22).Value =
                    item.UnitCost;
                worksheet.Cell(excelRow, 23).Value =
                    item.DrawingNo ?? "";

                if (item.IsOverdue)
                {
                    worksheet.Cell(excelRow, 19)
                        .Style.Fill.BackgroundColor =
                        XLColor.FromHtml("#FFE4E6");

                    worksheet.Cell(excelRow, 19)
                        .Style.Font.FontColor =
                        XLColor.FromHtml("#9F1239");

                    worksheet.Cell(excelRow, 19)
                        .Style.Font.Bold = true;
                }

                excelRow++;
            }

            var title = worksheet.Range("A1:W1");

            title.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#4C2FD4");
            title.Style.Font.FontColor = XLColor.White;
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 16;
            title.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Range("A2:W3")
                .Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            var header = worksheet.Range("A5:W5");

            header.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#EDE9FE");
            header.Style.Font.FontColor =
                XLColor.FromHtml("#2D1B69");
            header.Style.Font.Bold = true;

            if (rows.Count > 0)
            {
                worksheet.Range(6, 4, 5 + rows.Count, 5)
                    .Style.DateFormat.Format =
                    "dd-mmm-yyyy";

                worksheet.Range(6, 18, 5 + rows.Count, 18)
                    .Style.DateFormat.Format =
                    "dd-mmm-yyyy";

                worksheet.Range(6, 9, 5 + rows.Count, 13)
                    .Style.NumberFormat.Format =
                    "0.00";

                worksheet.Range(6, 22, 5 + rows.Count, 22)
                    .Style.NumberFormat.Format =
                    "₹ #,##0.00";
            }

            worksheet.SheetView.FreezeRows(5);
            worksheet.RangeUsed()?.SetAutoFilter();
            worksheet.Columns(1, 23).AdjustToContents();

            worksheet.Column(3).Width = 22;
            worksheet.Column(8).Width = 35;
            worksheet.Column(8).Style.Alignment.WrapText = true;
            worksheet.Column(15).Width = 18;
            worksheet.Column(16).Width = 20;
            worksheet.Column(17).Width = 18;
            worksheet.Column(21).Width = 18;
            worksheet.Column(22).Width = 16;
            worksheet.Column(23).Width = 26;
        }

        private sealed class DropdownData
        {
            public List<PendingMaterialFilterOptionViewModel>
                Handlers
            { get; set; } = new();

            public List<PendingMaterialFilterOptionViewModel>
                AssignedDashboards
            { get; set; } = new();

            public List<PendingMaterialFilterOptionViewModel>
                ProcessStatuses
            { get; set; } = new();

            public List<PendingMaterialFilterOptionViewModel>
                InvoiceStatuses
            { get; set; } = new();

            public List<PendingMaterialFilterOptionViewModel>
                DispatchStatuses
            { get; set; } = new();
        }
    }
}
