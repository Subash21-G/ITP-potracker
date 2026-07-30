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
        private readonly ApplicationDbContext _context;

        public AuditApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(AuditResponseDto),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<AuditResponseDto>>
            Index(
                int limit = 25,
                CancellationToken cancellationToken = default)
        {
            limit =
                Math.Clamp(
                    limit,
                    1,
                    100);

            var items =
                await _context.Materials
                    .AsNoTracking()
                    .OrderByDescending(material =>
                        material.Id)
                    .Take(limit)
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
                new AuditResponseDto
                {
                    Items = items,
                    RecordCount = items.Count,
                    GeneratedAt = DateTime.Now
                });
        }
    }
}