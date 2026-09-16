using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/materials/{materialId:int}/process-status")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class MaterialProcessStatusApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MaterialProcessStatusApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPatch]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(MaterialProcessStatusUpdateResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MaterialProcessStatusUpdateResponseDto>> Update(
            int materialId,
            [FromBody] MaterialProcessStatusUpdateRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            var normalizedStatus = NormalizeStatus(request?.ProcessStatus);

            if (normalizedStatus == null)
            {
                return BadRequest(new
                {
                    error = "Select Pending, In Process, Completed, or On Hold."
                });
            }

            var material = await _context.Materials
                .FirstOrDefaultAsync(
                    item => item.Id == materialId,
                    cancellationToken);

            if (material == null)
            {
                return NotFound(new
                {
                    error = $"Material {materialId} was not found."
                });
            }

            material.ProcessStatus = normalizedStatus;
            material.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new MaterialProcessStatusUpdateResponseDto
            {
                Success = true,
                Message = "Process status updated successfully.",
                MaterialId = material.Id,
                ProcessStatus = material.ProcessStatus,
                UpdatedDate = material.UpdatedDate.Value
            });
        }

        private static string? NormalizeStatus(string? value)
        {
            var status = value?.Trim();

            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                return "Pending";
            }

            if (status.Equals("In Process", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("InProcess", StringComparison.OrdinalIgnoreCase))
            {
                return "In Process";
            }

            if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("Complete", StringComparison.OrdinalIgnoreCase))
            {
                return "Completed";
            }

            if (status.Equals("On Hold", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("Hold", StringComparison.OrdinalIgnoreCase))
            {
                return "On Hold";
            }

            return null;
        }
    }
}
