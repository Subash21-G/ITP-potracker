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
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(20L * 1024L * 1024L)]
        [RequestFormLimits(
            ValueCountLimit = 10000,
            ValueLengthLimit = int.MaxValue,
            MultipartBodyLengthLimit = 20L * 1024L * 1024L)]
        public async Task<IActionResult> PreviewApi(
            [FromForm] IFormFile? file)
        {
            var result =
                await Upload(file);

            if (result is ViewResult viewResult &&
                viewResult.Model is
                    InvoiceImportPreviewViewModel preview)
            {
                var existsInDatabase =
                    !string.IsNullOrWhiteSpace(
                        preview.InvoiceNumber) &&
                    await _context.Invoices
                        .AsNoTracking()
                        .AnyAsync(invoice =>
                            invoice.InvoiceNumber ==
                            preview.InvoiceNumber.Trim());

                return Ok(new
                {
                    originalFileName =
                        preview.UploadedFileName,

                    fileSize =
                        file?.Length ?? 0,

                    isSuccessful = true,

                    existsInDatabase,

                    errorMessage =
                        (string?)null,

                    invoice = preview
                });
            }

            var errorMessage =
                ViewData["Error"]?.ToString() ??
                TempData["Error"]?.ToString() ??
                "Unable to extract the invoice file.";

            TempData.Remove("Error");
            ViewData.Remove("Error");

            return BadRequest(new
            {
                message = errorMessage
            });
        }

        // ============================================================
        // ANGULAR API - PREVIEW MULTIPLE INVOICE FILES
        // ============================================================

        [HttpPost("~/api/invoice-import/preview-batch")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(200L * 1024L * 1024L)]
        [RequestFormLimits(
            ValueCountLimit = 50000,
            ValueLengthLimit = int.MaxValue,
            MultipartBodyLengthLimit = 200L * 1024L * 1024L)]
        public async Task<IActionResult> PreviewBatchApi(
            [FromForm]
            List<IFormFile>? invoiceFiles)
        {
            var result =
                await UploadBatch(invoiceFiles);

            if (result is ViewResult viewResult &&
                viewResult.Model is
                    InvoiceBatchImportPreviewViewModel preview)
            {
                return Ok(preview);
            }

            var errorMessage =
                ViewData["Error"]?.ToString() ??
                TempData["Error"]?.ToString() ??
                "Unable to extract the invoice files.";

            TempData.Remove("Error");
            ViewData.Remove("Error");

            return BadRequest(new
            {
                message = errorMessage
            });
        }

        // ============================================================
        // ANGULAR API - SAVE SINGLE INVOICE
        // ============================================================

        [HttpPost("~/api/invoice-import/save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveApi(
            [FromBody]
            InvoiceImportPreviewViewModel model)
        {
            var result =
                await SaveAll(model);

            if (result is RedirectToActionResult redirect &&
                string.Equals(
                    redirect.ActionName,
                    nameof(History),
                    StringComparison.OrdinalIgnoreCase))
            {
                var invoiceNumber =
                    model.InvoiceNumber?.Trim() ??
                    string.Empty;

                var invoice =
                    await _context.Invoices
                        .AsNoTracking()
                        .FirstOrDefaultAsync(item =>
                            item.InvoiceNumber ==
                            invoiceNumber);

                var latestBatch =
                    await _context
                        .VyaparInvoiceImportBatches
                        .AsNoTracking()
                        .Where(batch =>
                            batch.FileName ==
                            model.UploadedFileName)
                        .OrderByDescending(batch =>
                            batch.Id)
                        .FirstOrDefaultAsync();

                var totalRows =
                    latestBatch?.TotalRows ??
                    model.Items.Count;

                var successRows =
                    latestBatch?.SuccessRows ??
                    0;

                var pendingRows =
                    Math.Max(
                        0,
                        totalRows -
                        successRows);

                var message =
                    TempData["Success"]
                        ?.ToString() ??
                    "Invoice imported successfully.";

                TempData.Remove("Success");
                TempData.Remove("Error");
                ViewData.Remove("Error");

                return Ok(new
                {
                    message,

                    invoiceId =
                        invoice?.Id ?? 0,

                    invoiceNumber,

                    totalRows,

                    successRows,

                    pendingRows
                });
            }

            var errorMessage =
                ViewData["Error"]?.ToString() ??
                TempData["Error"]?.ToString() ??
                "The invoice could not be saved.";

            TempData.Remove("Success");
            TempData.Remove("Error");
            ViewData.Remove("Error");

            return BadRequest(new
            {
                message = errorMessage
            });
        }

        // ============================================================
        // ANGULAR API - SAVE MULTIPLE INVOICES
        // ============================================================

        [HttpPost("~/api/invoice-import/save-batch")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBatchApi(
            [FromBody]
            InvoiceBatchImportPreviewViewModel model)
        {
            var result =
                await SaveBatch(model);

            if (result is ViewResult viewResult &&
                viewResult.Model is
                    InvoiceBatchImportResultViewModel batchResult)
            {
                TempData.Remove("Success");
                TempData.Remove("Error");
                ViewData.Remove("Error");

                return Ok(batchResult);
            }

            var errorMessage =
                ViewData["Error"]?.ToString() ??
                TempData["Error"]?.ToString() ??
                "The invoice batch could not be saved.";

            TempData.Remove("Success");
            TempData.Remove("Error");
            ViewData.Remove("Error");

            return BadRequest(new
            {
                message = errorMessage
            });
        }
    }
}
