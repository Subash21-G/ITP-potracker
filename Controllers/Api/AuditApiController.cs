using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

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

            var normalizedMaterialNo =
                Clean(
                    materialNo);

            var normalizedPONumber =
                Clean(
                    poNumber);

            var normalizedAction =
                Clean(
                    action);

            var normalizedColumnProcessStatus =
                Clean(
                    columnProcessStatus);

            var normalizedColumnDispatchStatus =
                Clean(
                    columnDispatchStatus);

            var normalizedColumnOwner =
                Clean(
                    columnOwner);

            var hasRealAuditLogs =
                await _context.AuditLogs.AnyAsync(cancellationToken);

            if (hasRealAuditLogs)
            {
                var auditQuery = _context.AuditLogs.AsNoTracking();

                if (normalizedSearch != null)
                {
                    auditQuery = auditQuery.Where(log =>
                        (log.MaterialNo != null && log.MaterialNo.Contains(normalizedSearch)) ||
                        (log.PONumber != null && log.PONumber.Contains(normalizedSearch)) ||
                        log.Action.Contains(normalizedSearch));
                }

                if (normalizedOwner != null)
                {
                    auditQuery = auditQuery.Where(log =>
                        (log.Owner != null && log.Owner.Contains(normalizedOwner)) ||
                        (log.UserName != null && log.UserName.Contains(normalizedOwner)));
                }

                if (normalizedProcessStatus != null)
                {
                    auditQuery = auditQuery.Where(log =>
                        log.ProcessStatus == normalizedProcessStatus);
                }

                if (normalizedDispatchStatus != null)
                {
                    auditQuery = auditQuery.Where(log =>
                        log.DispatchStatus == normalizedDispatchStatus);
                }

                if (dateFrom.HasValue)
                {
                    var startDate = dateFrom.Value.Date;
                    auditQuery = auditQuery.Where(log => log.CreatedDate >= startDate);
                }

                if (dateTo.HasValue)
                {
                    var endDateExclusive = dateTo.Value.Date.AddDays(1);
                    auditQuery = auditQuery.Where(log => log.CreatedDate < endDateExclusive);
                }

                if (normalizedMaterialNo != null)
                {
                    auditQuery = auditQuery.Where(log =>
                        log.MaterialNo != null && log.MaterialNo.Contains(normalizedMaterialNo));
                }

                if (normalizedPONumber != null)
                {
                    auditQuery = auditQuery.Where(log =>
                        log.PONumber != null && log.PONumber.Contains(normalizedPONumber));
                }

                if (normalizedAction != null)
                {
                    auditQuery = auditQuery.Where(log =>
                        log.Action.Contains(normalizedAction));
                }

                if (normalizedColumnProcessStatus != null)
                {
                    auditQuery = auditQuery.Where(log =>
                        log.ProcessStatus == normalizedColumnProcessStatus);
                }

                if (normalizedColumnDispatchStatus != null)
                {
                    auditQuery = auditQuery.Where(log =>
                        log.DispatchStatus == normalizedColumnDispatchStatus);
                }

                if (normalizedColumnOwner != null)
                {
                    auditQuery = auditQuery.Where(log =>
                        (log.Owner != null && log.Owner.Contains(normalizedColumnOwner)) ||
                        (log.UserName != null && log.UserName.Contains(normalizedColumnOwner)));
                }

                if (createdDateFrom.HasValue)
                {
                    var columnStartDate = createdDateFrom.Value.Date;
                    auditQuery = auditQuery.Where(log => log.CreatedDate >= columnStartDate);
                }

                if (createdDateTo.HasValue)
                {
                    var columnEndDateExclusive = createdDateTo.Value.Date.AddDays(1);
                    auditQuery = auditQuery.Where(log => log.CreatedDate < columnEndDateExclusive);
                }

                auditQuery = ApplyAuditLogExcelFilters(auditQuery, excelFilters);

                var totalAuditCount = await auditQuery.CountAsync(cancellationToken);
                var totalAuditPages = Math.Max(1, (int)Math.Ceiling(totalAuditCount / (double)pageSize));

                if (page > totalAuditPages)
                {
                    page = totalAuditPages;
                }

                var auditItems = await ApplyAuditLogSort(auditQuery, sortColumn, sortDirection)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(log => new AuditItemDto
                    {
                        MaterialId = log.MaterialId ?? 0,
                        CreatedDate = log.CreatedDate,
                        MaterialNo = log.MaterialNo ?? string.Empty,
                        PONumber = log.PONumber ?? string.Empty,
                        Action = log.Action,
                        ProcessStatus = log.ProcessStatus ?? string.Empty,
                        DispatchStatus = log.DispatchStatus ?? string.Empty,
                        Owner = log.Owner ?? log.UserName ?? string.Empty
                    })
                    .ToListAsync(cancellationToken);

                return Ok(new AuditPagedResponseDto
                {
                    Items = auditItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalAuditCount,
                    TotalPages = totalAuditPages,
                    GeneratedAt = DateTime.Now
                });
            }

            var query =
                _context.Materials
                    .AsNoTracking();

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

            if (normalizedMaterialNo != null)
            {
                query =
                    query.Where(material =>
                        material.MaterialNo != null &&
                        material.MaterialNo.Contains(
                            normalizedMaterialNo));
            }

            if (normalizedPONumber != null)
            {
                query =
                    query.Where(material =>
                        material.PurchaseOrder != null &&
                        material.PurchaseOrder.PONumber.Contains(
                            normalizedPONumber));
            }

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

            if (normalizedColumnProcessStatus != null)
            {
                query =
                    query.Where(material =>
                        material.ProcessStatus ==
                        normalizedColumnProcessStatus);
            }

            if (normalizedColumnDispatchStatus != null)
            {
                query =
                    query.Where(material =>
                        material.DispatchStatus ==
                        normalizedColumnDispatchStatus);
            }

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
