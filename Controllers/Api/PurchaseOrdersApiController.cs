using System.Globalization;
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

        private static readonly HashSet<string>
            PurchaseOrderExcelFilterColumns =
                new(
                    new[]
                    {
                        "po",
                        "podate",
                        "received",
                        "status",
                        "materialprogress",
                        "materials",
                        "quantity",
                        "value",
                        "dispatchbalance",
                        "delivery",
                        "pdf"
                    },
                    StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string>
            PurchaseOrderMaterialExcelFilterColumns =
                new(
                    new[]
                    {
                        "line",
                        "material",
                        "description",
                        "quantity",
                        "unitprice",
                        "invoicebalance",
                        "dispatchbalance",
                        "delivery",
                        "process",
                        "invoice",
                        "dispatch"
                    },
                    StringComparer.OrdinalIgnoreCase);

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
                int? minMaterials,
                int? maxMaterials,
                decimal? minPoQuantity,
                decimal? maxPoQuantity,
                decimal? minPoValue,
                decimal? maxPoValue,
                decimal? minDispatchBalance,
                decimal? maxDispatchBalance,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                string? pdfAvailability,
                string? excelFilters,

                string? sortColumn,
                string? sortDirection,

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
                materialStatus,
                minMaterials,
                maxMaterials,
                minPoQuantity,
                maxPoQuantity,
                minPoValue,
                maxPoValue,
                minDispatchBalance,
                maxDispatchBalance,
                deliveryDateFrom,
                deliveryDateTo,
                pdfAvailability);

            // Only compact aggregate rows are loaded for summary totals.
            // Linked Material entities are never loaded into application memory.
            var summaryRows = await aggregateQuery
                .ToListAsync(cancellationToken);

            var excelFilterSelections =
                ParsePurchaseOrderExcelFilters(
                    excelFilters);

            var filteredSummaryRows =
                summaryRows
                    .Where(row =>
                        MatchesPurchaseOrderExcelFilters(
                            row,
                            excelFilterSelections))
                    .ToList();

            var totalRecords =
                filteredSummaryRows.Count;

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalRecords / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            List<PurchaseOrderAggregateRow>
                aggregateRows;

            if (
                excelFilterSelections.Count ==
                0
            )
            {
                aggregateRows =
                    await ApplyPurchaseOrderSort(
                            aggregateQuery,
                            sortColumn,
                            sortDirection)
                        .Skip(
                            (page - 1) *
                            pageSize)
                        .Take(
                            pageSize)
                        .ToListAsync(
                            cancellationToken);
            }
            else
            {
                aggregateRows =
                    ApplyPurchaseOrderSortInMemory(
                            filteredSummaryRows,
                            sortColumn,
                            sortDirection)
                        .Skip(
                            (page - 1) *
                            pageSize)
                        .Take(
                            pageSize)
                        .ToList();
            }

            return Ok(new PurchaseOrderListResponseDto
            {
                Items = aggregateRows
                    .Select(MapListItem)
                    .ToList(),
                Summary = BuildSummary(filteredSummaryRows),
                Handlers = handlers,
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            });
        }

        // ============================================================
        // EXCEL FILTER VALUES
        // ============================================================

        [HttpGet("excel-filter-values")]
        [ProducesResponseType(
            typeof(
                IReadOnlyList<
                    ExcelFilterValueDto>),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<
                IReadOnlyList<
                    ExcelFilterValueDto>>>
            GetExcelFilterValues(
                string column,
                string? search,
                string? handler,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo,
                string? status,
                string? materialStatus,
                int? minMaterials,
                int? maxMaterials,
                decimal? minPoQuantity,
                decimal? maxPoQuantity,
                decimal? minPoValue,
                decimal? maxPoValue,
                decimal? minDispatchBalance,
                decimal? maxDispatchBalance,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                string? pdfAvailability,
                string? excelFilters,
                CancellationToken cancellationToken = default)
        {
            var normalizedColumn =
                column?
                    .Trim()
                    .ToLowerInvariant() ??
                string.Empty;

            if (
                !PurchaseOrderExcelFilterColumns
                    .Contains(
                        normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Unsupported Purchase Order Excel filter column."
                    });
            }

            var purchaseOrders =
                ApplyPurchaseOrderFilters(
                    _context.PurchaseOrders
                        .AsNoTracking(),
                    search,
                    handler,
                    poDateFrom,
                    poDateTo,
                    poReceivedDateFrom,
                    poReceivedDateTo);

            var aggregateRows =
                await ApplyAggregateFilters(
                        CreateAggregateQuery(
                            purchaseOrders),
                        status,
                        materialStatus,
                        minMaterials,
                        maxMaterials,
                        minPoQuantity,
                        maxPoQuantity,
                        minPoValue,
                        maxPoValue,
                        minDispatchBalance,
                        maxDispatchBalance,
                        deliveryDateFrom,
                        deliveryDateTo,
                        pdfAvailability)
                    .ToListAsync(
                        cancellationToken);

            var selections =
                ParsePurchaseOrderExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var values =
                aggregateRows
                    .Where(row =>
                        MatchesPurchaseOrderExcelFilters(
                            row,
                            selections))
                    .Select(row =>
                        GetPurchaseOrderExcelCellValue(
                            row,
                            normalizedColumn))
                    .GroupBy(
                        value =>
                            value,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                        new ExcelFilterValueDto
                        {
                            Value =
                                group.Key,

                            Label =
                                string.IsNullOrEmpty(
                                    group.Key)
                                    ? "(Blank)"
                                    : group.Key,

                            Count =
                                group.Count()
                        })
                    .OrderBy(
                        item =>
                            item.Label,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            return Ok(values);
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
            int? minMaterials,
            int? maxMaterials,
            decimal? minPoQuantity,
            decimal? maxPoQuantity,
            decimal? minPoValue,
            decimal? maxPoValue,
            decimal? minDispatchBalance,
            decimal? maxDispatchBalance,
            DateTime? deliveryDateFrom,
            DateTime? deliveryDateTo,
            string? pdfAvailability,
            string? excelFilters,

            string? sortColumn,
            string? sortDirection,

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
                await ApplyPurchaseOrderSort(
                        ApplyAggregateFilters(
                            CreateAggregateQuery(
                                filteredPurchaseOrders),
                            status,
                            materialStatus,
                            minMaterials,
                            maxMaterials,
                            minPoQuantity,
                            maxPoQuantity,
                            minPoValue,
                            maxPoValue,
                            minDispatchBalance,
                            maxDispatchBalance,
                            deliveryDateFrom,
                            deliveryDateTo,
                            pdfAvailability),
                        sortColumn,
                        sortDirection)
                    .ToListAsync(cancellationToken);

            var excelFilterSelections =
                ParsePurchaseOrderExcelFilters(
                    excelFilters);

            var rows = aggregateRows
                .Where(row =>
                    MatchesPurchaseOrderExcelFilters(
                        row,
                        excelFilterSelections))
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
                string? lineItem,
                string? material,
                string? description,
                decimal? minQuantity,
                decimal? maxQuantity,
                decimal? minUnitPrice,
                decimal? maxUnitPrice,
                decimal? minInvoiceBalance,
                decimal? maxInvoiceBalance,
                decimal? minDispatchBalance,
                decimal? maxDispatchBalance,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                string? overdueStatus,
                string? processStatus,
                string? invoiceStatus,
                string? dispatchStatus,
                string? materialSortColumn = null,
                string? materialSortDirection = null,
                string? materialExcelFilters = null,
                int materialPage = 1,
                int materialPageSize = DefaultPageSize,
                CancellationToken cancellationToken = default)
        {
            materialPage =
                Math.Max(
                    1,
                    materialPage);

            materialPageSize =
                NormalizePageSize(
                    materialPageSize);

            var header =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .Where(purchaseOrder =>
                        purchaseOrder.Id == id)
                    .Select(purchaseOrder =>
                        new
                        {
                            PurchaseOrderId =
                                purchaseOrder.Id,

                            purchaseOrder.PONumber,
                            purchaseOrder.PODate,

                            POReceivedDate =
                                purchaseOrder.ReceivedDate,

                            Handler =
                                purchaseOrder.BuyerContactPerson ??
                                string.Empty,

                            PurchaserName =
                                purchaseOrder.Purchaser != null
                                    ? purchaseOrder.Purchaser.PurchaserName
                                    : string.Empty,

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
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (header == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Purchase Order {id} was not found."
                    });
            }

            var today =
                DateTime.Today;

            var materialsQuery =
                _context.Materials
                    .AsNoTracking()
                    .Where(materialRow =>
                        materialRow.PurchaseOrderId == id);

            materialsQuery =
                ApplyPurchaseOrderMaterialFilters(
                    materialsQuery,
                    lineItem,
                    material,
                    description,
                    minQuantity,
                    maxQuantity,
                    minUnitPrice,
                    maxUnitPrice,
                    minInvoiceBalance,
                    maxInvoiceBalance,
                    minDispatchBalance,
                    maxDispatchBalance,
                    deliveryDateFrom,
                    deliveryDateTo,
                    overdueStatus,
                    processStatus,
                    invoiceStatus,
                    dispatchStatus,
                    today);

            materialsQuery =
                ApplyPurchaseOrderMaterialExcelFilters(
                    materialsQuery,
                    ParsePurchaseOrderMaterialExcelFilters(
                        materialExcelFilters));

            var summary =
                await materialsQuery
                    .GroupBy(_ => 1)
                    .Select(group =>
                        new PurchaseOrderDetailsSummaryDto
                        {
                            TotalMaterials =
                                group.Count(),

                            CompletedMaterials =
                                group.Count(materialRow =>
                                    (
                                        materialRow.BalanceQty <= 0 ||
                                        materialRow.InvoiceStatus ==
                                            "Fully Invoiced"
                                    ) &&
                                    (
                                        materialRow.PendingQty <= 0 ||
                                        materialRow.DispatchStatus ==
                                            "Dispatched"
                                    )),

                            PendingMaterials =
                                group.Count(materialRow =>
                                    !(
                                        (
                                            materialRow.BalanceQty <= 0 ||
                                            materialRow.InvoiceStatus ==
                                                "Fully Invoiced"
                                        ) &&
                                        (
                                            materialRow.PendingQty <= 0 ||
                                            materialRow.DispatchStatus ==
                                                "Dispatched"
                                        )
                                    )),

                            OverdueMaterials =
                                group.Count(materialRow =>
                                    materialRow.DeliveryDate.HasValue &&
                                    materialRow.DeliveryDate < today &&
                                    materialRow.PendingQty > 0),

                            TotalPOQuantity =
                                group.Sum(materialRow =>
                                    materialRow.Qty),

                            TotalInvoiceBalance =
                                group.Sum(materialRow =>
                                    materialRow.BalanceQty > 0
                                        ? materialRow.BalanceQty
                                        : 0m),

                            TotalDispatchBalance =
                                group.Sum(materialRow =>
                                    materialRow.PendingQty > 0
                                        ? materialRow.PendingQty
                                        : 0m)
                        })
                    .FirstOrDefaultAsync(
                        cancellationToken)
                    ?? new PurchaseOrderDetailsSummaryDto();

            var materialTotalPages =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        summary.TotalMaterials /
                        (double)materialPageSize));

            if (materialPage > materialTotalPages)
            {
                materialPage =
                    materialTotalPages;
            }

            var materials =
                await ApplyPurchaseOrderMaterialSort(
                        materialsQuery,
                        materialSortColumn,
                        materialSortDirection)
                    .Skip(
                        (materialPage - 1) *
                        materialPageSize)
                    .Take(
                        materialPageSize)
                    .Select(materialRow =>
                        new PurchaseOrderMaterialDto
                        {
                            Id =
                                materialRow.Id,

                            LineItem =
                                materialRow.LineItem ??
                                string.Empty,

                            MaterialNo =
                                materialRow.MaterialNo ??
                                string.Empty,

                            HsnSacCode =
                                materialRow.HsnSacCode,

                            Description =
                                materialRow.ShortDescription != null &&
                                materialRow.ShortDescription != string.Empty
                                    ? materialRow.ShortDescription
                                    : materialRow.BriefDescription ??
                                      string.Empty,

                            DrawingNo =
                                materialRow.DrawingNo ??
                                string.Empty,

                            Quantity =
                                materialRow.Qty,

                            Unit =
                                materialRow.Unit ??
                                string.Empty,

                            UnitPrice =
                                materialRow.Cost,

                            InvoiceQuantity =
                                materialRow.InvoiceQty,

                            InvoiceBalanceQuantity =
                                materialRow.BalanceQty > 0
                                    ? materialRow.BalanceQty
                                    : 0m,

                            DispatchQuantity =
                                materialRow.DispatchQty,

                            DispatchBalanceQuantity =
                                materialRow.PendingQty > 0
                                    ? materialRow.PendingQty
                                    : 0m,

                            DeliveryDate =
                                materialRow.DeliveryDate,

                            ProcessStatus =
                                materialRow.ProcessStatus,

                            InvoiceStatus =
                                materialRow.InvoiceStatus,

                            DispatchStatus =
                                materialRow.DispatchStatus,

                            IsOverdue =
                                materialRow.DeliveryDate.HasValue &&
                                materialRow.DeliveryDate < today &&
                                materialRow.PendingQty > 0
                        })
                    .ToListAsync(
                        cancellationToken);

            return Ok(
                new PurchaseOrderDetailsDto
                {
                    PurchaseOrderId =
                        header.PurchaseOrderId,

                    PONumber =
                        header.PONumber,

                    PODate =
                        header.PODate,

                    POReceivedDate =
                        header.POReceivedDate,

                    Handler =
                        header.Handler,

                    PurchaserName =
                        header.PurchaserName,

                    BuyerEmail =
                        header.BuyerEmail,

                    VendorCode =
                        header.VendorCode,

                    VendorName =
                        header.VendorName,

                    PaymentTerms =
                        header.PaymentTerms,

                    DeliveryCondition =
                        header.DeliveryCondition,

                    Currency =
                        header.Currency,

                    BasicValue =
                        header.BasicValue,

                    TaxValue =
                        header.TaxValue,

                    GrandTotal =
                        header.GrandTotal,

                    ImportFormat =
                        header.ImportFormat,

                    PdfFilePath =
                        _documentReferences.ToDownloadUrl(
                            header.PdfFilePath,
                            "po"),

                    Status =
                        header.Status,

                    CreatedDate =
                        header.CreatedDate,

                    UpdatedDate =
                        header.UpdatedDate,

                    Summary =
                        summary,

                    Materials =
                        new PagedResult<PurchaseOrderMaterialDto>
                        {
                            Items =
                                materials,

                            Page =
                                materialPage,

                            PageSize =
                                materialPageSize,

                            TotalRecords =
                                summary.TotalMaterials
                        }
                });
        }

        [HttpGet("{id:int}/materials/excel-filter-values")]
        [ProducesResponseType(
            typeof(IReadOnlyList<ExcelFilterValueDto>),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IReadOnlyList<ExcelFilterValueDto>>>
            GetPurchaseOrderMaterialExcelFilterValues(
                int id,
                string column,
                string? materialExcelFilters,
                CancellationToken cancellationToken = default)
        {
            var normalizedColumn =
                NormalizePurchaseOrderMaterialColumn(
                    column);

            if (!PurchaseOrderMaterialExcelFilterColumns.Contains(normalizedColumn))
            {
                return BadRequest(new
                {
                    error = "Unsupported Purchase Order material Excel filter column."
                });
            }

            if (!await _context.PurchaseOrders
                    .AsNoTracking()
                    .AnyAsync(
                        purchaseOrder => purchaseOrder.Id == id,
                        cancellationToken))
            {
                return NotFound(new
                {
                    error = $"Purchase Order {id} was not found."
                });
            }

            var selections =
                ParsePurchaseOrderMaterialExcelFilters(
                    materialExcelFilters);

            selections.Remove(normalizedColumn);

            var query =
                ApplyPurchaseOrderMaterialExcelFilters(
                    _context.Materials
                        .AsNoTracking()
                        .Where(materialRow =>
                            materialRow.PurchaseOrderId == id),
                    selections);

            var rows =
                await query
                    .Select(materialRow =>
                        new PurchaseOrderMaterialFilterRow
                        {
                            LineItem = materialRow.LineItem,
                            MaterialNo = materialRow.MaterialNo,
                            DrawingNo = materialRow.DrawingNo,
                            HsnSacCode = materialRow.HsnSacCode,
                            Description =
                                materialRow.ShortDescription != null &&
                                materialRow.ShortDescription != string.Empty
                                    ? materialRow.ShortDescription
                                    : materialRow.BriefDescription,
                            Quantity = materialRow.Qty,
                            Unit = materialRow.Unit,
                            UnitPrice = materialRow.Cost,
                            InvoiceBalance =
                                materialRow.BalanceQty > 0
                                    ? materialRow.BalanceQty
                                    : 0m,
                            DispatchBalance =
                                materialRow.PendingQty > 0
                                    ? materialRow.PendingQty
                                    : 0m,
                            DeliveryDate = materialRow.DeliveryDate,
                            ProcessStatus = materialRow.ProcessStatus,
                            InvoiceStatus = materialRow.InvoiceStatus,
                            DispatchStatus = materialRow.DispatchStatus
                        })
                    .ToListAsync(
                        cancellationToken);

            var values =
                rows
                    .Select(row =>
                        GetPurchaseOrderMaterialExcelCell(
                            row,
                            normalizedColumn))
                    .GroupBy(
                        cell => cell.Value,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                        new ExcelFilterValueDto
                        {
                            Value = group.Key,
                            Label = group.First().Label,
                            Count = group.Count()
                        })
                    .ToList();

            if (normalizedColumn == "line")
            {
                values = values
                    .OrderBy(item =>
                        PurchaseOrderLineItemNumber(item.Value).HasValue
                            ? 0
                            : 1)
                    .ThenBy(item =>
                        PurchaseOrderLineItemNumber(item.Value))
                    .ThenBy(
                        item => item.Label,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                values = values
                    .OrderBy(
                        item => item.Label,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return Ok(values);
        }

        private static Dictionary<string, string[]>
            ParsePurchaseOrderMaterialExcelFilters(
                string? excelFilters)
        {
            if (string.IsNullOrWhiteSpace(excelFilters))
            {
                return new Dictionary<string, string[]>(
                    StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var parsed =
                    System.Text.Json.JsonSerializer
                        .Deserialize<Dictionary<string, string[]>>(
                            excelFilters)
                    ?? new Dictionary<string, string[]>();

                return parsed
                    .Select(pair =>
                        new
                        {
                            Column = NormalizePurchaseOrderMaterialColumn(pair.Key),
                            Values = pair.Value ?? Array.Empty<string>()
                        })
                    .Where(pair =>
                        PurchaseOrderMaterialExcelFilterColumns.Contains(pair.Column))
                    .ToDictionary(
                        pair => pair.Column,
                        pair => pair.Values,
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (System.Text.Json.JsonException)
            {
                return new Dictionary<string, string[]>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private static IQueryable<Material>
            ApplyPurchaseOrderMaterialExcelFilters(
                IQueryable<Material> query,
                IReadOnlyDictionary<string, string[]> selections)
        {
            foreach (var pair in selections)
            {
                var values = pair.Value ?? Array.Empty<string>();

                if (values.Length == 0)
                {
                    query = query.Where(_ => false);
                    continue;
                }

                switch (pair.Key)
                {
                    case "line":
                        query = query.Where(materialRow =>
                            values.Contains(materialRow.LineItem ?? string.Empty));
                        break;

                    case "material":
                        query = query.Where(materialRow =>
                            values.Contains(
                                (materialRow.MaterialNo ?? string.Empty) +
                                "\u001f" +
                                (materialRow.DrawingNo ?? string.Empty) +
                                "\u001f" +
                                (materialRow.HsnSacCode ?? string.Empty)));
                        break;

                    case "description":
                        query = query.Where(materialRow =>
                            values.Contains(
                                materialRow.ShortDescription != null &&
                                materialRow.ShortDescription != string.Empty
                                    ? materialRow.ShortDescription
                                    : materialRow.BriefDescription ?? string.Empty));
                        break;

                    case "quantity":
                        query = ApplyDecimalExcelFilter(
                            query,
                            values,
                            "quantity");
                        break;

                    case "unitprice":
                        query = ApplyDecimalExcelFilter(
                            query,
                            values,
                            "unitprice");
                        break;

                    case "invoicebalance":
                        query = ApplyDecimalExcelFilter(
                            query,
                            values,
                            "invoicebalance");
                        break;

                    case "dispatchbalance":
                        query = ApplyDecimalExcelFilter(
                            query,
                            values,
                            "dispatchbalance");
                        break;

                    case "delivery":
                        var includeBlank = values.Contains(string.Empty);
                        var dates = values
                            .Select(value =>
                                DateTime.TryParseExact(
                                    value,
                                    "yyyy-MM-dd",
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.None,
                                    out var parsed)
                                        ? parsed.Date
                                        : (DateTime?)null)
                            .Where(value => value.HasValue)
                            .Select(value => value!.Value)
                            .ToArray();

                        query = query.Where(materialRow =>
                            (includeBlank && !materialRow.DeliveryDate.HasValue) ||
                            (materialRow.DeliveryDate.HasValue &&
                             dates.Contains(materialRow.DeliveryDate.Value.Date)));
                        break;

                    case "process":
                        query = query.Where(materialRow =>
                            values.Contains(materialRow.ProcessStatus));
                        break;

                    case "invoice":
                        query = query.Where(materialRow =>
                            values.Contains(materialRow.InvoiceStatus));
                        break;

                    case "dispatch":
                        query = query.Where(materialRow =>
                            values.Contains(materialRow.DispatchStatus));
                        break;
                }
            }

            return query;
        }

        private static IQueryable<Material>
            ApplyDecimalExcelFilter(
                IQueryable<Material> query,
                IReadOnlyList<string> rawValues,
                string column)
        {
            var values = rawValues
                .Select(value =>
                    decimal.TryParse(
                        value,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var parsed)
                            ? parsed
                            : (decimal?)null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();

            if (values.Length == 0)
            {
                return query.Where(_ => false);
            }

            return column switch
            {
                "quantity" => query.Where(materialRow =>
                    values.Contains(materialRow.Qty)),
                "unitprice" => query.Where(materialRow =>
                    values.Contains(materialRow.Cost)),
                "invoicebalance" => query.Where(materialRow =>
                    values.Contains(
                        materialRow.BalanceQty > 0
                            ? materialRow.BalanceQty
                            : 0m)),
                "dispatchbalance" => query.Where(materialRow =>
                    values.Contains(
                        materialRow.PendingQty > 0
                            ? materialRow.PendingQty
                            : 0m)),
                _ => query.Where(_ => false)
            };
        }

        private static IOrderedQueryable<Material>
            ApplyPurchaseOrderMaterialSort(
                IQueryable<Material> query,
                string? sortColumn,
                string? sortDirection)
        {
            var column = NormalizePurchaseOrderMaterialColumn(sortColumn);
            var descending = string.Equals(
                sortDirection?.Trim(),
                "desc",
                StringComparison.OrdinalIgnoreCase);

            return (column, descending) switch
            {
                ("line", false) => query
                    .OrderBy(materialRow => materialRow.LineItem)
                    .ThenBy(materialRow => materialRow.Id),
                ("line", true) => query
                    .OrderByDescending(materialRow => materialRow.LineItem)
                    .ThenByDescending(materialRow => materialRow.Id),
                ("material", false) => query
                    .OrderBy(materialRow => materialRow.MaterialNo)
                    .ThenBy(materialRow => materialRow.Id),
                ("material", true) => query
                    .OrderByDescending(materialRow => materialRow.MaterialNo)
                    .ThenByDescending(materialRow => materialRow.Id),
                ("description", false) => query
                    .OrderBy(materialRow =>
                        materialRow.ShortDescription ?? materialRow.BriefDescription)
                    .ThenBy(materialRow => materialRow.Id),
                ("description", true) => query
                    .OrderByDescending(materialRow =>
                        materialRow.ShortDescription ?? materialRow.BriefDescription)
                    .ThenByDescending(materialRow => materialRow.Id),
                ("quantity", false) => query
                    .OrderBy(materialRow => materialRow.Qty)
                    .ThenBy(materialRow => materialRow.Id),
                ("quantity", true) => query
                    .OrderByDescending(materialRow => materialRow.Qty)
                    .ThenByDescending(materialRow => materialRow.Id),
                ("unitprice", false) => query
                    .OrderBy(materialRow => materialRow.Cost)
                    .ThenBy(materialRow => materialRow.Id),
                ("unitprice", true) => query
                    .OrderByDescending(materialRow => materialRow.Cost)
                    .ThenByDescending(materialRow => materialRow.Id),
                ("invoicebalance", false) => query
                    .OrderBy(materialRow =>
                        materialRow.BalanceQty > 0 ? materialRow.BalanceQty : 0m)
                    .ThenBy(materialRow => materialRow.Id),
                ("invoicebalance", true) => query
                    .OrderByDescending(materialRow =>
                        materialRow.BalanceQty > 0 ? materialRow.BalanceQty : 0m)
                    .ThenByDescending(materialRow => materialRow.Id),
                ("dispatchbalance", false) => query
                    .OrderBy(materialRow =>
                        materialRow.PendingQty > 0 ? materialRow.PendingQty : 0m)
                    .ThenBy(materialRow => materialRow.Id),
                ("dispatchbalance", true) => query
                    .OrderByDescending(materialRow =>
                        materialRow.PendingQty > 0 ? materialRow.PendingQty : 0m)
                    .ThenByDescending(materialRow => materialRow.Id),
                ("delivery", false) => query
                    .OrderBy(materialRow => materialRow.DeliveryDate)
                    .ThenBy(materialRow => materialRow.Id),
                ("delivery", true) => query
                    .OrderByDescending(materialRow => materialRow.DeliveryDate)
                    .ThenByDescending(materialRow => materialRow.Id),
                ("process", false) => query
                    .OrderBy(materialRow => materialRow.ProcessStatus)
                    .ThenBy(materialRow => materialRow.Id),
                ("process", true) => query
                    .OrderByDescending(materialRow => materialRow.ProcessStatus)
                    .ThenByDescending(materialRow => materialRow.Id),
                ("invoice", false) => query
                    .OrderBy(materialRow => materialRow.InvoiceStatus)
                    .ThenBy(materialRow => materialRow.Id),
                ("invoice", true) => query
                    .OrderByDescending(materialRow => materialRow.InvoiceStatus)
                    .ThenByDescending(materialRow => materialRow.Id),
                ("dispatch", false) => query
                    .OrderBy(materialRow => materialRow.DispatchStatus)
                    .ThenBy(materialRow => materialRow.Id),
                ("dispatch", true) => query
                    .OrderByDescending(materialRow => materialRow.DispatchStatus)
                    .ThenByDescending(materialRow => materialRow.Id),
                _ => query
                    .OrderBy(materialRow => materialRow.LineItem)
                    .ThenBy(materialRow => materialRow.MaterialNo)
                    .ThenBy(materialRow => materialRow.Id)
            };
        }

        private static PurchaseOrderMaterialExcelCell
            GetPurchaseOrderMaterialExcelCell(
                PurchaseOrderMaterialFilterRow row,
                string column)
        {
            return column switch
            {
                "line" => new PurchaseOrderMaterialExcelCell(
                    row.LineItem ?? string.Empty,
                    PurchaseOrderLineItemLabel(row.LineItem)),
                "material" => new PurchaseOrderMaterialExcelCell(
                    (row.MaterialNo ?? string.Empty) +
                    "\u001f" +
                    (row.DrawingNo ?? string.Empty) +
                    "\u001f" +
                    (row.HsnSacCode ?? string.Empty),
                    PurchaseOrderMaterialLabel(row)),
                "description" => new PurchaseOrderMaterialExcelCell(
                    row.Description ?? string.Empty,
                    string.IsNullOrWhiteSpace(row.Description) ? "(Blank)" : row.Description),
                "quantity" => DecimalMaterialExcelCell(
                    row.Quantity,
                    row.Unit),
                "unitprice" => new PurchaseOrderMaterialExcelCell(
                    row.UnitPrice.ToString(CultureInfo.InvariantCulture),
                    $"₹{row.UnitPrice.ToString("N2", CultureInfo.InvariantCulture)}"),
                "invoicebalance" => DecimalMaterialExcelCell(row.InvoiceBalance),
                "dispatchbalance" => DecimalMaterialExcelCell(row.DispatchBalance),
                "delivery" => row.DeliveryDate.HasValue
                    ? new PurchaseOrderMaterialExcelCell(
                        row.DeliveryDate.Value.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture),
                        row.DeliveryDate.Value.ToString(
                            "dd MMM yyyy",
                            CultureInfo.InvariantCulture))
                    : new PurchaseOrderMaterialExcelCell(string.Empty, "(Blank)"),
                "process" => StatusMaterialExcelCell(row.ProcessStatus, "Pending"),
                "invoice" => StatusMaterialExcelCell(row.InvoiceStatus, "Not Invoiced"),
                "dispatch" => StatusMaterialExcelCell(row.DispatchStatus, "Pending"),
                _ => new PurchaseOrderMaterialExcelCell(string.Empty, "(Blank)")
            };
        }

        private static string PurchaseOrderLineItemLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "(Blank)";
            }

            var trimmed = value.Trim();
            var separatorIndex = trimmed.IndexOf('-');
            var numberText = separatorIndex >= 0
                ? trimmed[(separatorIndex + 1)..].Trim()
                : trimmed;

            return decimal.TryParse(
                numberText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var number)
                    ? number.ToString(
                        "0.############################",
                        CultureInfo.InvariantCulture)
                    : trimmed;
        }

        private static decimal? PurchaseOrderLineItemNumber(string? value)
        {
            var label = PurchaseOrderLineItemLabel(value);

            return decimal.TryParse(
                label,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var number)
                    ? number
                    : null;
        }

        private static PurchaseOrderMaterialExcelCell
            DecimalMaterialExcelCell(
                decimal value,
                string? suffix = null)
        {
            var label = value.ToString(
                "0.##",
                CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(suffix))
            {
                label += $" {suffix.Trim()}";
            }

            return new PurchaseOrderMaterialExcelCell(
                value.ToString(CultureInfo.InvariantCulture),
                label);
        }

        private static PurchaseOrderMaterialExcelCell
            StatusMaterialExcelCell(
                string? value,
                string fallback)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? fallback
                : value;

            return new PurchaseOrderMaterialExcelCell(
                normalized,
                normalized);
        }

        private static string PurchaseOrderMaterialLabel(
            PurchaseOrderMaterialFilterRow row)
        {
            var parts = new List<string>
            {
                string.IsNullOrWhiteSpace(row.MaterialNo)
                    ? "(No material number)"
                    : row.MaterialNo
            };

            if (!string.IsNullOrWhiteSpace(row.DrawingNo))
            {
                parts.Add($"Drg: {row.DrawingNo}");
            }

            if (!string.IsNullOrWhiteSpace(row.HsnSacCode))
            {
                parts.Add($"HSN: {row.HsnSacCode}");
            }

            return string.Join(" | ", parts);
        }

        private static string NormalizePurchaseOrderMaterialColumn(
            string? column)
        {
            return string.Concat(
                (column ?? string.Empty)
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant));
        }

        private sealed class PurchaseOrderMaterialFilterRow
        {
            public string? LineItem { get; init; }

            public string? MaterialNo { get; init; }

            public string? DrawingNo { get; init; }

            public string? HsnSacCode { get; init; }

            public string? Description { get; init; }

            public decimal Quantity { get; init; }

            public string? Unit { get; init; }

            public decimal UnitPrice { get; init; }

            public decimal InvoiceBalance { get; init; }

            public decimal DispatchBalance { get; init; }

            public DateTime? DeliveryDate { get; init; }

            public string? ProcessStatus { get; init; }

            public string? InvoiceStatus { get; init; }

            public string? DispatchStatus { get; init; }
        }

        private sealed record PurchaseOrderMaterialExcelCell(
            string Value,
            string Label);

        private static IQueryable<Material>
            ApplyPurchaseOrderMaterialFilters(
                IQueryable<Material> query,
                string? lineItem,
                string? material,
                string? description,
                decimal? minQuantity,
                decimal? maxQuantity,
                decimal? minUnitPrice,
                decimal? maxUnitPrice,
                decimal? minInvoiceBalance,
                decimal? maxInvoiceBalance,
                decimal? minDispatchBalance,
                decimal? maxDispatchBalance,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                string? overdueStatus,
                string? processStatus,
                string? invoiceStatus,
                string? dispatchStatus,
                DateTime today)
        {
            if (!string.IsNullOrWhiteSpace(lineItem))
            {
                var value =
                    lineItem.Trim();

                query =
                    query.Where(materialRow =>
                        materialRow.LineItem != null &&
                        materialRow.LineItem.Contains(
                            value));
            }

            if (!string.IsNullOrWhiteSpace(material))
            {
                var value =
                    material.Trim();

                query =
                    query.Where(materialRow =>
                        (
                            materialRow.MaterialNo != null &&
                            materialRow.MaterialNo.Contains(
                                value)
                        ) ||
                        (
                            materialRow.DrawingNo != null &&
                            materialRow.DrawingNo.Contains(
                                value)
                        ) ||
                        (
                            materialRow.HsnSacCode != null &&
                            materialRow.HsnSacCode.Contains(
                                value)
                        ));
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                var value =
                    description.Trim();

                query =
                    query.Where(materialRow =>
                        (
                            materialRow.ShortDescription != null &&
                            materialRow.ShortDescription.Contains(
                                value)
                        ) ||
                        (
                            materialRow.BriefDescription != null &&
                            materialRow.BriefDescription.Contains(
                                value)
                        ));
            }

            if (minQuantity.HasValue)
            {
                query =
                    query.Where(materialRow =>
                        materialRow.Qty >=
                        minQuantity.Value);
            }

            if (maxQuantity.HasValue)
            {
                query =
                    query.Where(materialRow =>
                        materialRow.Qty <=
                        maxQuantity.Value);
            }

            if (minUnitPrice.HasValue)
            {
                query =
                    query.Where(materialRow =>
                        materialRow.Cost >=
                        minUnitPrice.Value);
            }

            if (maxUnitPrice.HasValue)
            {
                query =
                    query.Where(materialRow =>
                        materialRow.Cost <=
                        maxUnitPrice.Value);
            }

            if (minInvoiceBalance.HasValue)
            {
                query =
                    query.Where(materialRow =>
                        (
                            materialRow.BalanceQty > 0
                                ? materialRow.BalanceQty
                                : 0m
                        ) >= minInvoiceBalance.Value);
            }

            if (maxInvoiceBalance.HasValue)
            {
                query =
                    query.Where(materialRow =>
                        (
                            materialRow.BalanceQty > 0
                                ? materialRow.BalanceQty
                                : 0m
                        ) <= maxInvoiceBalance.Value);
            }

            if (minDispatchBalance.HasValue)
            {
                query =
                    query.Where(materialRow =>
                        (
                            materialRow.PendingQty > 0
                                ? materialRow.PendingQty
                                : 0m
                        ) >= minDispatchBalance.Value);
            }

            if (maxDispatchBalance.HasValue)
            {
                query =
                    query.Where(materialRow =>
                        (
                            materialRow.PendingQty > 0
                                ? materialRow.PendingQty
                                : 0m
                        ) <= maxDispatchBalance.Value);
            }

            if (deliveryDateFrom.HasValue)
            {
                var start =
                    deliveryDateFrom.Value.Date;

                query =
                    query.Where(materialRow =>
                        materialRow.DeliveryDate.HasValue &&
                        materialRow.DeliveryDate.Value >=
                            start);
            }

            if (deliveryDateTo.HasValue)
            {
                var endExclusive =
                    deliveryDateTo
                        .Value
                        .Date
                        .AddDays(1);

                query =
                    query.Where(materialRow =>
                        materialRow.DeliveryDate.HasValue &&
                        materialRow.DeliveryDate.Value <
                            endExclusive);
            }

            var normalizedOverdueStatus =
                overdueStatus?.Trim();

            if (
                string.Equals(
                    normalizedOverdueStatus,
                    "overdue",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                query =
                    query.Where(materialRow =>
                        materialRow.DeliveryDate.HasValue &&
                        materialRow.DeliveryDate.Value < today &&
                        materialRow.PendingQty > 0);
            }
            else if (
                string.Equals(
                    normalizedOverdueStatus,
                    "not-overdue",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                query =
                    query.Where(materialRow =>
                        !(
                            materialRow.DeliveryDate.HasValue &&
                            materialRow.DeliveryDate.Value < today &&
                            materialRow.PendingQty > 0
                        ));
            }

            if (!string.IsNullOrWhiteSpace(processStatus))
            {
                var value =
                    processStatus.Trim();

                query =
                    query.Where(materialRow =>
                        materialRow.ProcessStatus.Contains(
                            value));
            }

            if (!string.IsNullOrWhiteSpace(invoiceStatus))
            {
                var value =
                    invoiceStatus.Trim();

                query =
                    query.Where(materialRow =>
                        materialRow.InvoiceStatus.Contains(
                            value));
            }

            if (!string.IsNullOrWhiteSpace(dispatchStatus))
            {
                var value =
                    dispatchStatus.Trim();

                query =
                    query.Where(materialRow =>
                        materialRow.DispatchStatus.Contains(
                            value));
            }

            return query;
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
                     purchaseOrder.BuyerContactPerson.Contains(value)) ||
                    (purchaseOrder.Purchaser != null &&
                     purchaseOrder.Purchaser.PurchaserName.Contains(value)));
            }

            if (!string.IsNullOrWhiteSpace(handler))
            {
                var value = handler.Trim();

                query = query.Where(purchaseOrder =>
                    purchaseOrder.BuyerContactPerson != null &&
                    purchaseOrder.BuyerContactPerson.Trim() ==
                        value);
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
                    PurchaserName =
                        purchaseOrder.Purchaser != null
                            ? purchaseOrder.Purchaser.PurchaserName
                            : string.Empty,
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

        private static IOrderedQueryable<PurchaseOrderAggregateRow>
            ApplyPurchaseOrderSort(
                IQueryable<PurchaseOrderAggregateRow> query,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                sortColumn?
                    .Trim()
                    .ToLowerInvariant() ??
                string.Empty;

            var descending =
                string.Equals(
                    sortDirection?.Trim(),
                    "desc",
                    StringComparison.OrdinalIgnoreCase);

            return column switch
            {
                "po" when descending =>
                    query
                        .OrderByDescending(row =>
                            row.PONumber)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "po" =>
                    query
                        .OrderBy(row =>
                            row.PONumber)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                "podate" when descending =>
                    query
                        .OrderByDescending(row =>
                            row.PODate.HasValue)
                        .ThenByDescending(row =>
                            row.PODate)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "podate" =>
                    query
                        .OrderByDescending(row =>
                            row.PODate.HasValue)
                        .ThenBy(row =>
                            row.PODate)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                "received" when descending =>
                    query
                        .OrderByDescending(row =>
                            row.POReceivedDate.HasValue)
                        .ThenByDescending(row =>
                            row.POReceivedDate)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "received" =>
                    query
                        .OrderByDescending(row =>
                            row.POReceivedDate.HasValue)
                        .ThenBy(row =>
                            row.POReceivedDate)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                "status" when descending =>
                    query
                        .OrderByDescending(row =>
                            row.TotalMaterials > 0 &&
                            row.CompletedMaterials >= row.TotalMaterials
                                ? 0
                                : row.TotalMaterials > 0 &&
                                  row.CompletedMaterials > 0
                                    ? 1
                                    : 2)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "status" =>
                    query
                        .OrderBy(row =>
                            row.TotalMaterials > 0 &&
                            row.CompletedMaterials >= row.TotalMaterials
                                ? 0
                                : row.TotalMaterials > 0 &&
                                  row.CompletedMaterials > 0
                                    ? 1
                                    : 2)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                "materialprogress" when descending =>
                    query
                        .OrderByDescending(row =>
                            row.TotalMaterials > 0 &&
                            row.CompletedMaterials >= row.TotalMaterials
                                ? 0
                                : row.TotalMaterials <= 0
                                    ? 1
                                    : row.CompletedMaterials > 0
                                        ? 2
                                        : 3)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "materialprogress" =>
                    query
                        .OrderBy(row =>
                            row.TotalMaterials > 0 &&
                            row.CompletedMaterials >= row.TotalMaterials
                                ? 0
                                : row.TotalMaterials <= 0
                                    ? 1
                                    : row.CompletedMaterials > 0
                                        ? 2
                                        : 3)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                "materials" when descending =>
                    query
                        .OrderByDescending(row =>
                            row.TotalMaterials)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "materials" =>
                    query
                        .OrderBy(row =>
                            row.TotalMaterials)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                "quantity" when descending =>
                    query
                        .OrderByDescending(row =>
                            row.TotalPOQuantity)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "quantity" =>
                    query
                        .OrderBy(row =>
                            row.TotalPOQuantity)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                "value" when descending =>
                    query
                        .OrderByDescending(row =>
                            row.TotalMaterialValue)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "value" =>
                    query
                        .OrderBy(row =>
                            row.TotalMaterialValue)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                "dispatchbalance" when descending =>
                    query
                        .OrderByDescending(row =>
                            row.TotalPendingDispatchQuantity)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "dispatchbalance" =>
                    query
                        .OrderBy(row =>
                            row.TotalPendingDispatchQuantity)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                "delivery" when descending =>
                    query
                        .OrderByDescending(row =>
                            row.EarliestDeliveryDate.HasValue)
                        .ThenByDescending(row =>
                            row.EarliestDeliveryDate)
                        .ThenByDescending(row =>
                            row.LatestDeliveryDate)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "delivery" =>
                    query
                        .OrderByDescending(row =>
                            row.EarliestDeliveryDate.HasValue)
                        .ThenBy(row =>
                            row.EarliestDeliveryDate)
                        .ThenBy(row =>
                            row.LatestDeliveryDate)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                "pdf" when descending =>
                    query
                        .OrderBy(row =>
                            row.PdfFilePath != null &&
                            row.PdfFilePath != string.Empty)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId),

                "pdf" =>
                    query
                        .OrderByDescending(row =>
                            row.PdfFilePath != null &&
                            row.PdfFilePath != string.Empty)
                        .ThenBy(row =>
                            row.PurchaseOrderId),

                _ =>
                    query
                        .OrderByDescending(row =>
                            row.POReceivedDate ??
                            row.PODate ??
                            row.CreatedDate)
                        .ThenByDescending(row =>
                            row.PurchaseOrderId)
            };
        }


        private static IQueryable<PurchaseOrderAggregateRow>
            ApplyAggregateFilters(
                IQueryable<PurchaseOrderAggregateRow> query,
                string? status,
                string? materialStatus,
                int? minMaterials,
                int? maxMaterials,
                decimal? minPoQuantity,
                decimal? maxPoQuantity,
                decimal? minPoValue,
                decimal? maxPoValue,
                decimal? minDispatchBalance,
                decimal? maxDispatchBalance,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                string? pdfAvailability)
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

            if (minMaterials.HasValue)
            {
                query = query.Where(row =>
                    row.TotalMaterials >=
                    minMaterials.Value);
            }

            if (maxMaterials.HasValue)
            {
                query = query.Where(row =>
                    row.TotalMaterials <=
                    maxMaterials.Value);
            }

            if (minPoQuantity.HasValue)
            {
                query = query.Where(row =>
                    row.TotalPOQuantity >=
                    minPoQuantity.Value);
            }

            if (maxPoQuantity.HasValue)
            {
                query = query.Where(row =>
                    row.TotalPOQuantity <=
                    maxPoQuantity.Value);
            }

            if (minPoValue.HasValue)
            {
                query = query.Where(row =>
                    row.TotalMaterialValue >=
                    minPoValue.Value);
            }

            if (maxPoValue.HasValue)
            {
                query = query.Where(row =>
                    row.TotalMaterialValue <=
                    maxPoValue.Value);
            }

            if (minDispatchBalance.HasValue)
            {
                query = query.Where(row =>
                    row.TotalPendingDispatchQuantity >=
                    minDispatchBalance.Value);
            }

            if (maxDispatchBalance.HasValue)
            {
                query = query.Where(row =>
                    row.TotalPendingDispatchQuantity <=
                    maxDispatchBalance.Value);
            }

            if (deliveryDateFrom.HasValue)
            {
                var startDate =
                    deliveryDateFrom.Value.Date;

                query = query.Where(row =>
                    row.LatestDeliveryDate.HasValue &&
                    row.LatestDeliveryDate.Value >=
                        startDate);
            }

            if (deliveryDateTo.HasValue)
            {
                var endDateExclusive =
                    deliveryDateTo.Value.Date.AddDays(1);

                query = query.Where(row =>
                    row.EarliestDeliveryDate.HasValue &&
                    row.EarliestDeliveryDate.Value <
                        endDateExclusive);
            }

            if (!string.IsNullOrWhiteSpace(
                    pdfAvailability))
            {
                query =
                    pdfAvailability
                        .Trim()
                        .ToLowerInvariant() switch
                    {
                        "available" =>
                            query.Where(row =>
                                row.PdfFilePath != null &&
                                row.PdfFilePath !=
                                    string.Empty),

                        "missing" =>
                            query.Where(row =>
                                row.PdfFilePath == null ||
                                row.PdfFilePath ==
                                    string.Empty),

                        _ =>
                            query
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
                PurchaserName = source.PurchaserName,
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

        private static Dictionary<
            string,
            string[]>
            ParsePurchaseOrderExcelFilters(
                string? excelFilters)
        {
            if (
                string.IsNullOrWhiteSpace(
                    excelFilters)
            )
            {
                return new Dictionary<
                    string,
                    string[]>(
                    StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var parsed =
                    System.Text.Json.JsonSerializer
                        .Deserialize<
                            Dictionary<
                                string,
                                string[]>>(
                            excelFilters)
                    ?? new Dictionary<
                        string,
                        string[]>();

                return parsed
                    .Where(pair =>
                        PurchaseOrderExcelFilterColumns
                            .Contains(
                                pair.Key))
                    .ToDictionary(
                        pair =>
                            pair.Key
                                .Trim()
                                .ToLowerInvariant(),
                        pair =>
                            pair.Value ??
                            Array.Empty<string>(),
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (
                System.Text.Json.JsonException)
            {
                return new Dictionary<
                    string,
                    string[]>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool
            MatchesPurchaseOrderExcelFilters(
                PurchaseOrderAggregateRow row,
                IReadOnlyDictionary<
                    string,
                    string[]> selections)
        {
            foreach (
                var pair in selections)
            {
                var selectedValues =
                    pair.Value ??
                    Array.Empty<string>();

                if (
                    selectedValues.Length ==
                    0
                )
                {
                    return false;
                }

                var cellValue =
                    GetPurchaseOrderExcelCellValue(
                        row,
                        pair.Key);

                if (
                    !selectedValues.Any(
                        selected =>
                            string.Equals(
                                selected,
                                cellValue,
                                StringComparison.OrdinalIgnoreCase))
                )
                {
                    return false;
                }
            }

            return true;
        }

        private static string
            GetPurchaseOrderExcelCellValue(
                PurchaseOrderAggregateRow row,
                string column)
        {
            var normalized =
                column
                    .Trim()
                    .ToLowerInvariant();

            return normalized switch
            {
                "po" =>
                    string.Join(
                        " | ",
                        new[]
                        {
                            row.PONumber,
                            row.PurchaserName,
                            row.Handler
                        }
                        .Where(value =>
                            !string.IsNullOrWhiteSpace(
                                value))),

                "podate" =>
                    row.PODate
                        ?.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)
                    ?? string.Empty,

                "received" =>
                    row.POReceivedDate
                        ?.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)
                    ?? string.Empty,

                "status" =>
                    GetPurchaseOrderStatus(
                        row),

                "materialprogress" =>
                    GetMaterialStatus(
                        row),

                "materials" =>
                    row.TotalMaterials
                        .ToString(
                            CultureInfo.InvariantCulture),

                "quantity" =>
                    row.TotalPOQuantity
                        .ToString(
                            "0.###",
                            CultureInfo.InvariantCulture),

                "value" =>
                    row.TotalMaterialValue
                        .ToString(
                            "0.##",
                            CultureInfo.InvariantCulture),

                "dispatchbalance" =>
                    row.TotalPendingDispatchQuantity
                        .ToString(
                            "0.###",
                            CultureInfo.InvariantCulture),

                "delivery" =>
                    GetDeliveryRangeExcelValue(
                        row),

                "pdf" =>
                    string.IsNullOrWhiteSpace(
                        row.PdfFilePath)
                        ? "Missing"
                        : "Available",

                _ =>
                    string.Empty
            };
        }

        private static string
            GetDeliveryRangeExcelValue(
                PurchaseOrderAggregateRow row)
        {
            if (
                !row.EarliestDeliveryDate
                    .HasValue &&
                !row.LatestDeliveryDate
                    .HasValue
            )
            {
                return string.Empty;
            }

            var earliest =
                row.EarliestDeliveryDate
                    ?.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture)
                ?? string.Empty;

            var latest =
                row.LatestDeliveryDate
                    ?.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture)
                ?? string.Empty;

            return string.Equals(
                    earliest,
                    latest,
                    StringComparison.Ordinal)
                ? earliest
                : $"{earliest} - {latest}";
        }

        private static IOrderedEnumerable<
            PurchaseOrderAggregateRow>
            ApplyPurchaseOrderSortInMemory(
                IEnumerable<
                    PurchaseOrderAggregateRow> rows,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                sortColumn?
                    .Trim()
                    .ToLowerInvariant() ??
                string.Empty;

            var descending =
                string.Equals(
                    sortDirection?.Trim(),
                    "desc",
                    StringComparison.OrdinalIgnoreCase);

            Func<
                PurchaseOrderAggregateRow,
                object?> selector =
                column switch
                {
                    "po" =>
                        row =>
                            row.PONumber,

                    "podate" =>
                        row =>
                            row.PODate,

                    "received" =>
                        row =>
                            row.POReceivedDate,

                    "status" =>
                        row =>
                            GetPurchaseOrderStatus(
                                row),

                    "materialprogress" =>
                        row =>
                            GetMaterialStatus(
                                row),

                    "materials" =>
                        row =>
                            row.TotalMaterials,

                    "quantity" =>
                        row =>
                            row.TotalPOQuantity,

                    "value" =>
                        row =>
                            row.TotalMaterialValue,

                    "dispatchbalance" =>
                        row =>
                            row.TotalPendingDispatchQuantity,

                    "delivery" =>
                        row =>
                            row.EarliestDeliveryDate,

                    "pdf" =>
                        row =>
                            string.IsNullOrWhiteSpace(
                                row.PdfFilePath)
                                ? 0
                                : 1,

                    _ =>
                        row =>
                            row.PurchaseOrderId
                };

            return descending
                ? rows
                    .OrderByDescending(
                        selector)
                    .ThenByDescending(
                        row =>
                            row.PurchaseOrderId)
                : rows
                    .OrderBy(
                        selector)
                    .ThenBy(
                        row =>
                            row.PurchaseOrderId);
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

            public string PurchaserName { get; init; } = string.Empty;

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
