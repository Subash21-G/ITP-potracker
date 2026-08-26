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
    public sealed partial class AuditApiController :
        ControllerBase
    {
        private const int DefaultPageSize = 25;

        private const string AuditAction =
            "Material schedule updated";

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
                string? materialNo,
                string? poNumber,
                string? action,
                string? columnProcessStatus,
                string? columnDispatchStatus,
                string? columnOwner,
                DateTime? createdDateFrom,
                DateTime? createdDateTo,
                string? sortColumn,
                string? sortDirection,
                string? excelFilters,
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

            var normalizedMaterialNo =
                Clean(
                    materialNo);

            if (normalizedMaterialNo != null)
            {
                query =
                    query.Where(material =>
                        material.MaterialNo != null &&
                        material.MaterialNo.Contains(
                            normalizedMaterialNo));
            }

            var normalizedPONumber =
                Clean(
                    poNumber);

            if (normalizedPONumber != null)
            {
                query =
                    query.Where(material =>
                        material.PurchaseOrder != null &&
                        material.PurchaseOrder.PONumber.Contains(
                            normalizedPONumber));
            }

            var normalizedAction =
                Clean(
                    action);

            if (
                normalizedAction != null &&
                !AuditAction.Contains(
                    normalizedAction,
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                query =
                    query.Where(_ => false);
            }

            var normalizedColumnProcessStatus =
                Clean(
                    columnProcessStatus);

            if (normalizedColumnProcessStatus != null)
            {
                query =
                    query.Where(material =>
                        material.ProcessStatus ==
                        normalizedColumnProcessStatus);
            }

            var normalizedColumnDispatchStatus =
                Clean(
                    columnDispatchStatus);

            if (normalizedColumnDispatchStatus != null)
            {
                query =
                    query.Where(material =>
                        material.DispatchStatus ==
                        normalizedColumnDispatchStatus);
            }

            var normalizedColumnOwner =
                Clean(
                    columnOwner);

            if (normalizedColumnOwner != null)
            {
                query =
                    query.Where(material =>
                        material.AssignedDashboard != null &&
                        material.AssignedDashboard.Contains(
                            normalizedColumnOwner));
            }

            if (createdDateFrom.HasValue)
            {
                var columnStartDate =
                    createdDateFrom.Value.Date;

                query =
                    query.Where(material =>
                        material.CreatedDate >=
                        columnStartDate);
            }

            if (createdDateTo.HasValue)
            {
                var columnEndDateExclusive =
                    createdDateTo.Value.Date.AddDays(
                        1);

                query =
                    query.Where(material =>
                        material.CreatedDate <
                        columnEndDateExclusive);
            }
            query =
                ApplyAuditExcelFilters(
                    query,
                    excelFilters);

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
                await ApplyAuditSort(
                        query,
                        sortColumn,
                        sortDirection)
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
                                AuditAction,

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
