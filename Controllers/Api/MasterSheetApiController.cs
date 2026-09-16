using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/mastersheet")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class MasterSheetApiController : ControllerBase
    {
        private const int MaximumProcessSteps = 100;

        private readonly ApplicationDbContext _context;

        public MasterSheetApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("process-options")]
        public async Task<ActionResult<IReadOnlyList<MasterSheetProcessOptionDto>>>
            GetProcessOptions(CancellationToken cancellationToken = default)
        {
            var options = await _context.MasterSheetProcessOptions
                .AsNoTracking()
                .OrderBy(option => option.CreatedDate)
                .ThenBy(option => option.Id)
                .Select(option => new MasterSheetProcessOptionDto
                {
                    Id = option.Id,
                    ProcessName = option.ProcessName,
                    VendorName = option.VendorName ?? string.Empty,
                    Source = option.Source
                })
                .ToListAsync(cancellationToken);

            return Ok(options);
        }

        [HttpGet("records")]
        public async Task<ActionResult<IReadOnlyList<MasterSheetRecordDto>>> GetRecords(
            CancellationToken cancellationToken = default)
        {
            var records = await _context.MasterSheetRecords
                .AsNoTracking()
                .Include(record => record.ProcessSteps)
                .OrderBy(record => record.MaterialNo)
                .ToListAsync(cancellationToken);

            return Ok(records.Select(MapRecord).ToArray());
        }

        [HttpPost("process-options")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<ActionResult<MasterSheetProcessOptionDto>>
            AddProcessOption(
                [FromBody] MasterSheetProcessOptionRequestDto request,
                CancellationToken cancellationToken = default)
        {
            var processName = request.ProcessName.Trim();

            if (processName.Length == 0)
            {
                ModelState.AddModelError(
                    nameof(request.ProcessName),
                    "Process name is required.");

                return ValidationProblem(ModelState);
            }

            var option = new MasterSheetProcessOption
            {
                ProcessName = processName,
                VendorName = NormalizeOptional(request.VendorName),
                Source = request.Source,
                CreatedDate = DateTime.Now
            };

            _context.MasterSheetProcessOptions.Add(option);
            await _context.SaveChangesAsync(cancellationToken);

            var response = MapOption(option);

            return CreatedAtAction(
                nameof(GetProcessOptions),
                value: response);
        }

        [HttpDelete("process-options/{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<IActionResult> DeleteProcessOption(
            int id,
            CancellationToken cancellationToken = default)
        {
            var option = await _context.MasterSheetProcessOptions
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (option == null)
            {
                return NotFound();
            }

            _context.MasterSheetProcessOptions.Remove(option);
            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        [HttpGet("record")]
        public async Task<ActionResult<MasterSheetRecordDto>> GetRecord(
            [FromQuery] string materialNo,
            CancellationToken cancellationToken = default)
        {
            var normalizedMaterialNo = materialNo?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedMaterialNo))
            {
                return BadRequest(new { error = "Material number is required." });
            }

            var record = await _context.MasterSheetRecords
                .AsNoTracking()
                .Include(item => item.ProcessSteps)
                .SingleOrDefaultAsync(
                    item => item.MaterialNo == normalizedMaterialNo,
                    cancellationToken);

            if (record == null)
            {
                return NotFound();
            }

            return Ok(MapRecord(record));
        }

        [HttpPut("record")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<ActionResult<MasterSheetRecordDto>> SaveRecord(
            [FromBody] MasterSheetSaveRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var materialNo = request.MaterialNo.Trim();
            var requestedSteps = request.ProcessSteps ?? new();

            if (materialNo.Length == 0)
            {
                ModelState.AddModelError(
                    nameof(request.MaterialNo),
                    "Material number is required.");
            }

            if (requestedSteps.Count > MaximumProcessSteps)
            {
                ModelState.AddModelError(
                    nameof(request.ProcessSteps),
                    $"A maximum of {MaximumProcessSteps} process steps is allowed.");
            }

            for (var index = 0; index < requestedSteps.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(requestedSteps[index].ProcessName))
                {
                    ModelState.AddModelError(
                        $"{nameof(request.ProcessSteps)}[{index}].{nameof(MasterSheetProcessStepRequestDto.ProcessName)}",
                        "Process name is required.");
                }
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var record = await _context.MasterSheetRecords
                .Include(item => item.ProcessSteps)
                .SingleOrDefaultAsync(
                    item => item.MaterialNo == materialNo,
                    cancellationToken);

            var now = DateTime.Now;

            var existingSteps = Array.Empty<MasterSheetProcessStep>();

            if (record == null)
            {
                record = new MasterSheetRecord
                {
                    MaterialNo = materialNo,
                    CreatedDate = now
                };

                _context.MasterSheetRecords.Add(record);
            }
            else
            {
                existingSteps = record.ProcessSteps
                    .OrderBy(step => step.Sequence)
                    .ThenBy(step => step.Id)
                    .ToArray();
                record.UpdatedDate = now;
            }

            record.ShortDescription = NormalizeOptional(request.ShortDescription);
            record.Grade = NormalizeOptional(request.Grade);
            record.DrawingNo = NormalizeOptional(request.DrawingNo);
            record.DrawingVersion = NormalizeOptional(request.DrawingVersion);
            record.FinishSize = NormalizeOptional(request.FinishSize);
            record.RequiredRmSize = NormalizeOptional(request.RequiredRmSize);

            for (var index = 0; index < requestedSteps.Count; index++)
            {
                var requestStep = requestedSteps[index];
                var step = index < existingSteps.Length
                    ? existingSteps[index]
                    : new MasterSheetProcessStep();

                step.Sequence = index + 1;
                step.ProcessName = requestStep.ProcessName.Trim();
                step.VendorName = NormalizeOptional(requestStep.VendorName);
                step.Source = requestStep.Source;

                if (index >= existingSteps.Length)
                {
                    record.ProcessSteps.Add(step);
                }
            }

            if (existingSteps.Length > requestedSteps.Count)
            {
                var removedSteps = existingSteps
                    .Skip(requestedSteps.Count)
                    .ToArray();

                _context.MasterSheetProcessSteps.RemoveRange(removedSteps);

                foreach (var removedStep in removedSteps)
                {
                    record.ProcessSteps.Remove(removedStep);
                }
            }

            await SynchronizeMaterials(record, now, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(MapRecord(record));
        }

        [HttpDelete("record")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<ActionResult<MasterSheetDeleteResponseDto>> DeleteRecord(
            [FromQuery] string materialNo,
            CancellationToken cancellationToken = default)
        {
            var normalizedMaterialNo = materialNo?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedMaterialNo))
            {
                return BadRequest(new { error = "Material number is required." });
            }

            var record = await _context.MasterSheetRecords
                .SingleOrDefaultAsync(
                    item => item.MaterialNo == normalizedMaterialNo,
                    cancellationToken);

            if (record == null)
            {
                return NotFound();
            }

            _context.MasterSheetRecords.Remove(record);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new MasterSheetDeleteResponseDto
            {
                Success = true,
                MaterialNo = normalizedMaterialNo,
                Message = "Master sheet data deleted."
            });
        }

        private async Task SynchronizeMaterials(
            MasterSheetRecord record,
            DateTime updatedDate,
            CancellationToken cancellationToken)
        {
            var materials = await _context.Materials
                .Where(material => material.MaterialNo == record.MaterialNo)
                .ToListAsync(cancellationToken);

            foreach (var material in materials)
            {
                material.ShortDescription = record.ShortDescription;
                material.RMGrade = record.Grade;
                material.DrawingNo = record.DrawingNo;
                material.DrawingVersion = record.DrawingVersion;
                material.SurfaceFinish = record.FinishSize;
                material.RMSize = record.RequiredRmSize;
                material.UpdatedDate = updatedDate;
            }
        }

        private static MasterSheetRecordDto MapRecord(MasterSheetRecord record)
        {
            return new MasterSheetRecordDto
            {
                Id = record.Id,
                MaterialNo = record.MaterialNo,
                ShortDescription = record.ShortDescription ?? string.Empty,
                Grade = record.Grade ?? string.Empty,
                DrawingNo = record.DrawingNo ?? string.Empty,
                DrawingVersion = record.DrawingVersion ?? string.Empty,
                FinishSize = record.FinishSize ?? string.Empty,
                RequiredRmSize = record.RequiredRmSize ?? string.Empty,
                ProcessSteps = record.ProcessSteps
                    .OrderBy(step => step.Sequence)
                    .ThenBy(step => step.Id)
                    .Select(step => new MasterSheetProcessStepDto
                    {
                        Id = step.Id,
                        Sequence = step.Sequence,
                        ProcessName = step.ProcessName,
                        VendorName = step.VendorName ?? string.Empty,
                        Source = step.Source
                    })
                    .ToArray(),
                CreatedDate = record.CreatedDate,
                UpdatedDate = record.UpdatedDate
            };
        }

        private static MasterSheetProcessOptionDto MapOption(
            MasterSheetProcessOption option)
        {
            return new MasterSheetProcessOptionDto
            {
                Id = option.Id,
                ProcessName = option.ProcessName,
                VendorName = option.VendorName ?? string.Empty,
                Source = option.Source
            };
        }

        private static string? NormalizeOptional(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrEmpty(normalized) ? null : normalized;
        }
    }
}
