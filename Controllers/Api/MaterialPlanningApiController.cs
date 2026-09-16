using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/material-planning")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class MaterialPlanningApiController : ControllerBase
    {
        private const int DefaultPageSize = 25;
        private const int MaximumPageSize = 100;

        private readonly ApplicationDbContext _context;

        public MaterialPlanningApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(MaterialPlanningListResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MaterialPlanningListResponseDto>> Index(
            string workspace,
            string? scope = null,
            string? search = null,
            int page = 1,
            int pageSize = DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var normalizedWorkspace = NormalizeWorkspace(workspace);

            if (normalizedWorkspace == null)
            {
                return BadRequest(new
                {
                    error = "Workspace must be sourcing, cost-variation, or priority-urgent."
                });
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

            var baseQuery = _context.Materials
                .AsNoTracking()
                .AsQueryable();

            var normalizedSearch = search?.Trim();

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                baseQuery = baseQuery.Where(material =>
                    (material.MaterialNo != null &&
                     material.MaterialNo.Contains(normalizedSearch)) ||
                    (material.LineItem != null &&
                     material.LineItem.Contains(normalizedSearch)) ||
                    (material.ShortDescription != null &&
                     material.ShortDescription.Contains(normalizedSearch)) ||
                    (material.BriefDescription != null &&
                     material.BriefDescription.Contains(normalizedSearch)) ||
                    (material.OutsourcedTo != null &&
                     material.OutsourcedTo.Contains(normalizedSearch)) ||
                    (material.PurchaseOrder != null &&
                     material.PurchaseOrder.PONumber.Contains(normalizedSearch)));
            }

            var summary = await baseQuery
                .GroupBy(_ => 1)
                .Select(group => new MaterialPlanningSummaryDto
                {
                    TotalMaterials = group.Count(),
                    Inhouse = group.Count(material =>
                        material.SourcingType == "Inhouse"),
                    Outsourcing = group.Count(material =>
                        material.SourcingType == "Outsourcing"),
                    CostVariation = group.Count(material =>
                        material.HasCostVariation),
                    Priority = group.Count(material =>
                        material.PriorityLevel == "Priority"),
                    Urgent = group.Count(material =>
                        material.PriorityLevel == "Urgent")
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? new MaterialPlanningSummaryDto();

            var resultQuery = ApplyWorkspace(
                baseQuery,
                normalizedWorkspace,
                scope);

            var totalRecords = await resultQuery.CountAsync(cancellationToken);
            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(totalRecords / (double)pageSize));
            page = Math.Min(page, totalPages);

            var items = await resultQuery
                .OrderByDescending(material =>
                    material.PriorityLevel == "Urgent")
                .ThenByDescending(material =>
                    material.PriorityLevel == "Priority")
                .ThenBy(material => material.DeliveryDate)
                .ThenBy(material => material.LineItem)
                .ThenBy(material => material.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(material => new MaterialPlanningItemDto
                {
                    Id = material.Id,
                    PurchaseOrderId = material.PurchaseOrderId,
                    PONumber = material.PurchaseOrder != null
                        ? material.PurchaseOrder.PONumber
                        : string.Empty,
                    LineItem = material.LineItem ?? string.Empty,
                    MaterialNo = material.MaterialNo ?? string.Empty,
                    Description = material.ShortDescription != null &&
                                  material.ShortDescription != string.Empty
                        ? material.ShortDescription
                        : material.BriefDescription ?? string.Empty,
                    Quantity = material.Qty,
                    Unit = material.Unit ?? string.Empty,
                    Cost = material.Cost,
                    SourcingType = material.SourcingType,
                    OutsourcedTo = material.OutsourcedTo ?? string.Empty,
                    HasCostVariation = material.HasCostVariation,
                    VariationCost = material.VariationCost,
                    PriorityLevel = material.PriorityLevel,
                    ProcessStatus = material.ProcessStatus,
                    DeliveryDate = material.DeliveryDate
                })
                .ToListAsync(cancellationToken);

            return Ok(new MaterialPlanningListResponseDto
            {
                Summary = summary,
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            });
        }

        private static IQueryable<Material> ApplyWorkspace(
            IQueryable<Material> query,
            string workspace,
            string? scope)
        {
            var normalizedScope = string.Concat(
                (scope ?? string.Empty)
                    .Where(char.IsLetterOrDigit))
                .ToLowerInvariant();

            if (workspace == "cost-variation")
            {
                return query.Where(material => material.HasCostVariation);
            }

            if (workspace == "priority-urgent")
            {
                return normalizedScope switch
                {
                    "priority" => query.Where(material =>
                        material.PriorityLevel == "Priority"),
                    "urgent" => query.Where(material =>
                        material.PriorityLevel == "Urgent"),
                    _ => query.Where(material =>
                        material.PriorityLevel == "Priority" ||
                        material.PriorityLevel == "Urgent")
                };
            }

            return normalizedScope switch
            {
                "inhouse" => query.Where(material =>
                    material.SourcingType == "Inhouse"),
                "outsourcing" => query.Where(material =>
                    material.SourcingType == "Outsourcing"),
                _ => query
            };
        }

        private static string? NormalizeWorkspace(string? workspace)
        {
            return string.Concat(
                    (workspace ?? string.Empty)
                        .Trim()
                        .ToLowerInvariant()
                        .Select(character =>
                            character == '_' || char.IsWhiteSpace(character)
                                ? '-'
                                : character))
                switch
                {
                    "sourcing" => "sourcing",
                    "cost-variation" => "cost-variation",
                    "priority-urgent" => "priority-urgent",
                    _ => null
                };
        }
    }
}
