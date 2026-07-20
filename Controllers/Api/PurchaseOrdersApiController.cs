using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/purchase-orders")]
    [Produces("application/json")]
    public sealed class PurchaseOrdersApiController : ControllerBase
    {
        private const int DefaultPageSize = 25;

        private static readonly int[] SupportedPageSizes =
        {
            10,
            25,
            50,
            100
        };

        private readonly ApplicationDbContext _context;

        public PurchaseOrdersApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns a filtered, SQL-paginated Purchase Order page for Angular.
        /// The calculations match the existing MVC Purchase Order screen.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(
            typeof(PurchaseOrderListResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PurchaseOrderListResponseDto>>
            Index(
                string? search,
                string? handler,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo,
                string? status,
                string? materialStatus,
                int page = 1,
                int pageSize = DefaultPageSize,
                CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = NormalizePageSize(pageSize);

            var purchaseOrders = _context.PurchaseOrders
                .AsNoTracking();

            var handlers = await purchaseOrders
                .Where(purchaseOrder =>
                    purchaseOrder.BuyerContactPerson != null &&
                    purchaseOrder.BuyerContactPerson != string.Empty)
                .Select(purchaseOrder =>
                    purchaseOrder.BuyerContactPerson!)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync(cancellationToken);

            var filteredPurchaseOrders = ApplyPurchaseOrderFilters(
                purchaseOrders,
                search,
                handler,
                poDateFrom,
                poDateTo,
                poReceivedDateFrom,
                poReceivedDateTo);

            var aggregateQuery = ApplyAggregateFilters(
                CreateAggregateQuery(filteredPurchaseOrders),
                status,
                materialStatus);

            // Only compact aggregate rows are loaded for summary totals.
            // Linked Material entities are never loaded into application memory.
            var summaryRows = await aggregateQuery
                .ToListAsync(cancellationToken);

            var totalRecords = summaryRows.Count;
            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalRecords / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var aggregateRows = await aggregateQuery
                .OrderByDescending(row =>
                    row.POReceivedDate ??
                    row.PODate ??
                    row.CreatedDate)
                .ThenByDescending(row => row.PurchaseOrderId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new PurchaseOrderListResponseDto
            {
                Items = aggregateRows
                    .Select(MapListItem)
                    .ToList(),
                Summary = BuildSummary(summaryRows),
                Handlers = handlers,
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            });
        }

        /// <summary>
        /// Returns one Purchase Order and a SQL-paginated page of its
        /// linked materials without serializing EF navigation properties.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(
            typeof(PurchaseOrderDetailsDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PurchaseOrderDetailsDto>>
            Details(
                int id,
                int materialPage = 1,
                int materialPageSize = DefaultPageSize,
                CancellationToken cancellationToken = default)
        {
            materialPage = Math.Max(1, materialPage);
            materialPageSize = NormalizePageSize(materialPageSize);

            var header = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(purchaseOrder => purchaseOrder.Id == id)
                .Select(purchaseOrder => new
                {
                    PurchaseOrderId = purchaseOrder.Id,
                    purchaseOrder.PONumber,
                    purchaseOrder.PODate,
                    POReceivedDate = purchaseOrder.ReceivedDate,
                    Handler =
                        purchaseOrder.BuyerContactPerson ?? string.Empty,
                    purchaseOrder.BuyerEmail,
                    purchaseOrder.VendorCode,
                    purchaseOrder.VendorName,
                    purchaseOrder.PaymentTerms,
                    purchaseOrder.DeliveryCondition,
                    purchaseOrder.Currency,
                    purchaseOrder.BasicValue,
                    purchaseOrder.TaxValue,
                    purchaseOrder.GrandTotal,
                    purchaseOrder.ImportFormat,
                    purchaseOrder.PdfFilePath,
                    purchaseOrder.Status,
                    purchaseOrder.CreatedDate,
                    purchaseOrder.UpdatedDate
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (header == null)
            {
                return NotFound(new
                {
                    error = $"Purchase Order {id} was not found."
                });
            }

            var today = DateTime.Today;
            var materialsQuery = _context.Materials
                .AsNoTracking()
                .Where(material => material.PurchaseOrderId == id);

            var summary = await materialsQuery
                .GroupBy(_ => 1)
                .Select(group =>
                    new PurchaseOrderDetailsSummaryDto
                    {
                        TotalMaterials = group.Count(),
                        CompletedMaterials = group.Count(material =>
                            (material.BalanceQty <= 0 ||
                             material.InvoiceStatus == "Fully Invoiced") &&
                            (material.PendingQty <= 0 ||
                             material.DispatchStatus == "Dispatched")),
                        PendingMaterials = group.Count(material =>
                            !((material.BalanceQty <= 0 ||
                               material.InvoiceStatus == "Fully Invoiced") &&
                              (material.PendingQty <= 0 ||
                               material.DispatchStatus == "Dispatched"))),
                        OverdueMaterials = group.Count(material =>
                            material.DeliveryDate.HasValue &&
                            material.DeliveryDate < today &&
                            material.PendingQty > 0),
                        TotalPOQuantity = group.Sum(material =>
                            material.Qty),
                        TotalInvoiceBalance = group.Sum(material =>
                            material.BalanceQty > 0
                                ? material.BalanceQty
                                : 0m),
                        TotalDispatchBalance = group.Sum(material =>
                            material.PendingQty > 0
                                ? material.PendingQty
                                : 0m)
                    })
                .FirstOrDefaultAsync(cancellationToken)
                ?? new PurchaseOrderDetailsSummaryDto();

            var materialTotalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    summary.TotalMaterials /
                    (double)materialPageSize));

            if (materialPage > materialTotalPages)
            {
                materialPage = materialTotalPages;
            }

            var materials = await materialsQuery
                .OrderBy(material => material.LineItem)
                .ThenBy(material => material.MaterialNo)
                .ThenBy(material => material.Id)
                .Skip((materialPage - 1) * materialPageSize)
                .Take(materialPageSize)
                .Select(material => new PurchaseOrderMaterialDto
                {
                    Id = material.Id,
                    LineItem = material.LineItem ?? string.Empty,
                    MaterialNo = material.MaterialNo ?? string.Empty,
                    HsnSacCode = material.HsnSacCode,
                    Description =
                        material.ShortDescription != null &&
                        material.ShortDescription != string.Empty
                            ? material.ShortDescription
                            : material.BriefDescription ?? string.Empty,
                    DrawingNo = material.DrawingNo ?? string.Empty,
                    Quantity = material.Qty,
                    Unit = material.Unit ?? string.Empty,
                    InvoiceQuantity = material.InvoiceQty,
                    InvoiceBalanceQuantity =
                        material.BalanceQty > 0
                            ? material.BalanceQty
                            : 0m,
                    DispatchQuantity = material.DispatchQty,
                    DispatchBalanceQuantity =
                        material.PendingQty > 0
                            ? material.PendingQty
                            : 0m,
                    DeliveryDate = material.DeliveryDate,
                    ProcessStatus = material.ProcessStatus,
                    InvoiceStatus = material.InvoiceStatus,
                    DispatchStatus = material.DispatchStatus,
                    IsOverdue =
                        material.DeliveryDate.HasValue &&
                        material.DeliveryDate < today &&
                        material.PendingQty > 0
                })
                .ToListAsync(cancellationToken);

            return Ok(new PurchaseOrderDetailsDto
            {
                PurchaseOrderId = header.PurchaseOrderId,
                PONumber = header.PONumber,
                PODate = header.PODate,
                POReceivedDate = header.POReceivedDate,
                Handler = header.Handler,
                BuyerEmail = header.BuyerEmail,
                VendorCode = header.VendorCode,
                VendorName = header.VendorName,
                PaymentTerms = header.PaymentTerms,
                DeliveryCondition = header.DeliveryCondition,
                Currency = header.Currency,
                BasicValue = header.BasicValue,
                TaxValue = header.TaxValue,
                GrandTotal = header.GrandTotal,
                ImportFormat = header.ImportFormat,
                PdfFilePath = header.PdfFilePath,
                Status = header.Status,
                CreatedDate = header.CreatedDate,
                UpdatedDate = header.UpdatedDate,
                Summary = summary,
                Materials = new PagedResult<PurchaseOrderMaterialDto>
                {
                    Items = materials,
                    Page = materialPage,
                    PageSize = materialPageSize,
                    TotalRecords = summary.TotalMaterials
                }
            });
        }

        private static IQueryable<PurchaseOrder>
            ApplyPurchaseOrderFilters(
                IQueryable<PurchaseOrder> query,
                string? search,
                string? handler,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();

                query = query.Where(purchaseOrder =>
                    purchaseOrder.PONumber.Contains(value) ||
                    (purchaseOrder.BuyerContactPerson != null &&
                     purchaseOrder.BuyerContactPerson.Contains(value)));
            }

            if (!string.IsNullOrWhiteSpace(handler))
            {
                var value = handler.Trim();

                query = query.Where(purchaseOrder =>
                    purchaseOrder.BuyerContactPerson != null &&
                    purchaseOrder.BuyerContactPerson.Contains(value));
            }

            if (poDateFrom.HasValue)
            {
                var startDate = poDateFrom.Value.Date;

                query = query.Where(purchaseOrder =>
                    purchaseOrder.PODate.HasValue &&
                    purchaseOrder.PODate.Value >= startDate);
            }

            if (poDateTo.HasValue)
            {
                var endDateExclusive = poDateTo.Value.Date.AddDays(1);

                query = query.Where(purchaseOrder =>
                    purchaseOrder.PODate.HasValue &&
                    purchaseOrder.PODate.Value < endDateExclusive);
            }

            if (poReceivedDateFrom.HasValue)
            {
                var startDate = poReceivedDateFrom.Value.Date;

                query = query.Where(purchaseOrder =>
                    purchaseOrder.ReceivedDate.HasValue &&
                    purchaseOrder.ReceivedDate.Value >= startDate);
            }

            if (poReceivedDateTo.HasValue)
            {
                var endDateExclusive =
                    poReceivedDateTo.Value.Date.AddDays(1);

                query = query.Where(purchaseOrder =>
                    purchaseOrder.ReceivedDate.HasValue &&
                    purchaseOrder.ReceivedDate.Value < endDateExclusive);
            }

            return query;
        }

        private static IQueryable<PurchaseOrderAggregateRow>
            CreateAggregateQuery(
                IQueryable<PurchaseOrder> query)
        {
            var today = DateTime.Today;

            return query.Select(purchaseOrder =>
                new PurchaseOrderAggregateRow
                {
                    PurchaseOrderId = purchaseOrder.Id,
                    PONumber = purchaseOrder.PONumber,
                    Handler =
                        purchaseOrder.BuyerContactPerson ?? string.Empty,
                    PODate = purchaseOrder.PODate,
                    POReceivedDate = purchaseOrder.ReceivedDate,
                    PdfFilePath = purchaseOrder.PdfFilePath,
                    CreatedDate = purchaseOrder.CreatedDate,
                    TotalMaterials =
                        purchaseOrder.Materials.Count(),
                    CompletedMaterials =
                        purchaseOrder.Materials.Count(material =>
                            (material.BalanceQty <= 0 ||
                             material.InvoiceStatus == "Fully Invoiced") &&
                            (material.PendingQty <= 0 ||
                             material.DispatchStatus == "Dispatched")),
                    NotInvoicedMaterials =
                        purchaseOrder.Materials.Count(material =>
                            material.InvoiceQty <= 0 ||
                            material.InvoiceStatus == "Not Invoiced"),
                    PendingDispatchMaterials =
                        purchaseOrder.Materials.Count(material =>
                            material.PendingQty > 0 ||
                            material.DispatchStatus != "Dispatched"),
                    OverdueMaterials =
                        purchaseOrder.Materials.Count(material =>
                            material.DeliveryDate.HasValue &&
                            material.DeliveryDate < today &&
                            material.PendingQty > 0),
                    TotalPOQuantity =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)material.Qty) ?? 0m,
                    TotalInvoiceQuantity =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)material.InvoiceQty) ?? 0m,
                    TotalInvoiceBalanceQuantity =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)(material.BalanceQty > 0
                                    ? material.BalanceQty
                                    : 0m)) ?? 0m,
                    TotalDispatchQuantity =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)material.DispatchQty) ?? 0m,
                    TotalPendingDispatchQuantity =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)(material.PendingQty > 0
                                    ? material.PendingQty
                                    : 0m)) ?? 0m,
                    EarliestDeliveryDate =
                        purchaseOrder.Materials
                            .Min(material => material.DeliveryDate),
                    LatestDeliveryDate =
                        purchaseOrder.Materials
                            .Max(material => material.DeliveryDate)
                });
        }

        private static IQueryable<PurchaseOrderAggregateRow>
            ApplyAggregateFilters(
                IQueryable<PurchaseOrderAggregateRow> query,
                string? status,
                string? materialStatus)
        {
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = status.Trim().ToLowerInvariant() switch
                {
                    "open" => query.Where(row =>
                        row.TotalMaterials <= 0 ||
                        row.CompletedMaterials <= 0),

                    "in progress" => query.Where(row =>
                        row.TotalMaterials > 0 &&
                        row.CompletedMaterials > 0 &&
                        row.CompletedMaterials < row.TotalMaterials),

                    "completed" => query.Where(row =>
                        row.TotalMaterials > 0 &&
                        row.CompletedMaterials >= row.TotalMaterials),

                    _ => query.Where(_ => false)
                };
            }

            if (!string.IsNullOrWhiteSpace(materialStatus))
            {
                query = materialStatus.Trim().ToLowerInvariant() switch
                {
                    "no materials" => query.Where(row =>
                        row.TotalMaterials <= 0),

                    "pending" => query.Where(row =>
                        row.TotalMaterials > 0 &&
                        row.CompletedMaterials <= 0),

                    "partially completed" => query.Where(row =>
                        row.TotalMaterials > 0 &&
                        row.CompletedMaterials > 0 &&
                        row.CompletedMaterials < row.TotalMaterials),

                    "completed" => query.Where(row =>
                        row.TotalMaterials > 0 &&
                        row.CompletedMaterials >= row.TotalMaterials),

                    _ => query.Where(_ => false)
                };
            }

            return query;
        }

        private static PurchaseOrderListItemDto MapListItem(
            PurchaseOrderAggregateRow source)
        {
            var pendingMaterials = Math.Max(
                0,
                source.TotalMaterials - source.CompletedMaterials);

            var completionPercentage = source.TotalMaterials <= 0
                ? 0m
                : Math.Clamp(
                    source.CompletedMaterials /
                    (decimal)source.TotalMaterials * 100m,
                    0m,
                    100m);

            return new PurchaseOrderListItemDto
            {
                PurchaseOrderId = source.PurchaseOrderId,
                PONumber = source.PONumber,
                Handler = source.Handler,
                PODate = source.PODate,
                POReceivedDate = source.POReceivedDate,
                Status = GetPurchaseOrderStatus(source),
                MaterialStatus = GetMaterialStatus(source),
                PdfFilePath = source.PdfFilePath,
                CreatedDate = source.CreatedDate,
                TotalMaterials = source.TotalMaterials,
                PendingMaterials = pendingMaterials,
                CompletedMaterials = source.CompletedMaterials,
                NotInvoicedMaterials = source.NotInvoicedMaterials,
                PendingDispatchMaterials =
                    source.PendingDispatchMaterials,
                OverdueMaterials = source.OverdueMaterials,
                TotalPOQuantity = source.TotalPOQuantity,
                TotalInvoiceQuantity = source.TotalInvoiceQuantity,
                TotalInvoiceBalanceQuantity =
                    source.TotalInvoiceBalanceQuantity,
                TotalDispatchQuantity = source.TotalDispatchQuantity,
                TotalPendingDispatchQuantity =
                    source.TotalPendingDispatchQuantity,
                EarliestDeliveryDate = source.EarliestDeliveryDate,
                LatestDeliveryDate = source.LatestDeliveryDate,
                CompletionPercentage = completionPercentage
            };
        }

        private static PurchaseOrderListSummaryDto BuildSummary(
            IReadOnlyCollection<PurchaseOrderAggregateRow> rows)
        {
            return new PurchaseOrderListSummaryDto
            {
                TotalPurchaseOrders = rows.Count,
                OpenPurchaseOrders = rows.Count(row =>
                    GetPurchaseOrderStatus(row) != "Completed"),
                CompletedPurchaseOrders = rows.Count(row =>
                    GetPurchaseOrderStatus(row) == "Completed"),
                PurchaseOrdersWithPendingMaterials = rows.Count(row =>
                    row.TotalMaterials - row.CompletedMaterials > 0),
                PurchaseOrdersWithOverdueMaterials = rows.Count(row =>
                    row.OverdueMaterials > 0),
                PurchaseOrdersWithoutHandler = rows.Count(row =>
                    string.IsNullOrWhiteSpace(row.Handler)),
                TotalPOQuantity = rows.Sum(row =>
                    row.TotalPOQuantity),
                TotalInvoiceBalanceQuantity = rows.Sum(row =>
                    row.TotalInvoiceBalanceQuantity),
                TotalPendingDispatchQuantity = rows.Sum(row =>
                    row.TotalPendingDispatchQuantity)
            };
        }

        private static string GetPurchaseOrderStatus(
            PurchaseOrderAggregateRow row)
        {
            if (row.TotalMaterials <= 0)
            {
                return "Open";
            }

            if (row.CompletedMaterials >= row.TotalMaterials)
            {
                return "Completed";
            }

            return row.CompletedMaterials > 0
                ? "In Progress"
                : "Open";
        }

        private static string GetMaterialStatus(
            PurchaseOrderAggregateRow row)
        {
            if (row.TotalMaterials <= 0)
            {
                return "No Materials";
            }

            if (row.CompletedMaterials >= row.TotalMaterials)
            {
                return "Completed";
            }

            return row.CompletedMaterials > 0
                ? "Partially Completed"
                : "Pending";
        }

        private static int NormalizePageSize(int pageSize)
        {
            return SupportedPageSizes.Contains(pageSize)
                ? pageSize
                : DefaultPageSize;
        }

        private sealed class PurchaseOrderAggregateRow
        {
            public int PurchaseOrderId { get; init; }

            public string PONumber { get; init; } = string.Empty;

            public string Handler { get; init; } = string.Empty;

            public DateTime? PODate { get; init; }

            public DateTime? POReceivedDate { get; init; }

            public string? PdfFilePath { get; init; }

            public DateTime CreatedDate { get; init; }

            public int TotalMaterials { get; init; }

            public int CompletedMaterials { get; init; }

            public int NotInvoicedMaterials { get; init; }

            public int PendingDispatchMaterials { get; init; }

            public int OverdueMaterials { get; init; }

            public decimal TotalPOQuantity { get; init; }

            public decimal TotalInvoiceQuantity { get; init; }

            public decimal TotalInvoiceBalanceQuantity { get; init; }

            public decimal TotalDispatchQuantity { get; init; }

            public decimal TotalPendingDispatchQuantity { get; init; }

            public DateTime? EarliestDeliveryDate { get; init; }

            public DateTime? LatestDeliveryDate { get; init; }
        }
    }
}
