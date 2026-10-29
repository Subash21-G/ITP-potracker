using System.ComponentModel.DataAnnotations;
using ITPQuotation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITPQuotation.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Estimator")]
[Route("api/quotations/{id:int}/potracker")]
public sealed class PoTrackerExportController(PoTrackerExportService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Export(int id, ExportQuotationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExportAsync(id, request.CustomerPoNumber, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (PoTrackerExportException exception)
        {
            return Problem(statusCode: exception.StatusCode, title: "POTracker export failed", detail: exception.Message);
        }
    }
}

public sealed class ExportQuotationRequest
{
    [Required, StringLength(100)]
    public string CustomerPoNumber { get; set; } = "";
}
