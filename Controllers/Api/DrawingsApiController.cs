using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;

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

        public DrawingsApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(DrawingListResponseDto),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<DrawingListResponseDto>>
            Index(
                string? search,
                string? status,
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
                            material =>
                                material.DrawingFilePath == null ||
                                material.DrawingFilePath == string.Empty,
                            cancellationToken),

                    UpdatedThisWeek =
                        await allMaterials.CountAsync(
                            material =>
                                material.DrawingUpdatedDate.HasValue &&
                                material.DrawingUpdatedDate.Value >=
                                weekStart,
                            cancellationToken),

                    WithVersion =
                        await allMaterials.CountAsync(
                            material =>
                                material.DrawingVersion != null &&
                                material.DrawingVersion != string.Empty,
                            cancellationToken)
                };

            var query =
                allMaterials.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var value =
                    search.Trim();

                query =
                    query.Where(material =>
                        (material.MaterialNo != null &&
                         material.MaterialNo.Contains(value)) ||
                        (material.ShortDescription != null &&
                         material.ShortDescription.Contains(value)) ||
                        (material.DrawingVersion != null &&
                         material.DrawingVersion.Contains(value)) ||
                        (material.PurchaseOrder != null &&
                         material.PurchaseOrder.PONumber.Contains(value)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                switch (
                    status.Trim().ToLowerInvariant())
                {
                    case "available":
                        query =
                            query.Where(material =>
                                material.DrawingFilePath != null &&
                                material.DrawingFilePath != string.Empty);
                        break;

                    case "missing":
                        query =
                            query.Where(material =>
                                material.DrawingFilePath == null ||
                                material.DrawingFilePath == string.Empty);
                        break;
                }
            }

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
                    .OrderByDescending(material =>
                        material.DrawingUpdatedDate ??
                        material.CreatedDate)
                    .ThenByDescending(material =>
                        material.Id)
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(pageSize)
                    .Select(material =>
                        new DrawingListItemDto
                        {
                            MaterialId =
                                material.Id,

                            MaterialNo =
                                material.MaterialNo ??
                                string.Empty,

                            Description =
                                material.ShortDescription ??
                                material.BriefDescription ??
                                string.Empty,

                            PONumber =
                                material.PurchaseOrder != null
                                    ? material.PurchaseOrder.PONumber
                                    : string.Empty,

                            DrawingVersion =
                                material.DrawingVersion ??
                                string.Empty,

                            DrawingUpdatedDate =
                                material.DrawingUpdatedDate,

                            DrawingFilePath =
                                material.DrawingFilePath,

                            PdfFilePath =
                                material.PDFFilePath,

                            HasDrawing =
                                material.DrawingFilePath != null &&
                                material.DrawingFilePath != string.Empty
                        })
                    .ToListAsync(
                        cancellationToken);

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
    }
}