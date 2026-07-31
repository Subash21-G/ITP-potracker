using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Services;
using POTracker.ViewModels;

namespace POTracker.Controllers
{
    public partial class VyaparInvoiceImportController
    {
        // ============================================================
        // ANGULAR API - DOWNLOAD INVOICE IMPORT TEMPLATE
        // ============================================================

        [HttpGet("~/api/invoice-import/template")]
        [Produces(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult DownloadExcelTemplateApi()
        {
            var fileContent =
                InvoiceImportTemplateService
                    .CreateInvoiceImportTemplate();

            return File(
                fileContent,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "POTracker_Invoice_Import_Template.xlsx");
        }

        // ============================================================
        // ANGULAR API - PREVIEW SINGLE INVOICE FILE
        // ============================================================

        [HttpPost("~/api/invoice-import/preview")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [RequestSizeLimit(20L * 1024L * 1024L)]
        [RequestFormLimits(
            ValueCountLimit = 10000,
            ValueLengthLimit = int.MaxValue,
            MultipartBodyLengthLimit = 20L * 1024L * 1024L)]
        public async Task<IActionResult> PreviewApi(
            [FromForm] IFormFile? file)
        {
            var result =
                await ProcessSingleInvoiceFileAsync(
                    file);

            if (!result.IsSuccessful ||
                result.Invoice == null)
            {
                return BadRequest(new
                {
                    message =
                        result.ErrorMessage ??
                        "Unable to extract the invoice file."
                });
            }

            return Ok(new
            {
                originalFileName =
                    result.OriginalFileName,

                fileSize =
                    result.FileSize,

                isSuccessful =
                    true,

                existsInDatabase =
                    result.ExistsInDatabase,

                errorMessage =
                    (string?)null,

                invoice =
                    result.Invoice
            });
        }
        // ============================================================
        // ANGULAR API - PREVIEW MULTIPLE INVOICE FILES
        // ============================================================

        [HttpPost("~/api/invoice-import/preview-batch")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [RequestSizeLimit(200L * 1024L * 1024L)]
        [RequestFormLimits(
            ValueCountLimit = 50000,
            ValueLengthLimit = int.MaxValue,
            MultipartBodyLengthLimit = 200L * 1024L * 1024L)]
        public async Task<IActionResult> PreviewBatchApi(
            [FromForm]
            List<IFormFile>? invoiceFiles)
        {
            var (
                preview,
                errorMessage) =
                await ProcessInvoiceBatchFilesAsync(
                    invoiceFiles);

            if (preview == null)
            {
                return BadRequest(new
                {
                    message =
                        errorMessage ??
                        "Unable to extract the invoice files."
                });
            }

            return Ok(preview);
        }
        // ============================================================
        // ANGULAR API - SAVE SINGLE INVOICE
        // ============================================================

        [HttpPost("~/api/invoice-import/save")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<IActionResult> SaveApi(
            [FromBody]
            InvoiceImportPreviewViewModel model)
        {
            var result =
                await SaveInvoiceAsync(
                    model);

            if (!result.IsSuccessful)
            {
                return BadRequest(new
                {
                    message =
                        result.Message
                });
            }

            return Ok(new
            {
                message =
                    result.Message,

                invoiceId =
                    result.InvoiceId,

                invoiceNumber =
                    result.InvoiceNumber,

                totalRows =
                    result.TotalRows,

                successRows =
                    result.SuccessRows,

                pendingRows =
                    result.PendingRows
            });
        }
        // ============================================================
        // ANGULAR API - SAVE MULTIPLE INVOICES
        // ============================================================

        [HttpPost("~/api/invoice-import/save-batch")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<IActionResult> SaveBatchApi(
            [FromBody]
            InvoiceBatchImportPreviewViewModel model)
        {
            var (
                result,
                errorMessage) =
                await SaveInvoiceBatchAsync(
                    model);

            if (result == null)
            {
                return BadRequest(new
                {
                    message =
                        errorMessage ??
                        "The invoice batch could not be saved."
                });
            }

            return Ok(result);
        }
    }
}
