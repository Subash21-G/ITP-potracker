using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;

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

        private readonly PrivateDocumentReferenceService _documentReferences;

        public PurchaseOrdersApiController(
            ApplicationDbContext context,
            PrivateDocumentReferenceService documentReferences)
        {
            _context = context;
            _documentReferences =
                documentReferences;
        }

        /// <summary>
        /// Returns a filtered, SQL-paginated Purchase Order page for Angular.
        /// The calculations follow the Purchase Order workspace rules.
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
        /// Exports all filtered Purchase Orders used by the Angular screen.
        /// </summary>
        [HttpGet("export-excel")]
        [Produces(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ExportExcel(
            string? search,
            string? handler,
            DateTime? poDateFrom,
            DateTime? poDateTo,
            DateTime? poReceivedDateFrom,
            DateTime? poReceivedDateTo,
            string? status,
            string? materialStatus,
            CancellationToken cancellationToken = default)
        {
            var filteredPurchaseOrders =
                ApplyPurchaseOrderFilters(
                    _context.PurchaseOrders.AsNoTracking(),
                    search,
                    handler,
                    poDateFrom,
                    poDateTo,
                    poReceivedDateFrom,
                    poReceivedDateTo);

            var aggregateRows =
                await ApplyAggregateFilters(
                        CreateAggregateQuery(
                            filteredPurchaseOrders),
                        status,
                        materialStatus)
                    .OrderByDescending(row =>
                        row.POReceivedDate ??
                        row.PODate ??
                        row.CreatedDate)
                    .ThenByDescending(row =>
                        row.PurchaseOrderId)
                    .ToListAsync(cancellationToken);

            var rows = aggregateRows
                .Select(MapListItem)
                .ToList();

            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add(
                    "Purchase Orders");

            WritePurchaseOrderExcel(
                worksheet,
                rows);

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Purchase_Orders_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        // ============================================================
        // BULK DELETE
        // ============================================================

        /// <summary>
        /// Deletes selected Purchase Orders that have no linked materials.
        /// Orders with linked materials are safely skipped.
        /// </summary>
        [HttpPost("bulk-delete")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(PurchaseOrderBulkDeleteResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(PurchaseOrderBulkDeleteResponseDto),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(PurchaseOrderBulkDeleteResponseDto),
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<PurchaseOrderBulkDeleteResponseDto>>
            BulkDelete(
                [FromBody]
                PurchaseOrderBulkDeleteRequestDto? request,
                CancellationToken cancellationToken = default)
        {
            var purchaseOrderIds =
                request?.PurchaseOrderIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList() ??
                new List<int>();

            if (purchaseOrderIds.Count == 0)
            {
                return BadRequest(
                    new PurchaseOrderBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Select at least one Purchase Order to delete."
                    });
            }

            if (purchaseOrderIds.Count > 200)
            {
                return BadRequest(
                    new PurchaseOrderBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "A maximum of 200 Purchase Orders can be deleted at one time.",
                        RequestedCount =
                            purchaseOrderIds.Count
                    });
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        cancellationToken);

            try
            {
                var purchaseOrders =
                    await _context.PurchaseOrders
                        .Where(purchaseOrder =>
                            purchaseOrderIds.Contains(
                                purchaseOrder.Id))
                        .OrderBy(purchaseOrder =>
                            purchaseOrder.Id)
                        .ToListAsync(
                            cancellationToken);

                var missingCount =
                    purchaseOrderIds.Count -
                    purchaseOrders.Count;

                if (purchaseOrders.Count == 0)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    return NotFound(
                        new PurchaseOrderBulkDeleteResponseDto
                        {
                            Success = false,
                            Message =
                                "None of the selected Purchase Orders were found.",
                            RequestedCount =
                                purchaseOrderIds.Count,
                            MissingCount =
                                purchaseOrderIds.Count
                        });
                }

                var existingIds =
                    purchaseOrders
                        .Select(purchaseOrder =>
                            purchaseOrder.Id)
                        .ToList();

                var linkedMaterialCounts =
                    await _context.Materials
                        .Where(material =>
                            existingIds.Contains(
                                material.PurchaseOrderId))
                        .GroupBy(material =>
                            material.PurchaseOrderId)
                        .Select(group =>
                            new
                            {
                                PurchaseOrderId =
                                    group.Key,
                                MaterialCount =
                                    group.Count()
                            })
                        .ToDictionaryAsync(
                            item =>
                                item.PurchaseOrderId,
                            item =>
                                item.MaterialCount,
                            cancellationToken);

                var blockedPurchaseOrders =
                    purchaseOrders
                        .Where(purchaseOrder =>
                            linkedMaterialCounts
                                .ContainsKey(
                                    purchaseOrder.Id))
                        .ToList();

                var deletablePurchaseOrders =
                    purchaseOrders
                        .Where(purchaseOrder =>
                            !linkedMaterialCounts
                                .ContainsKey(
                                    purchaseOrder.Id))
                        .ToList();

                var deletedPurchaseOrderIds =
                    deletablePurchaseOrders
                        .Select(purchaseOrder =>
                            purchaseOrder.Id)
                        .ToList();

                if (deletablePurchaseOrders.Count > 0)
                {
                    _context.PurchaseOrders
                        .RemoveRange(
                            deletablePurchaseOrders);

                    await _context.SaveChangesAsync(
                        cancellationToken);
                }

                await transaction.CommitAsync(
                    cancellationToken);

                var skippedCount =
                    blockedPurchaseOrders.Count;

                var linkedMaterialCount =
                    blockedPurchaseOrders.Sum(
                        purchaseOrder =>
                            linkedMaterialCounts
                                .TryGetValue(
                                    purchaseOrder.Id,
                                    out var count)
                                ? count
                                : 0);

                var message =
                    deletedPurchaseOrderIds.Count > 0
                        ? $"{deletedPurchaseOrderIds.Count} Purchase Order(s) deleted successfully."
                        : "No selected Purchase Orders were deleted.";

                var reasons =
                    new List<string>();

                if (skippedCount > 0)
                {
                    reasons.Add(
                        $"{skippedCount} linked PO(s) with " +
                        $"{linkedMaterialCount} material(s)");
                }

                if (missingCount > 0)
                {
                    reasons.Add(
                        $"{missingCount} PO(s) not found");
                }

                if (reasons.Count > 0)
                {
                    message +=
                        $" {skippedCount + missingCount} Purchase Order(s) " +
                        $"were not deleted ({string.Join(", ", reasons)}).";
                }

                return Ok(
                    new PurchaseOrderBulkDeleteResponseDto
                    {
                        Success =
                            deletedPurchaseOrderIds.Count > 0,
                        Message =
                            message,
                        RequestedCount =
                            purchaseOrderIds.Count,
                        DeletedCount =
                            deletedPurchaseOrderIds.Count,
                        SkippedCount =
                            skippedCount,
                        MissingCount =
                            missingCount,
                        LinkedMaterialCount =
                            linkedMaterialCount,
                        DeletedPurchaseOrderIds =
                            deletedPurchaseOrderIds
                    });
            }
            catch
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new PurchaseOrderBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Unable to delete the selected Purchase Orders. No partial deletion was saved.",
                        RequestedCount =
                            purchaseOrderIds.Count
                    });
            }
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
                    UnitPrice = material.Cost,
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
                PdfFilePath =
                    _documentReferences.ToDownloadUrl(
                        header.PdfFilePath,
                        "po"),
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
                    TotalMaterialValue =
                        purchaseOrder.Materials
                            .Sum(material =>
                                (decimal?)material.TotalValue) ?? 0m,
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

        private PurchaseOrderListItemDto MapListItem(
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
                PdfFilePath =
                    _documentReferences.ToDownloadUrl(
                        source.PdfFilePath,
                        "po"),
                CreatedDate = source.CreatedDate,
                TotalMaterials = source.TotalMaterials,
                PendingMaterials = pendingMaterials,
                CompletedMaterials = source.CompletedMaterials,
                NotInvoicedMaterials = source.NotInvoicedMaterials,
                PendingDispatchMaterials =
                    source.PendingDispatchMaterials,
                OverdueMaterials = source.OverdueMaterials,
                TotalPOQuantity = source.TotalPOQuantity,
                TotalMaterialValue = source.TotalMaterialValue,
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
                TotalMaterialValue = rows.Sum(row =>
                    row.TotalMaterialValue),
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

        private static void WritePurchaseOrderExcel(
            IXLWorksheet worksheet,
            IReadOnlyList<PurchaseOrderListItemDto> rows)
        {
            var totalMaterialValue =
                rows.Sum(row =>
                    row.TotalMaterialValue);

            worksheet.Cell("A1").Value =
                "PO TRACKER - PURCHASE ORDERS";

            worksheet.Range("A1:S1").Merge();

            worksheet.Cell("A2").Value =
                $"Generated: {DateTime.Now:dd-MM-yyyy hh:mm tt}";

            worksheet.Range("A2:S2").Merge();

            worksheet.Cell("A3").Value =
                $"Purchase orders: {rows.Count} | " +
                $"Total material value: ₹{totalMaterialValue:N2}";

            worksheet.Range("A3:S3").Merge();

            var headers = new[]
            {
                "S.No",
                "PO Number",
                "Handler / Person",
                "PO Date",
                "PO Received Date",
                "PO Status",
                "Material Status",
                "Total Materials",
                "Pending Materials",
                "Completed Materials",
                "Pending Dispatch Materials",
                "Overdue Materials",
                "Total PO Qty",
                "PO Material Value",
                "Dispatch Qty",
                "Dispatch Balance",
                "Earliest Delivery",
                "Latest Delivery",
                "PDF Available"
            };

            for (var index = 0;
                 index < headers.Length;
                 index++)
            {
                worksheet.Cell(
                    5,
                    index + 1).Value =
                    headers[index];
            }

            var rowNumber = 6;

            for (var index = 0;
                 index < rows.Count;
                 index++)
            {
                var item = rows[index];

                worksheet.Cell(
                    rowNumber,
                    1).Value =
                    index + 1;

                worksheet.Cell(
                    rowNumber,
                    2).Value =
                    item.PONumber;

                worksheet.Cell(
                    rowNumber,
                    3).Value =
                    string.IsNullOrWhiteSpace(item.Handler)
                        ? "Unassigned"
                        : item.Handler;

                if (item.PODate.HasValue)
                {
                    worksheet.Cell(
                        rowNumber,
                        4).Value =
                        item.PODate.Value;
                }

                if (item.POReceivedDate.HasValue)
                {
                    worksheet.Cell(
                        rowNumber,
                        5).Value =
                        item.POReceivedDate.Value;
                }

                worksheet.Cell(rowNumber, 6).Value =
                    item.Status;

                worksheet.Cell(rowNumber, 7).Value =
                    item.MaterialStatus;

                worksheet.Cell(rowNumber, 8).Value =
                    item.TotalMaterials;

                worksheet.Cell(rowNumber, 9).Value =
                    item.PendingMaterials;

                worksheet.Cell(rowNumber, 10).Value =
                    item.CompletedMaterials;

                worksheet.Cell(rowNumber, 11).Value =
                    item.PendingDispatchMaterials;

                worksheet.Cell(rowNumber, 12).Value =
                    item.OverdueMaterials;

                worksheet.Cell(rowNumber, 13).Value =
                    item.TotalPOQuantity;

                worksheet.Cell(rowNumber, 14).Value =
                    item.TotalMaterialValue;

                worksheet.Cell(rowNumber, 15).Value =
                    item.TotalDispatchQuantity;

                worksheet.Cell(rowNumber, 16).Value =
                    item.TotalPendingDispatchQuantity;

                if (item.EarliestDeliveryDate.HasValue)
                {
                    worksheet.Cell(
                        rowNumber,
                        17).Value =
                        item.EarliestDeliveryDate.Value;
                }

                if (item.LatestDeliveryDate.HasValue)
                {
                    worksheet.Cell(
                        rowNumber,
                        18).Value =
                        item.LatestDeliveryDate.Value;
                }

                worksheet.Cell(rowNumber, 19).Value =
                    string.IsNullOrWhiteSpace(
                        item.PdfFilePath)
                        ? "No"
                        : "Yes";

                StyleExcelStatus(
                    worksheet,
                    rowNumber,
                    item);

                rowNumber++;
            }

            var title =
                worksheet.Range("A1:S1");

            title.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#4C2FD4");

            title.Style.Font.FontColor =
                XLColor.White;

            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 16;

            title.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Range("A2:S3")
                .Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            var header =
                worksheet.Range("A5:S5");

            header.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#EDE9FE");

            header.Style.Font.FontColor =
                XLColor.FromHtml("#2D1B69");

            header.Style.Font.Bold = true;

            if (rows.Count > 0)
            {
                worksheet.Range(
                        6,
                        4,
                        5 + rows.Count,
                        5)
                    .Style.DateFormat.Format =
                    "dd-mmm-yyyy";

                worksheet.Range(
                        6,
                        17,
                        5 + rows.Count,
                        18)
                    .Style.DateFormat.Format =
                    "dd-mmm-yyyy";

                worksheet.Range(
                        6,
                        13,
                        5 + rows.Count,
                        13)
                    .Style.NumberFormat.Format =
                    "0.00";

                worksheet.Range(
                        6,
                        14,
                        5 + rows.Count,
                        14)
                    .Style.NumberFormat.Format =
                    "₹#,##0.00";

                worksheet.Range(
                        6,
                        15,
                        5 + rows.Count,
                        16)
                    .Style.NumberFormat.Format =
                    "0.00";
            }

            worksheet.SheetView.FreezeRows(5);

            worksheet.RangeUsed()?
                .SetAutoFilter();

            worksheet.Columns(1, 19)
                .AdjustToContents();

            worksheet.Column(3).Width = 22;
            worksheet.Column(6).Width = 16;
            worksheet.Column(7).Width = 20;
            worksheet.Column(14).Width = 18;
        }

        private static void StyleExcelStatus(
            IXLWorksheet worksheet,
            int rowNumber,
            PurchaseOrderListItemDto item)
        {
            var statusCell =
                worksheet.Cell(
                    rowNumber,
                    6);

            if (item.Status == "Completed")
            {
                statusCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#DCFCE7");

                statusCell.Style.Font.FontColor =
                    XLColor.FromHtml("#166534");
            }
            else if (item.Status == "In Progress")
            {
                statusCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#DBEAFE");

                statusCell.Style.Font.FontColor =
                    XLColor.FromHtml("#1D4ED8");
            }
            else
            {
                statusCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#FEF3C7");

                statusCell.Style.Font.FontColor =
                    XLColor.FromHtml("#92400E");
            }

            statusCell.Style.Font.Bold = true;

            if (item.OverdueMaterials > 0)
            {
                var overdueCell =
                    worksheet.Cell(
                        rowNumber,
                        12);

                overdueCell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#FFE4E6");

                overdueCell.Style.Font.FontColor =
                    XLColor.FromHtml("#9F1239");

                overdueCell.Style.Font.Bold = true;
            }
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

            public decimal TotalMaterialValue { get; init; }

            public decimal TotalInvoiceQuantity { get; init; }

            public decimal TotalInvoiceBalanceQuantity { get; init; }

            public decimal TotalDispatchQuantity { get; init; }

            public decimal TotalPendingDispatchQuantity { get; init; }

            public DateTime? EarliestDeliveryDate { get; init; }

            public DateTime? LatestDeliveryDate { get; init; }
        }
    }
}
