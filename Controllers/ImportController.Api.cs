using Microsoft.AspNetCore.Mvc;
using POTracker.ViewModels;

namespace POTracker.Controllers
{
    public partial class ImportController
    {
        // ============================================================
        // ANGULAR API - UPLOAD AND PREVIEW MATERIAL EXCEL
        // ============================================================

        [HttpPost("~/api/material-import/preview")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaximumExcelFileSize)]
        public async Task<IActionResult> PreviewApi(
            [FromForm]
            IFormFile? excelFile)
        {
            if (!IsLoggedIn())
            {
                return Unauthorized(new
                {
                    message =
                        "Your session has expired. Sign in again."
                });
            }

            var uploadResult =
                await Upload(excelFile);

            if (uploadResult is RedirectToActionResult redirect &&
                string.Equals(
                    redirect.ActionName,
                    nameof(Preview),
                    StringComparison.OrdinalIgnoreCase))
            {
                var batchId =
                    redirect.RouteValues?
                        ["batchId"]
                        ?.ToString();

                var batch =
                    await LoadBatchAsync(batchId);

                if (batch == null)
                {
                    return BadRequest(new
                    {
                        message =
                            "The material import preview could not be loaded."
                    });
                }

                var rows =
                    batch.Rows.Select(row => new
                    {
                        row.RowNumber,
                        row.PurchaserName,
                        row.ContactPerson,
                        row.PONumber,
                        row.PODate,
                        row.ReceivedDate,
                        row.BuyerContactPerson,
                        row.LineItem,
                        row.MaterialNo,
                        row.HsnSacCode,
                        row.ShortDescription,
                        row.BriefDescription,
                        row.Qty,
                        row.Unit,
                        row.Dep,
                        row.ValueOfGoods,
                        row.PriceUnit,
                        row.TotalValue,
                        row.DeliveryDate,
                        row.DrawingNo,
                        row.AssignedDashboard,
                        row.Remark,

                        isValid =
                            row.Errors == null ||
                            row.Errors.Count == 0,

                        errors =
                            row.Errors ??
                            new List<string>(),

                        errorMessage =
                            row.Errors == null ||
                            row.Errors.Count == 0
                                ? string.Empty
                                : string.Join(
                                    " | ",
                                    row.Errors)
                    })
                    .ToList();

                return Ok(new
                {
                    batchId =
                        batch.BatchId,

                    originalFileName =
                        batch.OriginalFileName,

                    createdAt =
                        batch.CreatedAt,

                    rows,

                    totalRows =
                        rows.Count,

                    validRows =
                        rows.Count(row =>
                            row.isValid),

                    invalidRows =
                        rows.Count(row =>
                            !row.isValid),

                    totalQuantity =
                        batch.Rows
                            .Where(row =>
                                row.Errors == null ||
                                row.Errors.Count == 0)
                            .Sum(row =>
                                row.Qty),

                    totalValue =
                        batch.Rows
                            .Where(row =>
                                row.Errors == null ||
                                row.Errors.Count == 0)
                            .Sum(row =>
                                row.TotalValue)
                });
            }

            var errorMessage =
                TempData["Error"]
                    ?.ToString() ??
                ViewData["Error"]
                    ?.ToString() ??
                "Unable to process the material Excel file.";

            TempData.Remove("Error");
            ViewData.Remove("Error");

            return BadRequest(new
            {
                message =
                    errorMessage
            });
        }

        // ============================================================
        // ANGULAR API - COMMIT MATERIAL IMPORT
        // ============================================================

        [HttpPost("~/api/material-import/commit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CommitApi(
            [FromBody]
            MaterialImportCommitRequest request)
        {
            if (!IsLoggedIn())
            {
                return Unauthorized(new
                {
                    message =
                        "Your session has expired. Sign in again."
                });
            }

            if (string.IsNullOrWhiteSpace(
                    request.BatchId))
            {
                return BadRequest(new
                {
                    message =
                        "A valid material import batch is required."
                });
            }

            var commitResult =
                await Commit(
                    request.BatchId);

            if (commitResult is RedirectToActionResult redirect &&
                string.Equals(
                    redirect.ControllerName,
                    "Material",
                    StringComparison.OrdinalIgnoreCase))
            {
                var message =
                    TempData["Success"]
                        ?.ToString() ??
                    "Material import completed successfully.";

                TempData.Remove("Success");
                TempData.Remove("Error");
                ViewData.Remove("Error");

                return Ok(new
                {
                    message,
                    batchId =
                        request.BatchId
                });
            }

            var errorMessage =
                TempData["Error"]
                    ?.ToString() ??
                ViewData["Error"]
                    ?.ToString() ??
                "The material import could not be completed.";

            TempData.Remove("Success");
            TempData.Remove("Error");
            ViewData.Remove("Error");

            return BadRequest(new
            {
                message =
                    errorMessage
            });
        }

        // ============================================================
        // ANGULAR API - CANCEL MATERIAL IMPORT PREVIEW
        // ============================================================

        [HttpDelete("~/api/material-import/{batchId}")]
        [ValidateAntiForgeryToken]
        public IActionResult CancelApi(
            string batchId)
        {
            if (!IsLoggedIn())
            {
                return Unauthorized(new
                {
                    message =
                        "Your session has expired. Sign in again."
                });
            }

            if (string.IsNullOrWhiteSpace(
                    batchId))
            {
                return BadRequest(new
                {
                    message =
                        "A valid material import batch is required."
                });
            }

            DeleteBatchFile(batchId);

            return Ok(new
            {
                message =
                    "Material import preview cancelled."
            });
        }
    }

    public sealed class MaterialImportCommitRequest
    {
        public string BatchId { get; set; }
            = string.Empty;
    }
}
