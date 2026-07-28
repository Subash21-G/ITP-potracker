using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/reports")]
    [Produces("application/json")]
    public sealed class ReportsApiController :
        ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly ExternalStockService
            _externalStockService;

        public ReportsApiController(
            ApplicationDbContext context,
            ExternalStockService
                externalStockService)
        {
            _context = context;

            _externalStockService =
                externalStockService;
        }

        [HttpGet("options")]
        public async Task<ActionResult<ReportOptionsDto>>
            GetOptions(
                CancellationToken cancellationToken)
        {
            var purchasers =
                await _context.Purchasers
                    .AsNoTracking()
                    .OrderBy(item =>
                        item.PurchaserName)
                    .Select(item =>
                        new ReportPurchaserOptionDto
                        {
                            Id =
                                item.Id,

                            PurchaserName =
                                item.PurchaserName,
                        })
                    .ToListAsync(
                        cancellationToken);

            return Ok(
                new ReportOptionsDto
                {
                    Purchasers =
                        purchasers,

                    ProcessStatuses =
                        new List<string>
                        {
                            "Pending",
                            "In Process",
                            "Completed",
                            "Hold",
                        },

                    DispatchStatuses =
                        new List<string>
                        {
                            "Pending",
                            "Partial",
                            "Dispatched",
                        },
                });
        }

        [HttpGet("preview")]
        public async Task<
            ActionResult<MaterialReportResponseDto>>
            Preview(
                DateTime? fromDate,
                DateTime? toDate,
                string? processStatus,
                string? dispatchStatus,
                int? purchaserId,
                CancellationToken cancellationToken)
        {
            var items =
                await GetReportItems(
                    fromDate,
                    toDate,
                    processStatus,
                    dispatchStatus,
                    purchaserId,
                    cancellationToken);

            return Ok(
                CreateResponse(items));
        }

        [HttpGet("materials-excel")]
        public async Task<IActionResult>
            MaterialsExcel(
                DateTime? fromDate,
                DateTime? toDate,
                string? processStatus,
                string? dispatchStatus,
                int? purchaserId,
                CancellationToken cancellationToken)
        {
            var items =
                await GetReportItems(
                    fromDate,
                    toDate,
                    processStatus,
                    dispatchStatus,
                    purchaserId,
                    cancellationToken);

            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add(
                    "Materials Report");

            string[] headers =
            {
                "PO Number",
                "Purchaser",
                "Material No",
                "Description",
                "Qty",
                "Dispatch Qty",
                "Pending Qty",
                "Live Stock",
                "Allocated Stock",
                "Stock Shortage",
                "Stock Status",
                "Delivery Date",
                "Process Status",
                "Dispatch Status",
                "RM Grade",
                "RM Source",
                "Surface Finish",
                "Cost",
                "Remark",
            };

            for (
                var column = 0;
                column < headers.Length;
                column++)
            {
                var cell =
                    worksheet.Cell(
                        1,
                        column + 1);

                cell.Value =
                    headers[column];

                cell.Style.Font.Bold =
                    true;
            }

            var row =
                2;

            foreach (var item in items)
            {
                worksheet.Cell(row, 1).Value =
                    item.PONumber;

                worksheet.Cell(row, 2).Value =
                    item.PurchaserName;

                worksheet.Cell(row, 3).Value =
                    item.MaterialNo;

                worksheet.Cell(row, 4).Value =
                    item.Description;

                worksheet.Cell(row, 5).Value =
                    item.Qty;

                worksheet.Cell(row, 6).Value =
                    item.DispatchQty;

                worksheet.Cell(row, 7).Value =
                    item.PendingQty;

                worksheet.Cell(row, 8).Value =
                    item.LiveStockQuantity;

                worksheet.Cell(row, 9).Value =
                    item.AllocatedStockQuantity;

                worksheet.Cell(row, 10).Value =
                    item.StockShortageQuantity;

                worksheet.Cell(row, 11).Value =
                    item.StockStatus;

                worksheet.Cell(row, 12).Value =
                    item.DeliveryDate
                        ?.ToString("dd-MM-yyyy") ??
                    "-";

                worksheet.Cell(row, 13).Value =
                    item.ProcessStatus;

                worksheet.Cell(row, 14).Value =
                    item.DispatchStatus;

                worksheet.Cell(row, 15).Value =
                    item.RMGrade;

                worksheet.Cell(row, 16).Value =
                    item.RMSource;

                worksheet.Cell(row, 17).Value =
                    item.SurfaceFinish;

                worksheet.Cell(row, 18).Value =
                    item.Cost;

                worksheet.Cell(row, 19).Value =
                    item.Remark;

                row++;
            }

            worksheet.Columns()
                .AdjustToContents();

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Materials_Report.xlsx");
        }

        [HttpGet("materials-pdf")]
        public async Task<IActionResult>
            MaterialsPdf(
                DateTime? fromDate,
                DateTime? toDate,
                string? processStatus,
                string? dispatchStatus,
                int? purchaserId,
                CancellationToken cancellationToken)
        {
            QuestPDF.Settings.License =
                LicenseType.Community;

            var items =
                await GetReportItems(
                    fromDate,
                    toDate,
                    processStatus,
                    dispatchStatus,
                    purchaserId,
                    cancellationToken);

            var pdf =
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(25);

                        page.Size(
                            PageSizes.A4
                                .Landscape());

                        page.Header()
                            .Text(
                                "Materials Report")
                            .FontSize(20)
                            .Bold();

                        page.Content()
                            .Table(table =>
                            {                                table.ColumnsDefinition(
                                    columns =>
                                    {
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });

                                table.Header(header =>
                                {
                                    header.Cell()
                                        .Text("PO No")
                                        .Bold();

                                    header.Cell()
                                        .Text("Purchaser")
                                        .Bold();

                                    header.Cell()
                                        .Text("Material")
                                        .Bold();

                                    header.Cell()
                                        .Text("Description")
                                        .Bold();

                                    header.Cell()
                                        .Text("Pending")
                                        .Bold();

                                    header.Cell()
                                        .Text("Allocated / Live")
                                        .Bold();

                                    header.Cell()
                                        .Text("Shortage")
                                        .Bold();

                                    header.Cell()
                                        .Text("Delivery")
                                        .Bold();

                                    header.Cell()
                                        .Text("Process")
                                        .Bold();

                                    header.Cell()
                                        .Text("Dispatch")
                                        .Bold();
                                });

                                foreach (
                                    var item in items)
                                {
                                    table.Cell()
                                        .Text(
                                            item.PONumber);

                                    table.Cell()
                                        .Text(
                                            item.PurchaserName);

                                    table.Cell()
                                        .Text(
                                            item.MaterialNo);

                                    table.Cell()
                                        .Text(
                                            item.Description);

                                    table.Cell()
                                        .Text(
                                            item.PendingQty
                                                .ToString(
                                                    "0.##"));

                                    table.Cell()
                                        .Text(
                                            $"{item.AllocatedStockQuantity:0.##} / {item.LiveStockQuantity:0.##}");

                                    table.Cell()
                                        .Text(
                                            $"{item.StockShortageQuantity:0.##} {item.StockStatus}");

                                    table.Cell()
                                        .Text(
                                            item.DeliveryDate
                                                ?.ToString(
                                                    "dd-MM-yyyy") ??
                                            "-");

                                    table.Cell()
                                        .Text(
                                            item.ProcessStatus);

                                    table.Cell()
                                        .Text(
                                            item.DispatchStatus);
                                }
                            });
                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span(
                                    "Generated on ");

                                text.Span(
                                    DateTime.Now
                                        .ToString(
                                            "dd-MM-yyyy hh:mm tt"));
                            });
                    });
                })
                .GeneratePdf();

            return File(
                pdf,
                "application/pdf",
                "Materials_Report.pdf");
        }
        private async Task<List<MaterialReportItemDto>>
            GetReportItems(
                DateTime? fromDate,
                DateTime? toDate,
                string? processStatus,
                string? dispatchStatus,
                int? purchaserId,
                CancellationToken cancellationToken)
        {
            var query =
                GetFilteredMaterials(
                    fromDate,
                    toDate,
                    processStatus,
                    dispatchStatus,
                    purchaserId);

            var items =
                await query
                    .Select(item =>
                        new MaterialReportItemDto
                        {
                            MaterialId =
                                item.Id,

                            PurchaseOrderId =
                                item.PurchaseOrderId,

                            PONumber =
                                item.PurchaseOrder != null
                                    ? item.PurchaseOrder
                                        .PONumber
                                    : string.Empty,

                            PurchaserName =
                                item.PurchaseOrder != null &&
                                item.PurchaseOrder
                                    .Purchaser != null
                                    ? item.PurchaseOrder
                                        .Purchaser
                                        .PurchaserName
                                    : string.Empty,

                            MaterialNo =
                                item.MaterialNo ??
                                string.Empty,

                            Description =
                                item.ShortDescription ??
                                item.BriefDescription ??
                                string.Empty,

                            Qty =
                                item.Qty,

                            DispatchQty =
                                item.DispatchQty,

                            PendingQty =
                                item.PendingQty,

                            DeliveryDate =
                                item.DeliveryDate,

                            ProcessStatus =
                                item.ProcessStatus ??
                                string.Empty,

                            DispatchStatus =
                                item.DispatchStatus ??
                                string.Empty,

                            RMGrade =
                                item.RMGrade ??
                                string.Empty,

                            RMSource =
                                item.RMSource ??
                                string.Empty,

                            SurfaceFinish =
                                item.SurfaceFinish ??
                                string.Empty,

                            Cost =
                                item.Cost,

                            Remark =
                                item.Remark ??
                                string.Empty
                        })
                    .ToListAsync(
                        cancellationToken);

            if (items.Count == 0)
            {
                return items;
            }

            var stockLoad =
                await _externalStockService
                    .GetAllAsync(
                        false,
                        cancellationToken);

            var materialKeys =
                items
                    .Select(item =>
                        ExternalStockService
                            .Normalize(
                                item.MaterialNo))
                    .Where(key =>
                        !string.IsNullOrWhiteSpace(
                            key))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);

            var allocationCandidates =
                await _context.Materials
                    .AsNoTracking()
                    .Where(material =>
                        material.MaterialNo != null &&
                        material.MaterialNo !=
                            string.Empty)
                    .Select(material =>
                        new
                        {
                            material.Id,
                            material.MaterialNo,
                            material.PendingQty,
                            material.DeliveryDate
                        })
                    .ToListAsync(
                        cancellationToken);

            var allocations =
                new Dictionary<
                    int,
                    (
                        decimal Live,
                        decimal Allocated,
                        decimal Minimum,
                        decimal Shortage,
                        string Status,
                        int RecordCount,
                        bool DataLive
                    )>();

            foreach (var materialKey in
                     materialKeys)
            {
                var reportMaterial =
                    items.First(item =>
                        ExternalStockService
                            .Normalize(
                                item.MaterialNo) ==
                        materialKey);

                var stockSnapshot =
                    ExternalStockService
                        .BuildSnapshot(
                            stockLoad.Items,
                            reportMaterial.MaterialNo,
                            0);

                var remainingStock =
                    stockSnapshot.LiveStock;

                var matchingRows =
                    allocationCandidates
                        .Where(material =>
                            ExternalStockService
                                .Normalize(
                                    material.MaterialNo) ==
                            materialKey)
                        .OrderBy(material =>
                            material.DeliveryDate ??
                            DateTime.MaxValue)
                        .ThenBy(material =>
                            material.Id)
                        .ToList();

                foreach (var material in
                         matchingRows)
                {
                    var requiredQuantity =
                        Math.Max(
                            0,
                            material.PendingQty);

                    var allocatedQuantity =
                        Math.Min(
                            remainingStock,
                            requiredQuantity);

                    var shortageQuantity =
                        Math.Max(
                            0,
                            requiredQuantity -
                            allocatedQuantity);

                    remainingStock =
                        Math.Max(
                            0,
                            remainingStock -
                            allocatedQuantity);

                    string stockStatus;

                    if (
                        !stockLoad.IsOnline &&
                        stockLoad.Items.Count == 0)
                    {
                        allocatedQuantity =
                            0;

                        shortageQuantity =
                            requiredQuantity;

                        stockStatus =
                            "Stock API Offline";
                    }
                    else if (
                        stockSnapshot.RecordCount == 0)
                    {
                        stockStatus =
                            "Not Found";
                    }
                    else if (
                        requiredQuantity <= 0)
                    {
                        stockStatus =
                            "No Pending";
                    }
                    else if (
                        allocatedQuantity >=
                        requiredQuantity)
                    {
                        stockStatus =
                            "Available";
                    }
                    else if (
                        allocatedQuantity > 0)
                    {
                        stockStatus =
                            "Partial Stock";
                    }
                    else if (
                        stockSnapshot.LiveStock <= 0)
                    {
                        stockStatus =
                            "Out of Stock";
                    }
                    else
                    {
                        stockStatus =
                            "Shortage";
                    }

                    allocations[material.Id] =
                        (
                            stockSnapshot.LiveStock,
                            allocatedQuantity,
                            stockSnapshot.MinimumStock,
                            shortageQuantity,
                            stockStatus,
                            stockSnapshot.RecordCount,
                            stockLoad.IsOnline
                        );
                }
            }

            foreach (var item in items)
            {
                if (
                    allocations.TryGetValue(
                        item.MaterialId,
                        out var allocation))
                {
                    item.LiveStockQuantity =
                        allocation.Live;

                    item.AllocatedStockQuantity =
                        allocation.Allocated;

                    item.StockMinimumQuantity =
                        allocation.Minimum;

                    item.StockShortageQuantity =
                        allocation.Shortage;

                    item.StockStatus =
                        allocation.Status;

                    item.StockRecordCount =
                        allocation.RecordCount;

                    item.StockDataLive =
                        allocation.DataLive;

                    continue;
                }

                item.LiveStockQuantity =
                    0;

                item.AllocatedStockQuantity =
                    0;

                item.StockMinimumQuantity =
                    0;

                item.StockShortageQuantity =
                    Math.Max(
                        0,
                        item.PendingQty);

                item.StockStatus =
                    stockLoad.IsOnline
                        ? "Not Found"
                        : "Stock API Offline";

                item.StockRecordCount =
                    0;

                item.StockDataLive =
                    stockLoad.IsOnline;
            }

            return items;
        }
        private IQueryable<Material>
            GetFilteredMaterials(
                DateTime? fromDate,
                DateTime? toDate,
                string? processStatus,
                string? dispatchStatus,
                int? purchaserId)
        {
            var query =
                _context.Materials
                    .AsNoTracking()
                    .Include(item =>
                        item.PurchaseOrder)
                    .ThenInclude(item =>
                        item!.Purchaser)
                    .AsQueryable();

            if (fromDate.HasValue)
            {
                query =
                    query.Where(item =>
                        item.DeliveryDate != null &&
                        item.DeliveryDate >=
                        fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query =
                    query.Where(item =>
                        item.DeliveryDate != null &&
                        item.DeliveryDate <=
                        toDate.Value);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    processStatus))
            {
                query =
                    query.Where(item =>
                        item.ProcessStatus ==
                        processStatus);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    dispatchStatus))
            {
                query =
                    query.Where(item =>
                        item.DispatchStatus ==
                        dispatchStatus);
            }

            if (
                purchaserId.HasValue &&
                purchaserId.Value > 0)
            {
                query =
                    query.Where(item =>
                        item.PurchaseOrder != null &&
                        item.PurchaseOrder
                            .PurchaserId ==
                        purchaserId.Value);
            }

            return query
                .OrderByDescending(item =>
                    item.Id);
        }

        private static MaterialReportResponseDto
            CreateResponse(
                List<MaterialReportItemDto> items)
        {
            return new MaterialReportResponseDto
            {
                Items =
                    items,

                Summary =
                    new MaterialReportSummaryDto
                    {
                        TotalRecords =
                            items.Count,

                        TotalQty =
                            items.Sum(item =>
                                item.Qty),

                        TotalDispatchQty =
                            items.Sum(item =>
                                item.DispatchQty),

                        TotalPendingQty =
                            items.Sum(item =>
                                item.PendingQty),

                        TotalCost =
                            items.Sum(item =>
                                item.Cost),
                    },
            };
        }
    }
}

