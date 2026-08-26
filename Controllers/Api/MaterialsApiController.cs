using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/materials")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class MaterialsApiController : ControllerBase
    {
        private const int DefaultPageSize = 25;
        private const int MaximumPageSize = 100;

        private static readonly HashSet<string>
            MaterialExcelFilterColumns =
                new(
                    new[]
                    {
                        "po",
                        "line",
                        "material",
                        "description",
                        "ordered",
                        "unitprice",
                        "materialvalue",
                        "dispatched",
                        "pending",
                        "allocatedlive",
                        "shortage",
                        "stock",
                        "process",
                        "dispatch",
                        "delivery"
                    },
                    StringComparer.OrdinalIgnoreCase);

        private const long MaximumPdfFileSize =
            50L * 1024L * 1024L;

        private const long MaximumDrawingFileSize =
            100L * 1024L * 1024L;

        private static readonly HashSet<string>
            AllowedDrawingExtensions =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ".pdf",
                    ".dwg",
                    ".dxf",
                    ".step",
                    ".stp",
                    ".iges",
                    ".igs",
                    ".zip"
                };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly PendingInvoiceMatcher
            _pendingInvoiceMatcher;
        private readonly ExternalStockService
            _externalStockService;

        private readonly PrivateDocumentStorageService
            _privateDocumentStorage;

        private readonly PrivateDocumentReferenceService
            _documentReferences;

        private static readonly Expression<
            Func<Material, MaterialListItemDto>>
                MaterialListProjection =
                    material => new MaterialListItemDto
                    {
                        Id = material.Id,

                        PurchaseOrderId =
                            material.PurchaseOrderId,

                        PONumber =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder.PONumber
                                : string.Empty,

                        Handler =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder
                                    .BuyerContactPerson ??
                                  string.Empty
                                : string.Empty,

                        PurchaserName =
                            material.PurchaseOrder != null &&
                            material.PurchaseOrder.Purchaser != null
                                ? material.PurchaseOrder
                                    .Purchaser.PurchaserName
                                : string.Empty,

                        PODate =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder.PODate ??
                                  material.PODate
                                : material.PODate,

                        POReceivedDate =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder
                                    .ReceivedDate ??
                                  material.ReceivingDate
                                : material.ReceivingDate,

                        LineItem =
                            material.LineItem ??
                            string.Empty,

                        MaterialNo =
                            material.MaterialNo ??
                            string.Empty,

                        HsnSacCode =
                            material.HsnSacCode,

                        Description =
                            material.ShortDescription != null &&
                            material.ShortDescription !=
                                string.Empty
                                ? material.ShortDescription
                                : material.BriefDescription ??
                                  string.Empty,

                        Quantity = material.Qty,

                        Unit =
                            material.Unit ??
                            string.Empty,

                        UnitPrice =
                            material.Cost,

                        PriceUnit =
                            material.PriceUnit ??
                            string.Empty,

                        TotalValue =
                            material.TotalValue,

                        InvoiceQuantity =
                            material.InvoiceQty,

                        DispatchQuantity =
                            material.DispatchQty,

                        PendingQuantity =
                            material.PendingQty,


                        InvoiceStatus =
                            material.InvoiceStatus,

                        ProcessStatus =
                            material.ProcessStatus,

                        DispatchStatus =
                            material.DispatchStatus,

                        DeliveryDate =
                            material.DeliveryDate,

                        DrawingNo =
                            material.DrawingNo ??
                            string.Empty,

                        AssignedDashboard =
                            material.AssignedDashboard ??
                            string.Empty
                    };

        private static readonly Expression<
            Func<Material, MaterialDetailsDto>>
                MaterialDetailsProjection =
                    material => new MaterialDetailsDto
                    {
                        Id = material.Id,

                        PurchaseOrderId =
                            material.PurchaseOrderId,

                        PONumber =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder.PONumber
                                : string.Empty,

                        Handler =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder
                                    .BuyerContactPerson ??
                                  string.Empty
                                : string.Empty,

                        PurchaserName =
                            material.PurchaseOrder != null &&
                            material.PurchaseOrder.Purchaser != null
                                ? material.PurchaseOrder
                                    .Purchaser.PurchaserName
                                : string.Empty,

                        LineItem =
                            material.LineItem ??
                            string.Empty,

                        ItemType =
                            material.ItemType ??
                            string.Empty,

                        MaterialNo =
                            material.MaterialNo ??
                            string.Empty,

                        HsnSacCode =
                            material.HsnSacCode ??
                            string.Empty,

                        ShortDescription =
                            material.ShortDescription ??
                            string.Empty,

                        BriefDescription =
                            material.BriefDescription ??
                            string.Empty,

                        Receiver =
                            material.Receiver ??
                            string.Empty,

                        UnloadingPoint =
                            material.UnloadingPoint ??
                            string.Empty,

                        Qty = material.Qty,

                        Unit =
                            material.Unit ??
                            string.Empty,

                        Dep =
                            material.Dep ??
                            string.Empty,

                        DispatchQty =
                            material.DispatchQty,

                        PendingQty =
                            material.PendingQty,


                        InvoiceQty =
                            material.InvoiceQty,

                        BalanceQty =
                            material.BalanceQty,

                        InvoiceStatus =
                            material.InvoiceStatus,

                        Cost =
                            material.Cost,

                        ValueOfGoods =
                            material.ValueOfGoods,

                        PriceUnit =
                            material.PriceUnit ??
                            string.Empty,

                        TotalValue =
                            material.TotalValue,

                        CGSTRate =
                            material.CGSTRate,

                        CGSTAmount =
                            material.CGSTAmount,

                        SGSTRate =
                            material.SGSTRate,

                        SGSTAmount =
                            material.SGSTAmount,

                        IGSTRate =
                            material.IGSTRate,

                        IGSTAmount =
                            material.IGSTAmount,

                        TotalValueWithTax =
                            material.TotalValueWithTax,

                        RMGrade =
                            material.RMGrade ??
                            string.Empty,

                        RMSource =
                            material.RMSource ??
                            string.Empty,

                        RMProcess =
                            material.RMProcess ??
                            string.Empty,

                        SurfaceFinish =
                            material.SurfaceFinish ??
                            string.Empty,

                        MachiningType =
                            material.MachiningType ??
                            string.Empty,

                        ProcessTime =
                            material.ProcessTime ??
                            string.Empty,

                        MaterialProcessTime =
                            material.MaterialProcessTime ??
                            string.Empty,

                        PODate =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder.PODate ??
                                  material.PODate
                                : material.PODate,

                        ReceivingDate =
                            material.PurchaseOrder != null
                                ? material.PurchaseOrder
                                    .ReceivedDate ??
                                  material.ReceivingDate
                                : material.ReceivingDate,

                        DeliveryDate =
                            material.DeliveryDate,

                        AlertDate =
                            material.AlertDate,

                        DispatchDate =
                            material.DispatchDate,

                        DrawingUpdatedDate =
                            material.DrawingUpdatedDate,

                        ProcessStartDate =
                            material.ProcessStartDate,

                        DrawingNo =
                            material.DrawingNo ??
                            string.Empty,

                        DrawingVersion =
                            material.DrawingVersion ??
                            string.Empty,

                        DrawingFilePath =
                            material.DrawingFilePath ??
                            string.Empty,

                        PdfFilePath =
                            material.PDFFilePath ??
                            string.Empty,

                        ProcessStatus =
                            material.ProcessStatus,

                        DispatchStatus =
                            material.DispatchStatus,

                        AssignedDashboard =
                            material.AssignedDashboard ??
                            string.Empty,

                        Remark =
                            material.Remark ??
                            string.Empty,

                        CreatedDate =
                            material.CreatedDate,

                        UpdatedDate =
                            material.UpdatedDate,

                        InvoiceItemCount =
                            material.InvoiceItems.Count,

                        DispatchRecordCount =
                            material.Dispatches.Count
                    };

        public MaterialsApiController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            PendingInvoiceMatcher pendingInvoiceMatcher,
            ExternalStockService externalStockService,
            PrivateDocumentStorageService
                privateDocumentStorage,
            PrivateDocumentReferenceService
                documentReferences)
        {
            _context = context;
            _environment = environment;
            _pendingInvoiceMatcher =
                pendingInvoiceMatcher;
            _externalStockService =
                externalStockService;
            _privateDocumentStorage =
                privateDocumentStorage;
            _documentReferences =
                documentReferences;
        }

        // ============================================================
        // LIST
        // ============================================================

        /// <summary>
        /// Returns a filtered material page for Angular.
        /// Database filters remain SQL-paginated. Live-stock status
        /// is calculated before pagination when that filter is active.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(
            typeof(PagedResult<MaterialListItemDto>),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            StatusCodes.Status403Forbidden)]
        public async Task<
            ActionResult<PagedResult<MaterialListItemDto>>>
            Index(
                string? search,
                string? poNumber,
                string? handler,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo,
                string? invoiceStatus,
                string? dispatchStatus,
                string? stockStatus,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,

                string? columnPoNumber,
                string? columnPurchaser,
                string? columnHandler,
                string? lineItem,
                string? materialNo,
                string? description,
                decimal? minOrderedQty,
                decimal? maxOrderedQty,
                decimal? minUnitPrice,
                decimal? maxUnitPrice,
                decimal? minMaterialValue,
                decimal? maxMaterialValue,
                decimal? minDispatchQty,
                decimal? maxDispatchQty,
                decimal? minPendingQty,
                decimal? maxPendingQty,

                decimal? minAllocatedQty,
                decimal? maxAllocatedQty,
                decimal? minLiveStockQty,
                decimal? maxLiveStockQty,
                decimal? minShortageQty,
                decimal? maxShortageQty,
                string? columnStockStatus,

                string? processStatus,
                string? columnDispatchStatus,
                DateTime? columnDeliveryDateFrom,
                DateTime? columnDeliveryDateTo,

                string? excelFilters,
                string? sortColumn,
                string? sortDirection,

                int page = 1,
                int pageSize = DefaultPageSize,
                CancellationToken cancellationToken = default)
        {
            page =
                Math.Max(
                    page,
                    1);

            pageSize =
                NormalizePageSize(
                    pageSize);

            var query =
                ApplyFilters(
                    _context.Materials
                        .AsNoTracking(),
                    search,
                    poNumber,
                    handler,
                    poDateFrom,
                    poDateTo,
                    poReceivedDateFrom,
                    poReceivedDateTo,
                    invoiceStatus,
                    dispatchStatus,
                    deliveryDateFrom,
                    deliveryDateTo,
                    columnPoNumber,
                    columnPurchaser,
                    columnHandler,
                    lineItem,
                    materialNo,
                    description,
                    minOrderedQty,
                    maxOrderedQty,
                    minUnitPrice,
                    maxUnitPrice,
                    minMaterialValue,
                    maxMaterialValue,
                    minDispatchQty,
                    maxDispatchQty,
                    minPendingQty,
                    maxPendingQty,
                    processStatus,
                    columnDispatchStatus,
                    columnDeliveryDateFrom,
                    columnDeliveryDateTo);

            var excelFilterSelections =
                ParseMaterialExcelFilters(
                    excelFilters);

            var requiresStockDerivedFiltering =
                !string.IsNullOrWhiteSpace(
                    stockStatus) ||
                !string.IsNullOrWhiteSpace(
                    columnStockStatus) ||
                minAllocatedQty.HasValue ||
                maxAllocatedQty.HasValue ||
                minLiveStockQty.HasValue ||
                maxLiveStockQty.HasValue ||
                minShortageQty.HasValue ||
                maxShortageQty.HasValue;

            var requiresExcelFiltering =
                excelFilterSelections.Count >
                0;

            var requiresExcelDerivedSort =
                RequiresMaterialExcelDerivedSort(
                    sortColumn);

            List<MaterialListItemDto> items;
            int totalRecords;
            decimal totalValue;

            if (
                !requiresStockDerivedFiltering &&
                !requiresExcelFiltering &&
                !requiresExcelDerivedSort
            )
            {
                totalRecords =
                    await query.CountAsync(
                        cancellationToken);

                totalValue =
                    await query.SumAsync(
                        material =>
                            (decimal?)material.TotalValue,
                        cancellationToken) ?? 0m;

                var totalPages =
                    Math.Max(
                        1,
                        (int)Math.Ceiling(
                            totalRecords /
                            (double)pageSize));

                if (page > totalPages)
                {
                    page = totalPages;
                }

                items =
                    await ApplyMaterialSort(
                            query,
                            sortColumn,
                            sortDirection)
                        .Skip(
                            (page - 1) *
                            pageSize)
                        .Take(pageSize)
                        .Select(
                            MaterialListProjection)
                        .ToListAsync(
                            cancellationToken);

                await PopulateExportStockAsync(
                    items,
                    cancellationToken);
            }
            else
            {
                var allItems =
                    await query
                        .Select(
                            MaterialListProjection)
                        .ToListAsync(
                            cancellationToken);

                await PopulateExportStockAsync(
                    allItems,
                    cancellationToken);

                var filteredItems =
                    allItems
                        .Where(item =>
                            MatchesStockDerivedFilters(
                                item,
                                stockStatus,
                                columnStockStatus,
                                minAllocatedQty,
                                maxAllocatedQty,
                                minLiveStockQty,
                                maxLiveStockQty,
                                minShortageQty,
                                maxShortageQty))
                        .Where(item =>
                            MatchesMaterialExcelFilters(
                                item,
                                excelFilterSelections))
                        .ToList();

                filteredItems =
                    ApplyMaterialDtoSort(
                        filteredItems,
                        sortColumn,
                        sortDirection);

                totalRecords =
                    filteredItems.Count;

                totalValue =
                    filteredItems.Sum(
                        item =>
                            item.TotalValue);

                var totalPages =
                    Math.Max(
                        1,
                        (int)Math.Ceiling(
                            totalRecords /
                            (double)pageSize));

                if (page > totalPages)
                {
                    page = totalPages;
                }

                items =
                    filteredItems
                        .Skip(
                            (page - 1) *
                            pageSize)
                        .Take(pageSize)
                        .ToList();
            }

            return Ok(
                new PagedResult<
                    MaterialListItemDto>
                {
                    Items =
                        items,

                    Page =
                        page,

                    PageSize =
                        pageSize,

                    TotalRecords =
                        totalRecords,

                    TotalValue =
                        totalValue
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
                string? poNumber,
                string? handler,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo,
                string? invoiceStatus,
                string? dispatchStatus,
                string? stockStatus,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                string? excelFilters,
                CancellationToken cancellationToken = default)
        {
            var normalizedColumn =
                column?
                    .Trim()
                    .ToLowerInvariant() ??
                string.Empty;

            if (
                !MaterialExcelFilterColumns
                    .Contains(
                        normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Unsupported material Excel filter column."
                    });
            }

            var query =
                ApplyFilters(
                    _context.Materials
                        .AsNoTracking(),
                    search,
                    poNumber,
                    handler,
                    poDateFrom,
                    poDateTo,
                    poReceivedDateFrom,
                    poReceivedDateTo,
                    invoiceStatus,
                    dispatchStatus,
                    deliveryDateFrom,
                    deliveryDateTo,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);

            var items =
                await query
                    .Select(
                        MaterialListProjection)
                    .ToListAsync(
                        cancellationToken);

            await PopulateExportStockAsync(
                items,
                cancellationToken);

            var excelFilterSelections =
                ParseMaterialExcelFilters(
                    excelFilters);

            excelFilterSelections.Remove(
                normalizedColumn);

            var values =
                items
                    .Where(item =>
                        MatchesStockDerivedFilters(
                            item,
                            stockStatus,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null))
                    .Where(item =>
                        MatchesMaterialExcelFilters(
                            item,
                            excelFilterSelections))
                    .Select(item =>
                        GetMaterialExcelCellValue(
                            item,
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

        // ============================================================
        // DETAILS
        // ============================================================

        /// <summary>
        /// Returns complete material details without EF navigation cycles.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(
            typeof(MaterialDetailsDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            StatusCodes.Status403Forbidden)]
        [ProducesResponseType(
            StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MaterialDetailsDto>>
            Details(
                int id,
                CancellationToken cancellationToken = default)
        {
            var material =
                await GetMaterialDetailsAsync(
                    id,
                    cancellationToken);

            if (material == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Material {id} was not found."
                    });
            }

            return Ok(material);
        }

        // ============================================================
        // PURCHASE ORDER OPTIONS
        // ============================================================

        /// <summary>
        /// Returns purchase orders used by Angular create/edit dropdowns.
        /// </summary>
        [HttpGet("options/purchase-orders")]
        [ProducesResponseType(
            typeof(
                IReadOnlyList<
                    MaterialPurchaseOrderOptionDto>),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            StatusCodes.Status403Forbidden)]
        public async Task<
            ActionResult<
                IReadOnlyList<
                    MaterialPurchaseOrderOptionDto>>>
            GetPurchaseOrderOptions(
                CancellationToken cancellationToken = default)
        {
            var options =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .OrderByDescending(
                        purchaseOrder =>
                            purchaseOrder.Id)
                    .Select(
                        purchaseOrder =>
                            new MaterialPurchaseOrderOptionDto
                            {
                                Id =
                                    purchaseOrder.Id,

                                PONumber =
                                    purchaseOrder.PONumber,

                                Handler =
                                    purchaseOrder
                                        .BuyerContactPerson ??
                                    string.Empty,

                                PODate =
                                    purchaseOrder.PODate,

                                ReceivedDate =
                                    purchaseOrder.ReceivedDate
                            })
                    .ToListAsync(cancellationToken);

            return Ok(options);
        }

        // ============================================================
        // CREATE
        // ============================================================

        /// <summary>
        /// Creates one material. Duplicate material numbers are allowed.
        /// InvoiceQty and DispatchQty always start at zero.
        /// </summary>
        [HttpPost]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(MaterialSaveResponseDto),
            StatusCodes.Status201Created)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            StatusCodes.Status403Forbidden)]
        public async Task<
            ActionResult<MaterialSaveResponseDto>>
            Create(
                [FromBody]
                MaterialUpsertRequestDto request,
                CancellationToken cancellationToken = default)
        {
            var purchaseOrder =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id ==
                            request.PurchaseOrderId,
                        cancellationToken);

            if (purchaseOrder == null)
            {
                ModelState.AddModelError(
                    nameof(
                        request.PurchaseOrderId),
                    "The selected purchase order was not found.");

                return ValidationProblem(ModelState);
            }

            var material = new Material
            {
                DispatchQty = 0m,
                InvoiceQty = 0m,
                CreatedDate = DateTime.Now
            };

            ApplyEditableValues(
                material,
                request);

            material.PODate ??=
                purchaseOrder.PODate;

            material.ReceivingDate ??=
                purchaseOrder.ReceivedDate;

            ApplyScheduleRules(material);

            _context.Materials.Add(material);

            await _context.SaveChangesAsync(
                cancellationToken);

            await CreateOrUpdateProcessAlertAsync(
                material,
                cancellationToken);

            await _context.SaveChangesAsync(
                cancellationToken);

            var matchedInvoiceRows =
                await _pendingInvoiceMatcher
                    .MatchPendingInvoicesForMaterialAsync(
                        material.Id);

            var savedMaterial =
                await GetMaterialDetailsAsync(
                    material.Id,
                    cancellationToken);

            var response =
                new MaterialSaveResponseDto
                {
                    Success = true,

                    Message =
                        matchedInvoiceRows > 0
                            ? $"Material created successfully. Pending invoice matched: {matchedInvoiceRows}."
                            : "Material created successfully.",

                    MaterialId =
                        material.Id,

                    MatchedPendingInvoiceRows =
                        matchedInvoiceRows,

                    Material =
                        savedMaterial
                };

            return CreatedAtAction(
                nameof(Details),
                new
                {
                    id = material.Id
                },
                response);
        }

        // ============================================================
        // UPDATE
        // ============================================================

        /// <summary>
        /// Updates editable material fields while preserving quantities
        /// controlled by Invoice and Dispatch modules.
        /// </summary>
        [HttpPut("{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(MaterialSaveResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            StatusCodes.Status403Forbidden)]
        [ProducesResponseType(
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<MaterialSaveResponseDto>>
            Update(
                int id,
                [FromBody]
                MaterialUpsertRequestDto request,
                CancellationToken cancellationToken = default)
        {
            var material =
                await _context.Materials
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id == id,
                        cancellationToken);

            if (material == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Material {id} was not found."
                    });
            }

            var purchaseOrder =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id ==
                            request.PurchaseOrderId,
                        cancellationToken);

            if (purchaseOrder == null)
            {
                ModelState.AddModelError(
                    nameof(
                        request.PurchaseOrderId),
                    "The selected purchase order was not found.");

                return ValidationProblem(ModelState);
            }

            ApplyEditableValues(
                material,
                request);

            material.PODate ??=
                purchaseOrder.PODate;

            material.ReceivingDate ??=
                purchaseOrder.ReceivedDate;

            material.UpdatedDate =
                DateTime.Now;

            ApplyScheduleRules(material);

            await CreateOrUpdateProcessAlertAsync(
                material,
                cancellationToken);

            await _context.SaveChangesAsync(
                cancellationToken);

            var matchedInvoiceRows =
                await _pendingInvoiceMatcher
                    .MatchPendingInvoicesForMaterialAsync(
                        material.Id);

            var savedMaterial =
                await GetMaterialDetailsAsync(
                    material.Id,
                    cancellationToken);

            return Ok(
                new MaterialSaveResponseDto
                {
                    Success = true,

                    Message =
                        matchedInvoiceRows > 0
                            ? $"Material updated successfully. Pending invoice matched: {matchedInvoiceRows}."
                            : "Material updated successfully.",

                    MaterialId =
                        material.Id,

                    MatchedPendingInvoiceRows =
                        matchedInvoiceRows,

                    Material =
                        savedMaterial
                });
        }

        // ============================================================
        // FILE UPLOAD
        // ============================================================

        /// <summary>
        /// Uploads or replaces a material PDF and/or drawing file.
        /// </summary>
        [HttpPost("{id:int}/files")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(200L * 1024L * 1024L)]
        [ProducesResponseType(
            typeof(MaterialSaveResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            StatusCodes.Status403Forbidden)]
        [ProducesResponseType(
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<MaterialSaveResponseDto>>
            UploadFiles(
                int id,
                [FromForm]
                IFormFile? pdfFile,
                [FromForm]
                IFormFile? drawingFile,
                CancellationToken cancellationToken = default)
        {
            if ((pdfFile == null ||
                 pdfFile.Length == 0) &&
                (drawingFile == null ||
                 drawingFile.Length == 0))
            {
                ModelState.AddModelError(
                    "files",
                    "Select a PDF or drawing file.");

                return ValidationProblem(ModelState);
            }

            var pdfError =
                ValidatePdfFile(pdfFile);

            if (pdfError != null)
            {
                ModelState.AddModelError(
                    nameof(pdfFile),
                    pdfError);
            }

            var drawingError =
                ValidateDrawingFile(
                    drawingFile);

            if (drawingError != null)
            {
                ModelState.AddModelError(
                    nameof(drawingFile),
                    drawingError);
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var material =
                await _context.Materials
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id == id,
                        cancellationToken);

            if (material == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Material {id} was not found."
                    });
            }

            if (pdfFile != null &&
                pdfFile.Length > 0)
            {
                material.PDFFilePath =
                    await SaveFileAsync(
                        pdfFile,
                        "pdfs",
                        cancellationToken);
            }

            if (drawingFile != null &&
                drawingFile.Length > 0)
            {
                material.DrawingFilePath =
                    await SaveFileAsync(
                        drawingFile,
                        "drawings",
                        cancellationToken);

                material.DrawingUpdatedDate =
                    DateTime.Now;
            }

            material.UpdatedDate =
                DateTime.Now;

            await _context.SaveChangesAsync(
                cancellationToken);

            var savedMaterial =
                await GetMaterialDetailsAsync(
                    material.Id,
                    cancellationToken);

            return Ok(
                new MaterialSaveResponseDto
                {
                    Success = true,
                    Message =
                        "Material files uploaded successfully.",
                    MaterialId =
                        material.Id,
                    Material =
                        savedMaterial
                });
        }

        // ============================================================
        // DELETE
        // ============================================================

        /// <summary>
        /// Deletes a material only when no invoice items or dispatch
        /// records are linked.
        /// </summary>
        [HttpDelete("{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(MaterialDeleteResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(MaterialDeleteResponseDto),
            StatusCodes.Status409Conflict)]
        [ProducesResponseType(
            StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            StatusCodes.Status403Forbidden)]
        [ProducesResponseType(
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<MaterialDeleteResponseDto>>
            Delete(
                int id,
                CancellationToken cancellationToken = default)
        {
            var material =
                await _context.Materials
                    .Include(item =>
                        item.InvoiceItems)
                    .Include(item =>
                        item.Dispatches)
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id == id,
                        cancellationToken);

            if (material == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Material {id} was not found."
                    });
            }

            if (material.InvoiceItems.Count > 0)
            {
                return Conflict(
                    new MaterialDeleteResponseDto
                    {
                        Success = false,

                        Message =
                            "Cannot delete this material because invoice items are already linked.",

                        MaterialId =
                            material.Id
                    });
            }

            if (material.Dispatches.Count > 0)
            {
                return Conflict(
                    new MaterialDeleteResponseDto
                    {
                        Success = false,

                        Message =
                            "Cannot delete this material because dispatch records are already linked.",

                        MaterialId =
                            material.Id
                    });
            }

            var alerts =
                await _context.Alerts
                    .Where(alert =>
                        alert.MaterialId == id)
                    .ToListAsync(
                        cancellationToken);

            if (alerts.Count > 0)
            {
                _context.Alerts.RemoveRange(
                    alerts);
            }

            _context.Materials.Remove(material);

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                new MaterialDeleteResponseDto
                {
                    Success = true,
                    Message =
                        "Material deleted successfully.",
                    MaterialId =
                        id
                });
        }

        // ============================================================
        // FILTERED MATERIAL EXCEL EXPORT
        // ============================================================

        /// <summary>
        /// Exports every material matching the Angular list filters.
        /// Live-stock status is populated before applying its filter.
        /// </summary>
        [HttpGet("export-excel")]
        public async Task<IActionResult> ExportExcel(
            string? search,
            string? poNumber,
            string? handler,
            DateTime? poDateFrom,
            DateTime? poDateTo,
            DateTime? poReceivedDateFrom,
            DateTime? poReceivedDateTo,
            string? invoiceStatus,
            string? dispatchStatus,
            string? stockStatus,
            DateTime? deliveryDateFrom,
            DateTime? deliveryDateTo,

            string? columnPoNumber,
            string? columnPurchaser,
            string? columnHandler,
            string? lineItem,
            string? materialNo,
            string? description,
            decimal? minOrderedQty,
            decimal? maxOrderedQty,
            decimal? minUnitPrice,
            decimal? maxUnitPrice,
            decimal? minMaterialValue,
            decimal? maxMaterialValue,
            decimal? minDispatchQty,
            decimal? maxDispatchQty,
            decimal? minPendingQty,
            decimal? maxPendingQty,

            decimal? minAllocatedQty,
            decimal? maxAllocatedQty,
            decimal? minLiveStockQty,
            decimal? maxLiveStockQty,
            decimal? minShortageQty,
            decimal? maxShortageQty,
            string? columnStockStatus,

            string? processStatus,
            string? columnDispatchStatus,
            DateTime? columnDeliveryDateFrom,
            DateTime? columnDeliveryDateTo,

            string? excelFilters,
            string? sortColumn,
            string? sortDirection,

            CancellationToken cancellationToken = default)
        {
            var items =
                await ApplyFilters(
                        _context.Materials
                            .AsNoTracking(),
                        search,
                        poNumber,
                        handler,
                        poDateFrom,
                        poDateTo,
                        poReceivedDateFrom,
                        poReceivedDateTo,
                        invoiceStatus,
                        dispatchStatus,
                        deliveryDateFrom,
                        deliveryDateTo,
                        columnPoNumber,
                        columnPurchaser,
                        columnHandler,
                        lineItem,
                        materialNo,
                        description,
                        minOrderedQty,
                        maxOrderedQty,
                        minUnitPrice,
                        maxUnitPrice,
                        minMaterialValue,
                        maxMaterialValue,
                        minDispatchQty,
                        maxDispatchQty,
                        minPendingQty,
                        maxPendingQty,
                        processStatus,
                        columnDispatchStatus,
                        columnDeliveryDateFrom,
                        columnDeliveryDateTo)
                    .OrderByDescending(
                        material =>
                            material.Id)
                    .Select(
                        MaterialListProjection)
                    .ToListAsync(
                        cancellationToken);

            await PopulateExportStockAsync(
                items,
                cancellationToken);

            var requiresStockDerivedFiltering =
                !string.IsNullOrWhiteSpace(
                    stockStatus) ||
                !string.IsNullOrWhiteSpace(
                    columnStockStatus) ||
                minAllocatedQty.HasValue ||
                maxAllocatedQty.HasValue ||
                minLiveStockQty.HasValue ||
                maxLiveStockQty.HasValue ||
                minShortageQty.HasValue ||
                maxShortageQty.HasValue;

            if (requiresStockDerivedFiltering)
            {
                items =
                    items
                        .Where(item =>
                            MatchesStockDerivedFilters(
                                item,
                                stockStatus,
                                columnStockStatus,
                                minAllocatedQty,
                                maxAllocatedQty,
                                minLiveStockQty,
                                maxLiveStockQty,
                                minShortageQty,
                                maxShortageQty))
                        .ToList();
            }

            var excelFilterSelections =
                ParseMaterialExcelFilters(
                    excelFilters);

            if (
                excelFilterSelections.Count >
                0
            )
            {
                items =
                    items
                        .Where(item =>
                            MatchesMaterialExcelFilters(
                                item,
                                excelFilterSelections))
                        .ToList();
            }

            items =
                ApplyMaterialDtoSort(
                    items,
                    sortColumn,
                    sortDirection);

            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add(
                    "Materials");

            WriteMaterialExcel(
                worksheet,
                items);

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Materials_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        // ============================================================
        // BULK DELETE
        // ============================================================

        /// <summary>
        /// Deletes selected materials that have no linked invoice items
        /// or dispatch records. Linked materials are safely skipped.
        /// </summary>
        [HttpPost("bulk-delete")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<
            ActionResult<MaterialBulkDeleteResponseDto>>
            BulkDelete(
                [FromBody]
                MaterialBulkDeleteRequestDto? request,
                CancellationToken cancellationToken = default)
        {
            var materialIds =
                request?.MaterialIds
                    .Where(id =>
                        id > 0)
                    .Distinct()
                    .ToList() ??
                new List<int>();

            if (materialIds.Count == 0)
            {
                return BadRequest(
                    new MaterialBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Select at least one material to delete."
                    });
            }

            if (materialIds.Count > 200)
            {
                return BadRequest(
                    new MaterialBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "A maximum of 200 materials can be deleted at one time.",
                        RequestedCount =
                            materialIds.Count
                    });
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        cancellationToken);

            try
            {
                var materials =
                    await _context.Materials
                        .Include(material =>
                            material.InvoiceItems)
                        .Include(material =>
                            material.Dispatches)
                        .Where(material =>
                            materialIds.Contains(
                                material.Id))
                        .OrderBy(material =>
                            material.Id)
                        .ToListAsync(
                            cancellationToken);

                var missingCount =
                    materialIds.Count -
                    materials.Count;

                if (materials.Count == 0)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    return NotFound(
                        new MaterialBulkDeleteResponseDto
                        {
                            Success = false,
                            Message =
                                "None of the selected materials were found.",
                            RequestedCount =
                                materialIds.Count,
                            MissingCount =
                                materialIds.Count
                        });
                }

                var invoiceLinkedCount =
                    materials.Count(material =>
                        material.InvoiceItems.Count > 0);

                var dispatchLinkedCount =
                    materials.Count(material =>
                        material.Dispatches.Count > 0);

                var deletableMaterials =
                    materials
                        .Where(material =>
                            material.InvoiceItems.Count == 0 &&
                            material.Dispatches.Count == 0)
                        .ToList();

                var skippedCount =
                    materials.Count -
                    deletableMaterials.Count;

                var deletedMaterialIds =
                    deletableMaterials
                        .Select(material =>
                            material.Id)
                        .ToList();

                if (deletedMaterialIds.Count > 0)
                {
                    var alerts =
                        await _context.Alerts
                            .Where(alert =>
                                deletedMaterialIds.Contains(
                                    alert.MaterialId))
                            .ToListAsync(
                                cancellationToken);

                    if (alerts.Count > 0)
                    {
                        _context.Alerts.RemoveRange(
                            alerts);
                    }

                    _context.Materials.RemoveRange(
                        deletableMaterials);

                    await _context.SaveChangesAsync(
                        cancellationToken);
                }

                await transaction.CommitAsync(
                    cancellationToken);

                var message =
                    deletedMaterialIds.Count > 0
                        ? $"{deletedMaterialIds.Count} material(s) deleted successfully."
                        : "No selected materials were deleted.";

                var skippedDetails =
                    new List<string>();

                if (invoiceLinkedCount > 0)
                {
                    skippedDetails.Add(
                        $"{invoiceLinkedCount} invoice-linked");
                }

                if (dispatchLinkedCount > 0)
                {
                    skippedDetails.Add(
                        $"{dispatchLinkedCount} dispatch-linked");
                }

                if (missingCount > 0)
                {
                    skippedDetails.Add(
                        $"{missingCount} not found");
                }

                if (skippedDetails.Count > 0)
                {
                    message +=
                        $" {skippedCount + missingCount} material(s) were not deleted " +
                        $"({string.Join(", ", skippedDetails)}).";
                }

                return Ok(
                    new MaterialBulkDeleteResponseDto
                    {
                        Success =
                            deletedMaterialIds.Count > 0,
                        Message =
                            message,
                        RequestedCount =
                            materialIds.Count,
                        DeletedCount =
                            deletedMaterialIds.Count,
                        SkippedCount =
                            skippedCount,
                        MissingCount =
                            missingCount,
                        InvoiceLinkedCount =
                            invoiceLinkedCount,
                        DispatchLinkedCount =
                            dispatchLinkedCount,
                        DeletedMaterialIds =
                            deletedMaterialIds
                    });
            }
            catch
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new MaterialBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Unable to delete the selected materials. No partial deletion was saved.",
                        RequestedCount =
                            materialIds.Count
                    });
            }
        }

        private async Task PopulateExportStockAsync(
            List<MaterialListItemDto> items,
            CancellationToken cancellationToken)
        {
            if (items.Count == 0)
            {
                return;
            }

            var stockLoad =
                await _externalStockService
                    .GetAllAsync(
                        false,
                        cancellationToken);

            var materialKeys =
                items
                    .Select(item =>
                        ExternalStockService.Normalize(
                            item.MaterialNo))
                    .Where(key =>
                        !string.IsNullOrWhiteSpace(
                            key))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);

            var requestedMaterialNumbers =
                items
                    .Select(item =>
                        item.MaterialNo)
                    .Where(materialNo =>
                        !string.IsNullOrWhiteSpace(
                            materialNo))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var typedAllocationCandidates =
                requestedMaterialNumbers.Count == 0
                    ? new List<MaterialStockAllocationCandidate>()
                    : await _context.Materials
                        .AsNoTracking()
                        .Where(material =>
                            material.MaterialNo != null &&
                            requestedMaterialNumbers.Contains(
                                material.MaterialNo))
                        .Select(material =>
                            new MaterialStockAllocationCandidate
                            {
                                Id =
                                    material.Id,

                                MaterialNo =
                                    material.MaterialNo,

                                PendingQty =
                                    material.PendingQty,

                                DeliveryDate =
                                    material.DeliveryDate
                            })
                        .ToListAsync(
                            cancellationToken);

            var allocations =
                new Dictionary<
                    int,
                    (
                        decimal Allocated,
                        decimal Shortage,
                        string Status
                    )>();

            foreach (var materialKey in
                     materialKeys)
            {
                var exportMaterial =
                    items.First(item =>
                        ExternalStockService.Normalize(
                            item.MaterialNo) ==
                        materialKey);

                var stockSnapshot =
                    ExternalStockService.BuildSnapshot(
                        stockLoad.Items,
                        exportMaterial.MaterialNo,
                        0);

                var remainingStock =
                    stockSnapshot.LiveStock;

                var matchingRows =
                    typedAllocationCandidates
                        .Where(material =>
                            ExternalStockService.Normalize(
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

                    string status;

                    if (stockSnapshot.RecordCount == 0)
                    {
                        status = "Not Found";
                    }
                    else if (requiredQuantity <= 0)
                    {
                        status = "No Pending";
                    }
                    else if (
                        allocatedQuantity >=
                        requiredQuantity)
                    {
                        status = "Available";
                    }
                    else if (allocatedQuantity > 0)
                    {
                        status = "Partial Stock";
                    }
                    else if (
                        stockSnapshot.LiveStock <= 0)
                    {
                        status = "Out of Stock";
                    }
                    else
                    {
                        status = "Shortage";
                    }

                    allocations[material.Id] =
                        (
                            allocatedQuantity,
                            shortageQuantity,
                            status
                        );
                }
            }

            foreach (var item in items)
            {
                var stockSnapshot =
                    ExternalStockService.BuildSnapshot(
                        stockLoad.Items,
                        item.MaterialNo,
                        item.PendingQuantity);

                item.LiveStockQuantity =
                    stockSnapshot.LiveStock;

                item.StockMinimumQuantity =
                    stockSnapshot.MinimumStock;

                item.StockRecordCount =
                    stockSnapshot.RecordCount;

                item.StockDataLive =
                    stockLoad.IsOnline;

                if (
                    allocations.TryGetValue(
                        item.Id,
                        out var allocation))
                {
                    item.AllocatedStockQuantity =
                        allocation.Allocated;

                    item.StockShortageQuantity =
                        allocation.Shortage;

                    item.StockStatus =
                        allocation.Status;
                }
                else
                {
                    item.AllocatedStockQuantity = 0;

                    item.StockShortageQuantity =
                        Math.Max(
                            0,
                            item.PendingQuantity);

                    item.StockStatus =
                        stockSnapshot.RecordCount == 0
                            ? "Not Found"
                            : "Shortage";
                }

                if (
                    !stockLoad.IsOnline &&
                    stockLoad.Items.Count == 0)
                {
                    item.StockStatus =
                        "Stock API Offline";
                }
            }
        }

        private sealed class MaterialStockAllocationCandidate
        {
            public int Id { get; init; }

            public string? MaterialNo { get; init; }

            public decimal PendingQty { get; init; }

            public DateTime? DeliveryDate { get; init; }
        }

        private static void WriteMaterialExcel(
            IXLWorksheet worksheet,
            IReadOnlyList<MaterialListItemDto> items)
        {
            worksheet.Cell("A1").Value =
                "PO TRACKER - MATERIAL LIST";

            worksheet.Range("A1:T1").Merge();

            worksheet.Cell("A2").Value =
                $"Generated: {DateTime.Now:dd-MM-yyyy hh:mm tt}";

            worksheet.Range("A2:T2").Merge();

            worksheet.Cell("A3").Value =
                $"Total materials: {items.Count}";

            worksheet.Range("A3:T3").Merge();

            var headers = new[]
            {
                "S.No",
                "PO Number",
                "Handler / Person",
                "PO Date",
                "PO Received Date",
                "Line Item",
                "Material Number",
                "HSN/SAC",
                "Description",
                "PO Qty",
                "Unit Price",
                "Price Unit",
                "Material Value",
                "Dispatch Qty",
                "Pending Dispatch Qty",
                "Dispatch Status",
                "Live Stock",
                "Shortage",
                "Stock Status",
                "Drawing Number"
            };

            for (
                var index = 0;
                index < headers.Length;
                index++)
            {
                worksheet.Cell(
                    5,
                    index + 1).Value =
                    headers[index];
            }

            for (
                var index = 0;
                index < items.Count;
                index++)
            {
                var item =
                    items[index];

                var row =
                    index + 6;

                worksheet.Cell(row, 1).Value =
                    index + 1;

                worksheet.Cell(row, 2).Value =
                    item.PONumber;

                worksheet.Cell(row, 3).Value =
                    string.IsNullOrWhiteSpace(
                        item.Handler)
                        ? "Unassigned"
                        : item.Handler;

                if (item.PODate.HasValue)
                {
                    worksheet.Cell(row, 4).Value =
                        item.PODate.Value;
                }

                if (item.POReceivedDate.HasValue)
                {
                    worksheet.Cell(row, 5).Value =
                        item.POReceivedDate.Value;
                }

                worksheet.Cell(row, 6).Value =
                    item.LineItem;

                worksheet.Cell(row, 7).Value =
                    item.MaterialNo;

                worksheet.Cell(row, 8).Value =
                    item.HsnSacCode ??
                    string.Empty;

                worksheet.Cell(row, 9).Value =
                    item.Description;

                worksheet.Cell(row, 10).Value =
                    item.Quantity;

                worksheet.Cell(row, 11).Value =
                    item.UnitPrice;

                worksheet.Cell(row, 12).Value =
                    item.PriceUnit;

                worksheet.Cell(row, 13).Value =
                    item.TotalValue;

                worksheet.Cell(row, 14).Value =
                    item.DispatchQuantity;

                worksheet.Cell(row, 15).Value =
                    item.PendingQuantity;

                worksheet.Cell(row, 16).Value =
                    item.DispatchStatus;

                worksheet.Cell(row, 17).Value =
                    item.LiveStockQuantity;

                worksheet.Cell(row, 18).Value =
                    item.StockShortageQuantity;

                worksheet.Cell(row, 19).Value =
                    item.StockStatus;

                worksheet.Cell(row, 20).Value =
                    item.DrawingNo;

                StyleMaterialExcelStatus(
                    worksheet,
                    row,
                    item.StockStatus);
            }

            var title =
                worksheet.Range("A1:T1");

            title.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#31587A");

            title.Style.Font.FontColor =
                XLColor.White;

            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 16;

            title.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Range("A2:T3")
                .Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            var header =
                worksheet.Range("A5:T5");

            header.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#E8F0FA");

            header.Style.Font.FontColor =
                XLColor.FromHtml("#263E66");

            header.Style.Font.Bold = true;

            header.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            if (items.Count > 0)
            {
                worksheet.Range(
                        6,
                        4,
                        5 + items.Count,
                        5)
                    .Style.DateFormat.Format =
                    "dd-mmm-yyyy";

                worksheet.Range(
                        6,
                        10,
                        5 + items.Count,
                        11)
                    .Style.NumberFormat.Format =
                    "0.00";

                worksheet.Range(
                        6,
                        13,
                        5 + items.Count,
                        15)
                    .Style.NumberFormat.Format =
                    "0.00";

                worksheet.Range(
                        6,
                        17,
                        5 + items.Count,
                        18)
                    .Style.NumberFormat.Format =
                    "0.00";

                worksheet.Range(
                        6,
                        1,
                        5 + items.Count,
                        20)
                    .Style.Border.BottomBorder =
                    XLBorderStyleValues.Hair;
            }

            worksheet.SheetView.FreezeRows(5);

            worksheet.Range(
                    5,
                    1,
                    Math.Max(
                        5,
                        5 + items.Count),
                    20)
                .SetAutoFilter();

            worksheet.Columns(1, 20)
                .AdjustToContents();

            worksheet.Column(3).Width = 22;
            worksheet.Column(9).Width = 35;

            worksheet.Column(9)
                .Style.Alignment.WrapText = true;

            worksheet.Column(12).Width = 14;
            worksheet.Column(13).Width = 18;
            worksheet.Column(16).Width = 18;
            worksheet.Column(19).Width = 18;
            worksheet.Column(20).Width = 24;
        }

        private static void StyleMaterialExcelStatus(
            IXLWorksheet worksheet,
            int row,
            string stockStatus)
        {
            var cell =
                worksheet.Cell(
                    row,
                    19);

            if (
                string.Equals(
                    stockStatus,
                    "Available",
                    StringComparison.OrdinalIgnoreCase))
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#DCFCE7");

                cell.Style.Font.FontColor =
                    XLColor.FromHtml("#166534");
            }
            else if (
                stockStatus.Contains(
                    "Shortage",
                    StringComparison.OrdinalIgnoreCase) ||
                stockStatus.Contains(
                    "Partial",
                    StringComparison.OrdinalIgnoreCase))
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#FEF3C7");

                cell.Style.Font.FontColor =
                    XLColor.FromHtml("#92400E");
            }
            else
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#FFE4E6");

                cell.Style.Font.FontColor =
                    XLColor.FromHtml("#9F1239");
            }

            cell.Style.Font.Bold = true;
        }
        // ============================================================
        // QUERY HELPERS
        // ============================================================
        private async Task<MaterialDetailsDto?>
            GetMaterialDetailsAsync(
                int id,
                CancellationToken cancellationToken)
        {
            var details =
                await _context.Materials
                    .AsNoTracking()
                    .Where(material =>
                        material.Id == id)
                    .Select(
                        MaterialDetailsProjection)
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (details == null)
            {
                return null;
            }

            details.DrawingFilePath =
                _documentReferences.ToDownloadUrl(
                    details.DrawingFilePath,
                    "drawings") ??
                string.Empty;

            details.PdfFilePath =
                _documentReferences.ToDownloadUrl(
                    details.PdfFilePath,
                    "pdfs") ??
                string.Empty;

            var stockLoad =
                await _externalStockService
                    .GetAllAsync(
                        false,
                        cancellationToken);

            var stockSnapshot =
                ExternalStockService
                    .BuildSnapshot(
                        stockLoad.Items,
                        details.MaterialNo,
                        details.PendingQty);

            details.LiveStockQuantity =
                stockSnapshot.LiveStock;

            details.StockMinimumQuantity =
                stockSnapshot.MinimumStock;

            details.StockRecordCount =
                stockSnapshot.RecordCount;

            details.StockDataLive =
                stockLoad.IsOnline;

            if (
                !stockLoad.IsOnline &&
                stockLoad.Items.Count == 0)
            {
                details.StockStatus =
                    "Stock API Offline";

                details.AllocatedStockQuantity =
                    0;

                details.StockShortageQuantity =
                    Math.Max(
                        0,
                        details.PendingQty);

                return details;
            }

            var normalizedMaterialNo =
                ExternalStockService.Normalize(
                    details.MaterialNo);

            var candidates =
                await _context.Materials
                    .AsNoTracking()
                    .Where(material =>
                        material.MaterialNo != null &&
                        material.MaterialNo ==
                            details.MaterialNo)
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

            var matchingRows =
                candidates
                    .Where(material =>
                        ExternalStockService
                            .Normalize(
                                material.MaterialNo) ==
                        normalizedMaterialNo)
                    .OrderBy(material =>
                        material.DeliveryDate ??
                        DateTime.MaxValue)
                    .ThenBy(material =>
                        material.Id)
                    .ToList();

            var remainingStock =
                stockSnapshot.LiveStock;

            foreach (var material in matchingRows)
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

                if (material.Id != details.Id)
                {
                    continue;
                }

                details.AllocatedStockQuantity =
                    allocatedQuantity;

                details.StockShortageQuantity =
                    shortageQuantity;

                if (stockSnapshot.RecordCount == 0)
                {
                    details.StockStatus =
                        "Not Found";
                }
                else if (requiredQuantity <= 0)
                {
                    details.StockStatus =
                        "No Pending";
                }
                else if (
                    allocatedQuantity >=
                    requiredQuantity)
                {
                    details.StockStatus =
                        "Available";
                }
                else if (allocatedQuantity > 0)
                {
                    details.StockStatus =
                        "Partial Stock";
                }
                else if (
                    stockSnapshot.LiveStock <= 0)
                {
                    details.StockStatus =
                        "Out of Stock";
                }
                else
                {
                    details.StockStatus =
                        "Shortage";
                }

                break;
            }

            if (
                !stockLoad.IsOnline &&
                stockLoad.Items.Count == 0)
            {
                details.StockStatus =
                    "Stock API Offline";

                details.AllocatedStockQuantity =
                    0;

                details.StockShortageQuantity =
                    Math.Max(
                        0,
                        details.PendingQty);
            }

            return details;
        }
        private static IOrderedQueryable<Material>
            ApplyMaterialSort(
                IQueryable<Material> query,
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
                        .OrderByDescending(
                            material =>
                                material.PurchaseOrder != null
                                    ? material.PurchaseOrder.PONumber
                                    : string.Empty)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "po" =>
                    query
                        .OrderBy(
                            material =>
                                material.PurchaseOrder != null
                                    ? material.PurchaseOrder.PONumber
                                    : string.Empty)
                        .ThenBy(
                            material =>
                                material.Id),

                "line" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.LineItem ??
                                string.Empty)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "line" =>
                    query
                        .OrderBy(
                            material =>
                                material.LineItem ??
                                string.Empty)
                        .ThenBy(
                            material =>
                                material.Id),

                "material" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.MaterialNo ??
                                string.Empty)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "material" =>
                    query
                        .OrderBy(
                            material =>
                                material.MaterialNo ??
                                string.Empty)
                        .ThenBy(
                            material =>
                                material.Id),

                "description" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.ShortDescription != null &&
                                material.ShortDescription != string.Empty
                                    ? material.ShortDescription
                                    : material.BriefDescription ??
                                      string.Empty)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "description" =>
                    query
                        .OrderBy(
                            material =>
                                material.ShortDescription != null &&
                                material.ShortDescription != string.Empty
                                    ? material.ShortDescription
                                    : material.BriefDescription ??
                                      string.Empty)
                        .ThenBy(
                            material =>
                                material.Id),

                "ordered" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.Qty)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "ordered" =>
                    query
                        .OrderBy(
                            material =>
                                material.Qty)
                        .ThenBy(
                            material =>
                                material.Id),

                "unitprice" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.Cost)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "unitprice" =>
                    query
                        .OrderBy(
                            material =>
                                material.Cost)
                        .ThenBy(
                            material =>
                                material.Id),

                "materialvalue" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.TotalValue)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "materialvalue" =>
                    query
                        .OrderBy(
                            material =>
                                material.TotalValue)
                        .ThenBy(
                            material =>
                                material.Id),

                "dispatched" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.DispatchQty)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "dispatched" =>
                    query
                        .OrderBy(
                            material =>
                                material.DispatchQty)
                        .ThenBy(
                            material =>
                                material.Id),

                "pending" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.PendingQty)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "pending" =>
                    query
                        .OrderBy(
                            material =>
                                material.PendingQty)
                        .ThenBy(
                            material =>
                                material.Id),

                "process" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.ProcessStatus)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "process" =>
                    query
                        .OrderBy(
                            material =>
                                material.ProcessStatus)
                        .ThenBy(
                            material =>
                                material.Id),

                "dispatch" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.DispatchStatus)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "dispatch" =>
                    query
                        .OrderBy(
                            material =>
                                material.DispatchStatus)
                        .ThenBy(
                            material =>
                                material.Id),

                "delivery" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.DeliveryDate.HasValue)
                        .ThenByDescending(
                            material =>
                                material.DeliveryDate)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "delivery" =>
                    query
                        .OrderByDescending(
                            material =>
                                material.DeliveryDate.HasValue)
                        .ThenBy(
                            material =>
                                material.DeliveryDate)
                        .ThenBy(
                            material =>
                                material.Id),

                _ =>
                    query
                        .OrderByDescending(
                            material =>
                                material.Id)
            };
        }


        private static List<MaterialListItemDto>
            ApplyMaterialDtoSort(
                IEnumerable<MaterialListItemDto> items,
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
                    items
                        .OrderByDescending(
                            item =>
                                item.PONumber,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "po" =>
                    items
                        .OrderBy(
                            item =>
                                item.PONumber,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "line" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.LineItem,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "line" =>
                    items
                        .OrderBy(
                            item =>
                                item.LineItem,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "material" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.MaterialNo,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "material" =>
                    items
                        .OrderBy(
                            item =>
                                item.MaterialNo,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "description" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.Description,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "description" =>
                    items
                        .OrderBy(
                            item =>
                                item.Description,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "ordered" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.Quantity)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "ordered" =>
                    items
                        .OrderBy(
                            item =>
                                item.Quantity)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "unitprice" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.UnitPrice)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "unitprice" =>
                    items
                        .OrderBy(
                            item =>
                                item.UnitPrice)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "materialvalue" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.TotalValue)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "materialvalue" =>
                    items
                        .OrderBy(
                            item =>
                                item.TotalValue)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "dispatched" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.DispatchQuantity)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "dispatched" =>
                    items
                        .OrderBy(
                            item =>
                                item.DispatchQuantity)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "pending" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.PendingQuantity)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "pending" =>
                    items
                        .OrderBy(
                            item =>
                                item.PendingQuantity)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "allocatedlive" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.AllocatedStockQuantity)
                        .ThenByDescending(
                            item =>
                                item.LiveStockQuantity)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "allocatedlive" =>
                    items
                        .OrderBy(
                            item =>
                                item.AllocatedStockQuantity)
                        .ThenBy(
                            item =>
                                item.LiveStockQuantity)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "shortage" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.StockShortageQuantity)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "shortage" =>
                    items
                        .OrderBy(
                            item =>
                                item.StockShortageQuantity)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "stock" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.StockStatus,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "stock" =>
                    items
                        .OrderBy(
                            item =>
                                item.StockStatus,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "process" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.ProcessStatus,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "process" =>
                    items
                        .OrderBy(
                            item =>
                                item.ProcessStatus,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "dispatch" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.DispatchStatus,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "dispatch" =>
                    items
                        .OrderBy(
                            item =>
                                item.DispatchStatus,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                "delivery" when descending =>
                    items
                        .OrderByDescending(
                            item =>
                                item.DeliveryDate.HasValue)
                        .ThenByDescending(
                            item =>
                                item.DeliveryDate)
                        .ThenByDescending(
                            item =>
                                item.Id)
                        .ToList(),

                "delivery" =>
                    items
                        .OrderByDescending(
                            item =>
                                item.DeliveryDate.HasValue)
                        .ThenBy(
                            item =>
                                item.DeliveryDate)
                        .ThenBy(
                            item =>
                                item.Id)
                        .ToList(),

                _ =>
                    items
                        .OrderByDescending(
                            item =>
                                item.Id)
                        .ToList()
            };
        }


        private static bool
            RequiresMaterialExcelDerivedSort(
                string? sortColumn)
        {
            var normalized =
                sortColumn?
                    .Trim()
                    .ToLowerInvariant() ??
                string.Empty;

            return
                normalized ==
                    "allocatedlive" ||
                normalized ==
                    "shortage" ||
                normalized ==
                    "stock";
        }

        private static Dictionary<
            string,
            HashSet<string>>
            ParseMaterialExcelFilters(
                string? excelFilters)
        {
            var result =
                new Dictionary<
                    string,
                    HashSet<string>>(
                    StringComparer.OrdinalIgnoreCase);

            if (
                string.IsNullOrWhiteSpace(
                    excelFilters)
            )
            {
                return result;
            }

            try
            {
                var parsed =
                    JsonSerializer
                        .Deserialize<
                            Dictionary<
                                string,
                                string[]>>(
                            excelFilters);

                if (parsed == null)
                {
                    return result;
                }

                foreach (
                    var pair in parsed)
                {
                    var column =
                        pair.Key?
                            .Trim()
                            .ToLowerInvariant();

                    if (
                        string.IsNullOrWhiteSpace(
                            column) ||
                        !MaterialExcelFilterColumns
                            .Contains(
                                column)
                    )
                    {
                        continue;
                    }

                    result[column] =
                        new HashSet<string>(
                            pair.Value ??
                                Array.Empty<string>(),
                            StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (JsonException)
            {
                // Invalid Excel-filter JSON is ignored so legacy
                // list filtering continues to work unchanged.
            }

            return result;
        }

        private static bool
            MatchesMaterialExcelFilters(
                MaterialListItemDto item,
                IReadOnlyDictionary<
                    string,
                    HashSet<string>>
                    selections)
        {
            foreach (
                var selection in
                selections)
            {
                if (
                    selection.Value.Count ==
                    0
                )
                {
                    return false;
                }

                var cellValue =
                    GetMaterialExcelCellValue(
                        item,
                        selection.Key);

                if (
                    !selection.Value
                        .Contains(
                            cellValue)
                )
                {
                    return false;
                }
            }

            return true;
        }

        private static string
            GetMaterialExcelCellValue(
                MaterialListItemDto item,
                string column)
        {
            static string FormatDecimal(
                decimal value) =>
                    value.ToString(
                        "0.####",
                        CultureInfo.InvariantCulture);

            static string JoinValues(
                params string?[] values) =>
                    string.Join(
                        " | ",
                        values
                            .Where(value =>
                                !string.IsNullOrWhiteSpace(
                                    value))
                            .Select(value =>
                                value!.Trim()));

            return
                column
                    .Trim()
                    .ToLowerInvariant()
                switch
                {
                    "po" =>
                        JoinValues(
                            item.PONumber,
                            item.PurchaserName,
                            item.Handler),

                    "line" =>
                        item.LineItem ??
                        string.Empty,

                    "material" =>
                        JoinValues(
                            item.MaterialNo,
                            item.HsnSacCode),

                    "description" =>
                        item.Description ??
                        string.Empty,

                    "ordered" =>
                        FormatDecimal(
                            item.Quantity),

                    "unitprice" =>
                        FormatDecimal(
                            item.UnitPrice),

                    "materialvalue" =>
                        FormatDecimal(
                            item.TotalValue),

                    "dispatched" =>
                        FormatDecimal(
                            item.DispatchQuantity),

                    "pending" =>
                        FormatDecimal(
                            item.PendingQuantity),

                    "allocatedlive" =>
                        string.Concat(
                            FormatDecimal(
                                item.AllocatedStockQuantity),
                            " / ",
                            FormatDecimal(
                                item.LiveStockQuantity)),

                    "shortage" =>
                        FormatDecimal(
                            item.StockShortageQuantity),

                    "stock" =>
                        item.StockStatus ??
                        string.Empty,

                    "process" =>
                        item.ProcessStatus ??
                        string.Empty,

                    "dispatch" =>
                        item.DispatchStatus ??
                        string.Empty,

                    "delivery" =>
                        item.DeliveryDate
                            .HasValue
                            ? item.DeliveryDate
                                .Value
                                .ToString(
                                    "yyyy-MM-dd",
                                    CultureInfo.InvariantCulture)
                            : string.Empty,

                    _ =>
                        string.Empty
                };
        }


        private static IQueryable<Material>
            ApplyFilters(
                IQueryable<Material> query,
                string? search,
                string? poNumber,
                string? handler,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo,
                string? invoiceStatus,
                string? dispatchStatus,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                string? columnPoNumber,
                string? columnPurchaser,
                string? columnHandler,
                string? lineItem,
                string? materialNo,
                string? description,
                decimal? minOrderedQty,
                decimal? maxOrderedQty,
                decimal? minUnitPrice,
                decimal? maxUnitPrice,
                decimal? minMaterialValue,
                decimal? maxMaterialValue,
                decimal? minDispatchQty,
                decimal? maxDispatchQty,
                decimal? minPendingQty,
                decimal? maxPendingQty,
                string? processStatus,
                string? columnDispatchStatus,
                DateTime? columnDeliveryDateFrom,
                DateTime? columnDeliveryDateTo)
        {
            if (
                !string.IsNullOrWhiteSpace(
                    search))
            {
                var value =
                    search.Trim();

                query =
                    query.Where(
                        material =>
                            (material.MaterialNo != null &&
                             material.MaterialNo
                                 .Contains(value)) ||
                            (material.LineItem != null &&
                             material.LineItem
                                 .Contains(value)) ||
                            (material.HsnSacCode != null &&
                             material.HsnSacCode
                                 .Contains(value)) ||
                            (material.ShortDescription != null &&
                             material.ShortDescription
                                 .Contains(value)) ||
                            (material.BriefDescription != null &&
                             material.BriefDescription
                                 .Contains(value)) ||
                            (material.DrawingNo != null &&
                             material.DrawingNo
                                 .Contains(value)) ||
                            (material.PurchaseOrder != null &&
                             material.PurchaseOrder
                                 .PONumber
                                 .Contains(value)) ||
                            (material.PurchaseOrder != null &&
                             material.PurchaseOrder.Purchaser != null &&
                             material.PurchaseOrder.Purchaser
                                 .PurchaserName
                                 .Contains(value)));
            }

            if (
                !string.IsNullOrWhiteSpace(
                    poNumber))
            {
                var value =
                    poNumber.Trim();

                query =
                    query.Where(
                        material =>
                            material.PurchaseOrder != null &&
                            material.PurchaseOrder
                                .PONumber
                                .Contains(value));
            }

            if (
                !string.IsNullOrWhiteSpace(
                    handler))
            {
                var value =
                    handler.Trim();

                query =
                    query.Where(
                        material =>
                            material.PurchaseOrder != null &&
                            material.PurchaseOrder
                                .BuyerContactPerson != null &&
                            material.PurchaseOrder
                                .BuyerContactPerson
                                .Contains(value));
            }

            if (poDateFrom.HasValue)
            {
                var startDate =
                    poDateFrom.Value.Date;

                query =
                    query.Where(
                        material =>
                            (
                                material.PurchaseOrder != null &&
                                material.PurchaseOrder
                                    .PODate
                                    .HasValue &&
                                material.PurchaseOrder
                                    .PODate
                                    .Value >=
                                startDate
                            ) ||
                            (
                                (
                                    material.PurchaseOrder == null ||
                                    !material.PurchaseOrder
                                        .PODate
                                        .HasValue
                                ) &&
                                material.PODate.HasValue &&
                                material.PODate.Value >=
                                startDate
                            ));
            }

            if (poDateTo.HasValue)
            {
                var endDateExclusive =
                    poDateTo.Value
                        .Date
                        .AddDays(1);

                query =
                    query.Where(
                        material =>
                            (
                                material.PurchaseOrder != null &&
                                material.PurchaseOrder
                                    .PODate
                                    .HasValue &&
                                material.PurchaseOrder
                                    .PODate
                                    .Value <
                                endDateExclusive
                            ) ||
                            (
                                (
                                    material.PurchaseOrder == null ||
                                    !material.PurchaseOrder
                                        .PODate
                                        .HasValue
                                ) &&
                                material.PODate.HasValue &&
                                material.PODate.Value <
                                endDateExclusive
                            ));
            }

            if (
                poReceivedDateFrom.HasValue)
            {
                var startDate =
                    poReceivedDateFrom
                        .Value
                        .Date;

                query =
                    query.Where(
                        material =>
                            (
                                material.PurchaseOrder != null &&
                                material.PurchaseOrder
                                    .ReceivedDate
                                    .HasValue &&
                                material.PurchaseOrder
                                    .ReceivedDate
                                    .Value >=
                                startDate
                            ) ||
                            (
                                (
                                    material.PurchaseOrder == null ||
                                    !material.PurchaseOrder
                                        .ReceivedDate
                                        .HasValue
                                ) &&
                                material.ReceivingDate
                                    .HasValue &&
                                material.ReceivingDate
                                    .Value >=
                                startDate
                            ));
            }

            if (
                poReceivedDateTo.HasValue)
            {
                var endDateExclusive =
                    poReceivedDateTo
                        .Value
                        .Date
                        .AddDays(1);

                query =
                    query.Where(
                        material =>
                            (
                                material.PurchaseOrder != null &&
                                material.PurchaseOrder
                                    .ReceivedDate
                                    .HasValue &&
                                material.PurchaseOrder
                                    .ReceivedDate
                                    .Value <
                                endDateExclusive
                            ) ||
                            (
                                (
                                    material.PurchaseOrder == null ||
                                    !material.PurchaseOrder
                                        .ReceivedDate
                                        .HasValue
                                ) &&
                                material.ReceivingDate
                                    .HasValue &&
                                material.ReceivingDate
                                    .Value <
                                endDateExclusive
                            ));
            }

            if (
                !string.IsNullOrWhiteSpace(
                    invoiceStatus))
            {
                query =
                    query.Where(
                        material =>
                            material.InvoiceStatus ==
                            invoiceStatus);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    dispatchStatus))
            {
                query =
                    query.Where(
                        material =>
                            material.DispatchStatus ==
                            dispatchStatus);
            }

            if (deliveryDateFrom.HasValue)
            {
                var startDate =
                    deliveryDateFrom
                        .Value
                        .Date;

                query =
                    query.Where(
                        material =>
                            material.DeliveryDate
                                .HasValue &&
                            material.DeliveryDate
                                .Value >=
                            startDate);
            }

            if (deliveryDateTo.HasValue)
            {
                var endDateExclusive =
                    deliveryDateTo
                        .Value
                        .Date
                        .AddDays(1);

                query =
                    query.Where(
                        material =>
                            material.DeliveryDate
                                .HasValue &&
                            material.DeliveryDate
                                .Value <
                            endDateExclusive);
            }

            var normalizedColumnPoNumber =
                Clean(
                    columnPoNumber);

            if (normalizedColumnPoNumber != null)
            {
                query =
                    query.Where(material =>
                        material.PurchaseOrder != null &&
                        material.PurchaseOrder
                            .PONumber
                            .Contains(
                                normalizedColumnPoNumber));
            }

            var normalizedColumnPurchaser =
                Clean(
                    columnPurchaser);

            if (normalizedColumnPurchaser != null)
            {
                query =
                    query.Where(material =>
                        material.PurchaseOrder != null &&
                        material.PurchaseOrder.Purchaser != null &&
                        material.PurchaseOrder.Purchaser
                            .PurchaserName
                            .Contains(
                                normalizedColumnPurchaser));
            }

            var normalizedColumnHandler =
                Clean(
                    columnHandler);

            if (normalizedColumnHandler != null)
            {
                query =
                    query.Where(material =>
                        material.PurchaseOrder != null &&
                        material.PurchaseOrder
                            .BuyerContactPerson != null &&
                        material.PurchaseOrder
                            .BuyerContactPerson
                            .Contains(
                                normalizedColumnHandler));
            }

            var normalizedLineItem =
                Clean(
                    lineItem);

            if (normalizedLineItem != null)
            {
                query =
                    query.Where(material =>
                        material.LineItem != null &&
                        material.LineItem.Contains(
                            normalizedLineItem));
            }

            var normalizedMaterialNo =
                Clean(
                    materialNo);

            if (normalizedMaterialNo != null)
            {
                query =
                    query.Where(material =>
                        (
                            material.MaterialNo != null &&
                            material.MaterialNo.Contains(
                                normalizedMaterialNo)
                        ) ||
                        (
                            material.HsnSacCode != null &&
                            material.HsnSacCode.Contains(
                                normalizedMaterialNo)
                        ));
            }

            var normalizedDescription =
                Clean(
                    description);

            if (normalizedDescription != null)
            {
                query =
                    query.Where(material =>
                        (
                            material.ShortDescription != null &&
                            material.ShortDescription.Contains(
                                normalizedDescription)
                        ) ||
                        (
                            material.BriefDescription != null &&
                            material.BriefDescription.Contains(
                                normalizedDescription)
                        ));
            }

            if (minOrderedQty.HasValue)
            {
                query =
                    query.Where(material =>
                        material.Qty >=
                        minOrderedQty.Value);
            }

            if (maxOrderedQty.HasValue)
            {
                query =
                    query.Where(material =>
                        material.Qty <=
                        maxOrderedQty.Value);
            }

            if (minUnitPrice.HasValue)
            {
                query =
                    query.Where(material =>
                        material.Cost >=
                        minUnitPrice.Value);
            }

            if (maxUnitPrice.HasValue)
            {
                query =
                    query.Where(material =>
                        material.Cost <=
                        maxUnitPrice.Value);
            }

            if (minMaterialValue.HasValue)
            {
                query =
                    query.Where(material =>
                        material.TotalValue >=
                        minMaterialValue.Value);
            }

            if (maxMaterialValue.HasValue)
            {
                query =
                    query.Where(material =>
                        material.TotalValue <=
                        maxMaterialValue.Value);
            }

            if (minDispatchQty.HasValue)
            {
                query =
                    query.Where(material =>
                        material.DispatchQty >=
                        minDispatchQty.Value);
            }

            if (maxDispatchQty.HasValue)
            {
                query =
                    query.Where(material =>
                        material.DispatchQty <=
                        maxDispatchQty.Value);
            }

            if (minPendingQty.HasValue)
            {
                query =
                    query.Where(material =>
                        material.PendingQty >=
                        minPendingQty.Value);
            }

            if (maxPendingQty.HasValue)
            {
                query =
                    query.Where(material =>
                        material.PendingQty <=
                        maxPendingQty.Value);
            }

            var normalizedProcessStatus =
                Clean(
                    processStatus);

            if (normalizedProcessStatus != null)
            {
                query =
                    query.Where(material =>
                        material.ProcessStatus.Contains(
                            normalizedProcessStatus));
            }

            var normalizedColumnDispatchStatus =
                Clean(
                    columnDispatchStatus);

            if (normalizedColumnDispatchStatus != null)
            {
                query =
                    query.Where(material =>
                        material.DispatchStatus.Contains(
                            normalizedColumnDispatchStatus));
            }

            if (columnDeliveryDateFrom.HasValue)
            {
                var columnDeliveryStart =
                    columnDeliveryDateFrom.Value.Date;

                query =
                    query.Where(material =>
                        material.DeliveryDate.HasValue &&
                        material.DeliveryDate.Value >=
                        columnDeliveryStart);
            }

            if (columnDeliveryDateTo.HasValue)
            {
                var columnDeliveryEndExclusive =
                    columnDeliveryDateTo
                        .Value
                        .Date
                        .AddDays(1);

                query =
                    query.Where(material =>
                        material.DeliveryDate.HasValue &&
                        material.DeliveryDate.Value <
                        columnDeliveryEndExclusive);
            }

            return query;
        }

        private static bool MatchesStockDerivedFilters(
            MaterialListItemDto item,
            string? topStockStatus,
            string? columnStockStatus,
            decimal? minAllocatedQty,
            decimal? maxAllocatedQty,
            decimal? minLiveStockQty,
            decimal? maxLiveStockQty,
            decimal? minShortageQty,
            decimal? maxShortageQty)
        {
            if (
                !string.IsNullOrWhiteSpace(
                    topStockStatus) &&
                !MatchesStockStatus(
                    item.StockStatus,
                    topStockStatus.Trim()))
            {
                return false;
            }

            if (
                !string.IsNullOrWhiteSpace(
                    columnStockStatus) &&
                !MatchesStockStatus(
                    item.StockStatus,
                    columnStockStatus.Trim()))
            {
                return false;
            }

            if (
                minAllocatedQty.HasValue &&
                item.AllocatedStockQuantity <
                    minAllocatedQty.Value)
            {
                return false;
            }

            if (
                maxAllocatedQty.HasValue &&
                item.AllocatedStockQuantity >
                    maxAllocatedQty.Value)
            {
                return false;
            }

            if (
                minLiveStockQty.HasValue &&
                item.LiveStockQuantity <
                    minLiveStockQty.Value)
            {
                return false;
            }

            if (
                maxLiveStockQty.HasValue &&
                item.LiveStockQuantity >
                    maxLiveStockQty.Value)
            {
                return false;
            }

            if (
                minShortageQty.HasValue &&
                item.StockShortageQuantity <
                    minShortageQty.Value)
            {
                return false;
            }

            if (
                maxShortageQty.HasValue &&
                item.StockShortageQuantity >
                    maxShortageQty.Value)
            {
                return false;
            }

            return true;
        }

        private static bool MatchesStockStatus(
            string actualStatus,
            string requestedStatus)
        {
            if (
                requestedStatus.Equals(
                    "Available",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    actualStatus.Equals(
                        "Available",
                        StringComparison.OrdinalIgnoreCase) ||
                    actualStatus.Equals(
                        "No Pending",
                        StringComparison.OrdinalIgnoreCase);
            }

            if (
                requestedStatus.Equals(
                    "Shortage",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    actualStatus.Equals(
                        "Shortage",
                        StringComparison.OrdinalIgnoreCase) ||
                    actualStatus.Equals(
                        "Partial Stock",
                        StringComparison.OrdinalIgnoreCase);
            }

            if (
                requestedStatus.Equals(
                    "Not Found",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    actualStatus.Equals(
                        "Not Found",
                        StringComparison.OrdinalIgnoreCase) ||
                    actualStatus.Equals(
                        "Stock API Offline",
                        StringComparison.OrdinalIgnoreCase);
            }

            return
                actualStatus.Equals(
                    requestedStatus,
                    StringComparison.OrdinalIgnoreCase);
        }
        private static int NormalizePageSize(
            int pageSize)
        {
            if (pageSize < 1)
            {
                return DefaultPageSize;
            }

            return Math.Min(
                pageSize,
                MaximumPageSize);
        }

        // ============================================================
        // MAPPING AND BUSINESS RULES
        // ============================================================

        private static void ApplyEditableValues(
            Material material,
            MaterialUpsertRequestDto request)
        {
            material.PurchaseOrderId =
                request.PurchaseOrderId;

            material.LineItem =
                Clean(request.LineItem);

            material.ItemType =
                Clean(request.ItemType);

            material.MaterialNo =
                Clean(request.MaterialNo);

            material.HsnSacCode =
                Clean(request.HsnSacCode);

            material.ShortDescription =
                Clean(request.ShortDescription);

            material.BriefDescription =
                Clean(request.BriefDescription);

            material.Receiver =
                Clean(request.Receiver);

            material.UnloadingPoint =
                Clean(request.UnloadingPoint);

            material.Qty =
                request.Qty;

            material.Unit =
                Clean(request.Unit);

            material.Dep =
                Clean(request.Dep);
            material.Cost =
                request.Cost;

            material.ValueOfGoods =
                request.ValueOfGoods;

            material.PriceUnit =
                Clean(request.PriceUnit);

            material.TotalValue =
                request.TotalValue;

            material.RMGrade =
                Clean(request.RMGrade);

            material.RMSource =
                Clean(request.RMSource);

            material.RMProcess =
                Clean(request.RMProcess);

            material.SurfaceFinish =
                Clean(request.SurfaceFinish);

            material.MachiningType =
                Clean(request.MachiningType);

            material.ProcessTime =
                Clean(request.ProcessTime);

            material.MaterialProcessTime =
                Clean(
                    request.MaterialProcessTime);

            material.PODate =
                request.PODate;

            material.ReceivingDate =
                request.ReceivingDate;

            material.DeliveryDate =
                request.DeliveryDate;

            material.DispatchDate =
                request.DispatchDate;

            material.DrawingUpdatedDate =
                request.DrawingUpdatedDate;

            material.DrawingNo =
                Clean(request.DrawingNo);

            material.DrawingVersion =
                Clean(request.DrawingVersion);

            material.ProcessStatus =
                string.IsNullOrWhiteSpace(
                    request.ProcessStatus)
                    ? "Pending"
                    : request.ProcessStatus.Trim();

            material.AssignedDashboard =
                Clean(
                    request.AssignedDashboard);

            material.Remark =
                Clean(request.Remark);
        }

        private static void ApplyScheduleRules(
            Material material)
        {
            if (material.DeliveryDate.HasValue)
            {
                material.ProcessStartDate =
                    material.DeliveryDate.Value
                        .AddMonths(-1);

                material.AlertDate =
                    material.ProcessStartDate;
            }
            else
            {
                material.ProcessStartDate = null;
                material.AlertDate = null;
            }

            material.PendingQty =
                material.Qty -
                material.DispatchQty;

            if (material.PendingQty < 0)
            {
                material.PendingQty = 0;
            }

            if (material.DispatchQty <= 0)
            {
                material.DispatchStatus =
                    "Pending";
            }
            else if (
                material.PendingQty <= 0 ||
                material.DispatchQty >=
                material.Qty)
            {
                material.DispatchStatus =
                    "Dispatched";
            }
            else
            {
                material.DispatchStatus =
                    "Partial";
            }

            material.BalanceQty =
                material.Qty -
                material.InvoiceQty;

            if (material.BalanceQty < 0)
            {
                material.BalanceQty = 0;
            }

            if (material.InvoiceQty <= 0)
            {
                material.InvoiceStatus =
                    "Not Invoiced";
            }
            else if (
                material.BalanceQty <= 0 ||
                material.InvoiceQty >=
                material.Qty)
            {
                material.InvoiceStatus =
                    "Fully Invoiced";
            }
            else
            {
                material.InvoiceStatus =
                    "Partially Invoiced";
            }
        }

        private async Task
            CreateOrUpdateProcessAlertAsync(
                Material material,
                CancellationToken cancellationToken)
        {
            var existingAlert =
                await _context.Alerts
                    .FirstOrDefaultAsync(
                        alert =>
                            alert.MaterialId ==
                                material.Id &&
                            alert.AlertType ==
                                "Process Start",
                        cancellationToken);

            if (material.AlertDate.HasValue)
            {
                var message =
                    $"Start process for {material.MaterialNo} before delivery date {material.DeliveryDate?.ToString("dd-MM-yyyy")}";

                if (existingAlert == null)
                {
                    _context.Alerts.Add(
                        new Alert
                        {
                            MaterialId =
                                material.Id,

                            AlertType =
                                "Process Start",

                            AlertDate =
                                material.AlertDate.Value,

                            Message =
                                message
                        });
                }
                else
                {
                    existingAlert.AlertDate =
                        material.AlertDate.Value;

                    existingAlert.Message =
                        message;
                }

                return;
            }

            if (existingAlert != null)
            {
                _context.Alerts.Remove(
                    existingAlert);
            }
        }

        private static string? Clean(
            string? value)
        {
            var cleaned = value?.Trim();

            return string.IsNullOrWhiteSpace(
                cleaned)
                ? null
                : cleaned;
        }

        // ============================================================
        // FILE HELPERS
        // ============================================================

        private static string? ValidatePdfFile(
            IFormFile? file)
        {
            if (file == null ||
                file.Length == 0)
            {
                return null;
            }

            if (file.Length >
                MaximumPdfFileSize)
            {
                return
                    "PDF file size must not exceed 50 MB.";
            }

            var extension =
                Path.GetExtension(
                    file.FileName);

            if (!extension.Equals(
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    "Only PDF files are allowed for the material PDF.";
            }

            return null;
        }

        private static string? ValidateDrawingFile(
            IFormFile? file)
        {
            if (file == null ||
                file.Length == 0)
            {
                return null;
            }

            if (file.Length >
                MaximumDrawingFileSize)
            {
                return
                    "Drawing file size must not exceed 100 MB.";
            }

            var extension =
                Path.GetExtension(
                    file.FileName);

            if (!AllowedDrawingExtensions.Contains(
                    extension))
            {
                return
                    "Allowed drawing files: PDF, DWG, DXF, STEP, STP, IGES, IGS or ZIP.";
            }

            return null;
        }

        private async Task<string> SaveFileAsync(
            IFormFile file,
            string folderName,
            CancellationToken cancellationToken)
        {
            var webRootPath =
                _environment.WebRootPath ??
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot");

            var uploadFolder =
                Path.Combine(
                    webRootPath,
                    "uploads",
                    folderName);

            Directory.CreateDirectory(
                uploadFolder);

            var extension =
                Path.GetExtension(
                    file.FileName)
                    .ToLowerInvariant();

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var publicPhysicalPath =
                Path.Combine(
                    uploadFolder,
                    fileName);

            StoredPrivateDocument privateDocument;

            try
            {
                await using (
                    var publicStream =
                        new FileStream(
                            publicPhysicalPath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            bufferSize: 81920,
                            useAsync: true))
                {
                    await file.CopyToAsync(
                        publicStream,
                        cancellationToken);
                }

                await using (
                    var privateSource =
                        new FileStream(
                            publicPhysicalPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            bufferSize: 81920,
                            useAsync: true))
                {
                    privateDocument =
                        await _privateDocumentStorage
                            .SaveWithStoredFileNameAsync(
                                privateSource,
                                folderName,
                                fileName,
                                cancellationToken);
                }
            }
            catch
            {
                if (System.IO.File.Exists(
                        publicPhysicalPath))
                {
                    System.IO.File.Delete(
                        publicPhysicalPath);
                }

                throw;
            }

            var secureFilePath =
                _documentReferences.ToDownloadUrl(
                    privateDocument.RelativePath,
                    folderName);

            if (secureFilePath == null)
            {
                try
                {
                    _privateDocumentStorage.DeleteIfExists(
                        privateDocument.RelativePath);
                }
                catch
                {
                    // Preserve the secure URL-generation failure.
                }

                if (System.IO.File.Exists(
                        publicPhysicalPath))
                {
                    System.IO.File.Delete(
                        publicPhysicalPath);
                }

                throw new InvalidOperationException(
                    "Unable to create a secure material document URL.");
            }

            return secureFilePath;
        }
    }
}
