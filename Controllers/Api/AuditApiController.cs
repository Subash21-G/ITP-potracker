using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/audit")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class AuditApiController :
        ControllerBase
    {
        private const int DefaultPageSize = 25;

        private readonly ApplicationDbContext _context;

        public AuditApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(AuditPagedResponseDto),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<AuditPagedResponseDto>>
            Index(
                string? search,
                string? owner,
                string? processStatus,
                string? dispatchStatus,
                DateTime? dateFrom,
                DateTime? dateTo,
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
                _context.Materials
                    .AsNoTracking();

            var normalizedSearch =
                Clean(
                    search);

            var normalizedOwner =
                Clean(
                    owner);

            var normalizedProcessStatus =
                Clean(
                    processStatus);

            var normalizedDispatchStatus =
                Clean(
                    dispatchStatus);

            if (normalizedSearch != null)
            {
                query =
                    query.Where(material =>
                        (
                            material.MaterialNo != null &&
                            material.MaterialNo.Contains(
                                normalizedSearch)
                        ) ||
                        (
                            material.PurchaseOrder != null &&
                            material.PurchaseOrder.PONumber.Contains(
                                normalizedSearch)
                        ));
            }

            if (normalizedOwner != null)
            {
                query =
                    query.Where(material =>
                        material.AssignedDashboard != null &&
                        material.AssignedDashboard.Contains(
                            normalizedOwner));
            }

            if (normalizedProcessStatus != null)
            {
                query =
                    query.Where(material =>
                        material.ProcessStatus ==
                        normalizedProcessStatus);
            }

            if (normalizedDispatchStatus != null)
            {
                query =
                    query.Where(material =>
                        material.DispatchStatus ==
                        normalizedDispatchStatus);
            }

            if (dateFrom.HasValue)
            {
                var startDate =
                    dateFrom.Value.Date;

                query =
                    query.Where(material =>
                        material.CreatedDate >=
                        startDate);
            }

            if (dateTo.HasValue)
            {
                var endDateExclusive =
                    dateTo.Value.Date.AddDays(
                        1);

                query =
                    query.Where(material =>
                        material.CreatedDate <
                        endDateExclusive);
            }

            var totalCount =
                await query.CountAsync(
                    cancellationToken);

            var totalPages =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        totalCount /
                        (double)pageSize));

            if (page > totalPages)
            {
                page =
                    totalPages;
            }

            var items =
                await query
                    .OrderByDescending(material =>
                        material.CreatedDate)
                    .ThenByDescending(material =>
                        material.Id)
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(
                        pageSize)
                    .Select(material =>
                        new AuditItemDto
                        {
                            MaterialId =
                                material.Id,

                            CreatedDate =
                                material.CreatedDate,

                            MaterialNo =
                                material.MaterialNo ??
                                string.Empty,

                            PONumber =
                                material.PurchaseOrder != null
                                    ? material.PurchaseOrder.PONumber
                                    : string.Empty,

                            Action =
                                "Material schedule updated",

                            ProcessStatus =
                                material.ProcessStatus ??
                                string.Empty,

                            DispatchStatus =
                                material.DispatchStatus ??
                                string.Empty,

                            Owner =
                                material.AssignedDashboard ??
                                string.Empty
                        })
                    .ToListAsync(
                        cancellationToken);

            return Ok(
                new AuditPagedResponseDto
                {
                    Items =
                        items,

                    Page =
                        page,

                    PageSize =
                        pageSize,

                    TotalCount =
                        totalCount,

                    TotalPages =
                        totalPages,

                    GeneratedAt =
                        DateTime.Now
                });
        }

        private static int NormalizePageSize(
            int pageSize)
        {
            return pageSize is
                25 or 50 or 100
                    ? pageSize
                    : DefaultPageSize;
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
