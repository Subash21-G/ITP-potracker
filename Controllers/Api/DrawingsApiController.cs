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
                int page = 1,
                int pageSize = 25,
                CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);

            pageSize =
                pageSize is 10 or 25 or 50 or 100
                    ? pageSize
                    : 25;

            var allMaterials =
                _context.Materials
                    .AsNoTracking();

            var weekStart =
                DateTime.Today.AddDays(-7);

            var summary =
                new DrawingSummaryDto
                {
                    TotalMaterials =
                        await allMaterials.CountAsync(
                            cancellationToken),

                    MissingDrawings =
                        await allMaterials.CountAsync(
                            item =>
                                item.DrawingFilePath == null ||
                                item.DrawingFilePath == string.Empty,
                            cancellationToken),

                    UpdatedThisWeek =
                        await allMaterials.CountAsync(
                            item =>
                                item.DrawingUpdatedDate.HasValue &&
                                item.DrawingUpdatedDate.Value >=
                                weekStart,
                            cancellationToken),

                    WithVersion =
                        await allMaterials.CountAsync(
                            item =>
                                item.DrawingVersion != null &&
                                item.DrawingVersion != string.Empty,
                            cancellationToken)
                };

            var query =
                allMaterials.AsQueryable();

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

            query =
                ApplyAvailabilityFilter(
                    query,
                    columnStatus,
                    usePdfFile: false);

            var totalRecords =
                await query.CountAsync(
                    cancellationToken);

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
                await query
                    .OrderByDescending(item =>
                        item.DrawingUpdatedDate ??
                        item.CreatedDate)
                    .ThenByDescending(item =>
                        item.Id)
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(pageSize)
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