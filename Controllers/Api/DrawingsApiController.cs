using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Services;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/drawings")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class DrawingsApiController :
        ControllerBase
    {
        private static readonly HashSet<string>
            DrawingExcelFilterColumns =
                new(
                    new[]
                    {
                        "material",
                        "po",
                        "version",
                        "updated",
                        "drawing",
                        "pdf",
                        "status"
                    },
                    StringComparer.OrdinalIgnoreCase);

        private readonly ApplicationDbContext _context;

        private readonly PrivateDocumentReferenceService
            _documentReferences;

        public DrawingsApiController(
            ApplicationDbContext context,
            PrivateDocumentReferenceService
                documentReferences)
        {
            _context = context;
            _documentReferences =
                documentReferences;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(DrawingListResponseDto),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<DrawingListResponseDto>>
            Index(
                string? search,
                string? status,
                string? material,
                string? poNumber,
                string? drawingVersion,
                DateTime? updatedDateFrom,
                DateTime? updatedDateTo,
                string? drawingAvailability,
                string? pdfAvailability,
                string? columnStatus,
                string? sortColumn,
                string? sortDirection,
                string? excelFilters,
                int page = 1,
                int pageSize = 25,
                CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);

            pageSize =
                pageSize is 10 or 25 or 50 or 100
                    ? pageSize
                    : 25;

            var rows =
                await LoadDrawingRows(
                    BuildDrawingQuery(
                        search,
                        status,
                        material,
                        poNumber,
                        drawingVersion,
                        updatedDateFrom,
                        updatedDateTo,
                        drawingAvailability,
                        pdfAvailability,
                        columnStatus),
                    cancellationToken);

            var selections =
                ParseDrawingExcelFilters(
                    excelFilters);

            var filteredRows =
                rows
                    .Where(row =>
                        MatchesDrawingExcelFilters(
                            row,
                            selections))
                    .ToList();

            var weekStart =
                DateTime.Today.AddDays(-7);

            var summary =
                new DrawingSummaryDto
                {
                    TotalMaterials =
                        filteredRows.Count,

                    MissingDrawings =
                        filteredRows.Count(row =>
                            !row.HasDrawing),

                    UpdatedThisWeek =
                        filteredRows.Count(row =>
                            row.DrawingUpdatedDate.HasValue &&
                            row.DrawingUpdatedDate.Value >=
                                weekStart),

                    WithVersion =
                        filteredRows.Count(row =>
                            !string.IsNullOrWhiteSpace(
                                row.DrawingVersion))
                };

            var totalRecords =
                filteredRows.Count;

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

            var items =
                ApplyDrawingSort(
                        filteredRows,
                        sortColumn,
                        sortDirection)
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(pageSize)
                    .ToList();

            ApplySecureDocumentUrls(
                items);

            return Ok(
                new DrawingListResponseDto
                {
                    Summary = summary,
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalRecords = totalRecords,
                    TotalPages = totalPages
                });
        }

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
            ExcelFilterValues(
                string column,
                string? search,
                string? status,
                string? material,
                string? poNumber,
                string? drawingVersion,
                DateTime? updatedDateFrom,
                DateTime? updatedDateTo,
                string? drawingAvailability,
                string? pdfAvailability,
                string? columnStatus,
                string? excelFilters,
                CancellationToken cancellationToken = default)
        {
            var normalizedColumn =
                NormalizeDrawingExcelColumn(
                    column);

            if (
                !DrawingExcelFilterColumns.Contains(
                    normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Unsupported Drawings Excel filter column."
                    });
            }

            var rows =
                await LoadDrawingRows(
                    BuildDrawingQuery(
                        search,
                        status,
                        material,
                        poNumber,
                        drawingVersion,
                        updatedDateFrom,
                        updatedDateTo,
                        drawingAvailability,
                        pdfAvailability,
                        columnStatus),
                    cancellationToken);

            var selections =
                ParseDrawingExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var values =
                rows
                    .Where(row =>
                        MatchesDrawingExcelFilters(
                            row,
                            selections))
                    .Select(row =>
                        GetDrawingExcelCellValue(
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

        private IQueryable<POTracker.Models.Material>
            BuildDrawingQuery(
                string? search,
                string? status,
                string? material,
                string? poNumber,
                string? drawingVersion,
                DateTime? updatedDateFrom,
                DateTime? updatedDateTo,
                string? drawingAvailability,
                string? pdfAvailability,
                string? columnStatus)
        {
            var query =
                _context.Materials
                    .AsNoTracking()
                    .AsQueryable();

            var normalizedSearch =
                Clean(search);

            if (normalizedSearch != null)
            {
                query =
                    query.Where(item =>
                        (
                            item.MaterialNo != null &&
                            item.MaterialNo.Contains(
                                normalizedSearch)
                        ) ||
                        (
                            item.ShortDescription != null &&
                            item.ShortDescription.Contains(
                                normalizedSearch)
                        ) ||
                        (
                            item.DrawingVersion != null &&
                            item.DrawingVersion.Contains(
                                normalizedSearch)
                        ) ||
                        (
                            item.PurchaseOrder != null &&
                            item.PurchaseOrder.PONumber.Contains(
                                normalizedSearch)
                        ));
            }

            query =
                ApplyAvailabilityFilter(
                    query,
                    status,
                    usePdfFile: false);

            var normalizedMaterial =
                Clean(material);

            if (normalizedMaterial != null)
            {
                query =
                    query.Where(item =>
                        (
                            item.MaterialNo != null &&
                            item.MaterialNo.Contains(
                                normalizedMaterial)
                        ) ||
                        (
                            item.ShortDescription != null &&
                            item.ShortDescription.Contains(
                                normalizedMaterial)
                        ) ||
                        (
                            item.BriefDescription != null &&
                            item.BriefDescription.Contains(
                                normalizedMaterial)
                        ));
            }

            var normalizedPONumber =
                Clean(poNumber);

            if (normalizedPONumber != null)
            {
                query =
                    query.Where(item =>
                        item.PurchaseOrder != null &&
                        item.PurchaseOrder.PONumber.Contains(
                            normalizedPONumber));
            }

            var normalizedDrawingVersion =
                Clean(drawingVersion);

            if (normalizedDrawingVersion != null)
            {
                query =
                    query.Where(item =>
                        item.DrawingVersion != null &&
                        item.DrawingVersion.Contains(
                            normalizedDrawingVersion));
            }

            if (updatedDateFrom.HasValue)
            {
                var updatedStart =
                    updatedDateFrom.Value.Date;

                query =
                    query.Where(item =>
                        item.DrawingUpdatedDate.HasValue &&
                        item.DrawingUpdatedDate.Value >=
                            updatedStart);
            }

            if (updatedDateTo.HasValue)
            {
                var updatedEndExclusive =
                    updatedDateTo.Value.Date.AddDays(1);

                query =
                    query.Where(item =>
                        item.DrawingUpdatedDate.HasValue &&
                        item.DrawingUpdatedDate.Value <
                            updatedEndExclusive);
            }

            query =
                ApplyAvailabilityFilter(
                    query,
                    drawingAvailability,
                    usePdfFile: false);

            query =
                ApplyAvailabilityFilter(
                    query,
                    pdfAvailability,
                    usePdfFile: true);

            return ApplyAvailabilityFilter(
                query,
                columnStatus,
                usePdfFile: false);
        }

        private static async Task<
            List<DrawingListItemDto>>
            LoadDrawingRows(
                IQueryable<POTracker.Models.Material> query,
                CancellationToken cancellationToken)
        {
            return await query
                .OrderByDescending(item =>
                    item.DrawingUpdatedDate ??
                    item.CreatedDate)
                .ThenByDescending(item =>
                    item.Id)
                .Select(item =>
                    new DrawingListItemDto
                    {
                        MaterialId =
                            item.Id,

                        MaterialNo =
                            item.MaterialNo ??
                            string.Empty,

                        Description =
                            item.ShortDescription ??
                            item.BriefDescription ??
                            string.Empty,

                        PONumber =
                            item.PurchaseOrder != null
                                ? item.PurchaseOrder.PONumber
                                : string.Empty,

                        DrawingVersion =
                            item.DrawingVersion ??
                            string.Empty,

                        DrawingUpdatedDate =
                            item.DrawingUpdatedDate,

                        DrawingFilePath =
                            item.DrawingFilePath,

                        PdfFilePath =
                            item.PDFFilePath,

                        HasDrawing =
                            item.DrawingFilePath != null &&
                            item.DrawingFilePath != string.Empty
                    })
                .ToListAsync(
                    cancellationToken);
        }

        private void ApplySecureDocumentUrls(
            IEnumerable<DrawingListItemDto> items)
        {
            foreach (var item in items)
            {
                item.DrawingFilePath =
                    _documentReferences.ToDownloadUrl(
                        item.DrawingFilePath,
                        "drawings");

                item.PdfFilePath =
                    _documentReferences.ToDownloadUrl(
                        item.PdfFilePath,
                        "pdfs");
            }
        }

        private static IEnumerable<
            DrawingListItemDto>
            ApplyDrawingSort(
                IEnumerable<
                    DrawingListItemDto> rows,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                NormalizeDrawingExcelColumn(
                    sortColumn);

            var descending =
                string.Equals(
                    sortDirection?.Trim(),
                    "desc",
                    StringComparison.OrdinalIgnoreCase);

            return (
                column,
                descending
            ) switch
            {
                ("material", true) =>
                    rows
                        .OrderByDescending(
                            row =>
                                row.MaterialNo,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(
                            row =>
                                row.Description,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(row =>
                            row.MaterialId),

                ("material", false) =>
                    rows
                        .OrderBy(
                            row =>
                                row.MaterialNo,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            row =>
                                row.Description,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row =>
                            row.MaterialId),

                ("po", true) =>
                    rows
                        .OrderByDescending(
                            row =>
                                row.PONumber,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(row =>
                            row.MaterialId),

                ("po", false) =>
                    rows
                        .OrderBy(
                            row =>
                                row.PONumber,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row =>
                            row.MaterialId),

                ("version", true) =>
                    rows
                        .OrderByDescending(
                            row =>
                                row.DrawingVersion,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(row =>
                            row.MaterialId),

                ("version", false) =>
                    rows
                        .OrderBy(
                            row =>
                                row.DrawingVersion,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row =>
                            row.MaterialId),

                ("updated", true) =>
                    rows
                        .OrderByDescending(row =>
                            row.DrawingUpdatedDate.HasValue)
                        .ThenByDescending(row =>
                            row.DrawingUpdatedDate)
                        .ThenByDescending(row =>
                            row.MaterialId),

                ("updated", false) =>
                    rows
                        .OrderByDescending(row =>
                            row.DrawingUpdatedDate.HasValue)
                        .ThenBy(row =>
                            row.DrawingUpdatedDate)
                        .ThenBy(row =>
                            row.MaterialId),

                ("drawing", true) =>
                    rows
                        .OrderByDescending(
                            row =>
                                GetDrawingExcelCellValue(
                                    row,
                                    "drawing"),
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(row =>
                            row.MaterialId),

                ("drawing", false) =>
                    rows
                        .OrderBy(
                            row =>
                                GetDrawingExcelCellValue(
                                    row,
                                    "drawing"),
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row =>
                            row.MaterialId),

                ("pdf", true) =>
                    rows
                        .OrderByDescending(
                            row =>
                                GetDrawingExcelCellValue(
                                    row,
                                    "pdf"),
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(row =>
                            row.MaterialId),

                ("pdf", false) =>
                    rows
                        .OrderBy(
                            row =>
                                GetDrawingExcelCellValue(
                                    row,
                                    "pdf"),
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row =>
                            row.MaterialId),

                ("status", true) =>
                    rows
                        .OrderByDescending(
                            row =>
                                GetDrawingExcelCellValue(
                                    row,
                                    "status"),
                            StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(row =>
                            row.MaterialId),

                ("status", false) =>
                    rows
                        .OrderBy(
                            row =>
                                GetDrawingExcelCellValue(
                                    row,
                                    "status"),
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row =>
                            row.MaterialId),

                _ =>
                    rows
            };
        }

        private static Dictionary<
            string,
            string[]>
            ParseDrawingExcelFilters(
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
                    JsonSerializer.Deserialize<
                        Dictionary<
                            string,
                            string[]>>(
                        excelFilters)
                    ?? new Dictionary<
                        string,
                        string[]>();

                return parsed
                    .Where(pair =>
                        DrawingExcelFilterColumns.Contains(
                            NormalizeDrawingExcelColumn(
                                pair.Key)))
                    .ToDictionary(
                        pair =>
                            NormalizeDrawingExcelColumn(
                                pair.Key),

                        pair =>
                            pair.Value ??
                            Array.Empty<string>(),

                        StringComparer.OrdinalIgnoreCase);
            }
            catch (
                JsonException)
            {
                return new Dictionary<
                    string,
                    string[]>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool
            MatchesDrawingExcelFilters(
                DrawingListItemDto row,
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
                    GetDrawingExcelCellValue(
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
            GetDrawingExcelCellValue(
                DrawingListItemDto row,
                string column)
        {
            return NormalizeDrawingExcelColumn(
                    column)
                switch
                {
                    "material" =>
                        string.Join(
                            " | ",
                            new[]
                            {
                                row.MaterialNo,
                                row.Description
                            }
                            .Where(value =>
                                !string.IsNullOrWhiteSpace(
                                    value))),

                    "po" =>
                        row.PONumber,

                    "version" =>
                        row.DrawingVersion,

                    "updated" =>
                        row.DrawingUpdatedDate
                            ?.ToString(
                                "yyyy-MM-dd",
                                CultureInfo.InvariantCulture)
                        ?? string.Empty,

                    "drawing" =>
                        row.HasDrawing
                            ? "Available"
                            : "Missing",

                    "pdf" =>
                        HasFile(
                            row.PdfFilePath)
                            ? "Available"
                            : "Missing",

                    "status" =>
                        row.HasDrawing
                            ? "Available"
                            : "Missing",

                    _ =>
                        string.Empty
                };
        }

        private static bool HasFile(
            string? path)
        {
            return !string.IsNullOrWhiteSpace(
                path);
        }

        private static string
            NormalizeDrawingExcelColumn(
                string? column)
        {
            return column?
                .Trim()
                .ToLowerInvariant() ??
                string.Empty;
        }

        private static IQueryable<POTracker.Models.Material>
            ApplyAvailabilityFilter(
                IQueryable<POTracker.Models.Material> query,
                string? value,
                bool usePdfFile)
        {
            var normalized =
                Clean(value)?
                    .ToLowerInvariant();

            return normalized switch
            {
                "available" =>
                    usePdfFile
                        ? query.Where(item =>
                            item.PDFFilePath != null &&
                            item.PDFFilePath != string.Empty)
                        : query.Where(item =>
                            item.DrawingFilePath != null &&
                            item.DrawingFilePath != string.Empty),

                "missing" =>
                    usePdfFile
                        ? query.Where(item =>
                            item.PDFFilePath == null ||
                            item.PDFFilePath == string.Empty)
                        : query.Where(item =>
                            item.DrawingFilePath == null ||
                            item.DrawingFilePath == string.Empty),

                _ =>
                    query
            };
        }

        private static string? Clean(
            string? value)
        {
            var cleaned =
                value?.Trim();

            return string.IsNullOrWhiteSpace(
                cleaned)
                    ? null
                    : cleaned;
        }
    }
}
