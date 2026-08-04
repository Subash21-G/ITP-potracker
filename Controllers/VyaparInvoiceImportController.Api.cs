using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Services;
using POTracker.ViewModels;
using System.Text.Json;

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
        // ANGULAR API - DISCARD TEMPORARY INVOICE PREVIEWS
        // ============================================================

        [HttpPost("~/api/invoice-import/discard-previews")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<IActionResult> DiscardPreviewsApi(
            [FromBody]
            InvoicePreviewDiscardRequest? request)
        {
            var submittedPaths =
                request?.UploadedFilePaths ??
                new List<string?>();

            if (submittedPaths.Count > 200)
            {
                return BadRequest(new
                {
                    message =
                        "A maximum of 200 temporary preview paths " +
                        "can be discarded at one time."
                });
            }

            var registeredPaths =
                ReadTemporaryInvoicePreviewPaths();

            var requestedRelativePaths =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            var invalidPathCount =
                0;

            var unregisteredPathCount =
                0;

            foreach (var submittedPath in submittedPaths)
            {
                if (
                    !_documentReferences
                        .TryNormalizeRelativePath(
                            submittedPath,
                            "invoices",
                            out var relativePath) ||
                    !relativePath.StartsWith(
                        "invoices/",
                        StringComparison.OrdinalIgnoreCase)
                )
                {
                    invalidPathCount++;

                    continue;
                }

                if (!registeredPaths.Contains(relativePath))
                {
                    unregisteredPathCount++;

                    continue;
                }

                requestedRelativePaths.Add(
                    relativePath);
            }

            var databasePathValues =
                await _context.Invoices
                    .AsNoTracking()
                    .Where(invoice =>
                        invoice.UploadedFilePath != null &&
                        invoice.UploadedFilePath != string.Empty)
                    .Select(invoice =>
                        invoice.UploadedFilePath)
                    .ToListAsync();

            var protectedRelativePaths =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (
                var databasePathValue
                in databasePathValues)
            {
                if (
                    _documentReferences
                        .TryNormalizeRelativePath(
                            databasePathValue,
                            "invoices",
                            out var protectedRelativePath)
                )
                {
                    protectedRelativePaths.Add(
                        protectedRelativePath);
                }
            }

            var publicInvoiceRoot =
                Path.GetFullPath(
                    Path.Combine(
                        _environment.WebRootPath,
                        "uploads",
                        "invoices"));

            var publicRootWithSeparator =
                publicInvoiceRoot.EndsWith(
                    Path.DirectorySeparatorChar.ToString(),
                    StringComparison.Ordinal)
                    ? publicInvoiceRoot
                    : publicInvoiceRoot +
                      Path.DirectorySeparatorChar;

            var deletedPreviewCount =
                0;

            var protectedPreviewCount =
                0;

            var missingPreviewCount =
                0;

            foreach (
                var relativePath
                in requestedRelativePaths)
            {
                registeredPaths.Remove(
                    relativePath);

                if (
                    protectedRelativePaths.Contains(
                        relativePath)
                )
                {
                    protectedPreviewCount++;

                    continue;
                }

                var deletedAnyFile =
                    false;

                if (
                    _privateDocumentStorage.Exists(
                        relativePath)
                )
                {
                    _privateDocumentStorage.DeleteIfExists(
                        relativePath);

                    deletedAnyFile =
                        true;
                }

                var storedFileName =
                    relativePath[
                        "invoices/".Length..];

                var publicFilePath =
                    Path.GetFullPath(
                        Path.Combine(
                            publicInvoiceRoot,
                            storedFileName));

                if (
                    !publicFilePath.StartsWith(
                        publicRootWithSeparator,
                        StringComparison.OrdinalIgnoreCase)
                )
                {
                    invalidPathCount++;

                    continue;
                }

                if (
                    System.IO.File.Exists(
                        publicFilePath)
                )
                {
                    System.IO.File.Delete(
                        publicFilePath);

                    deletedAnyFile =
                        true;
                }

                if (deletedAnyFile)
                {
                    deletedPreviewCount++;
                }
                else
                {
                    missingPreviewCount++;
                }
            }

            WriteTemporaryInvoicePreviewPaths(
                registeredPaths);

            return Ok(new
            {
                submittedPathCount =
                    submittedPaths.Count,

                uniqueRegisteredPathCount =
                    requestedRelativePaths.Count,

                deletedPreviewCount,

                protectedPreviewCount,

                missingPreviewCount,

                invalidPathCount,

                unregisteredPathCount
            });
        }

        private const string
            TemporaryInvoicePreviewSessionKey =
                "POTracker.InvoicePreviewTemporaryPaths";

        private void RegisterTemporaryInvoicePreview(
            string storedValue)
        {
            if (
                !_documentReferences
                    .TryNormalizeRelativePath(
                        storedValue,
                        "invoices",
                        out var relativePath) ||
                !relativePath.StartsWith(
                    "invoices/",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    "The temporary invoice preview path is invalid.");
            }

            var paths =
                ReadTemporaryInvoicePreviewPaths();

            paths.Add(
                relativePath);

            WriteTemporaryInvoicePreviewPaths(
                paths);
        }

        private HashSet<string>
            ReadTemporaryInvoicePreviewPaths()
        {
            var serializedPaths =
                HttpContext.Session.GetString(
                    TemporaryInvoicePreviewSessionKey);

            if (string.IsNullOrWhiteSpace(
                    serializedPaths))
            {
                return new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var storedPaths =
                    JsonSerializer.Deserialize<List<string>>(
                        serializedPaths) ??
                    new List<string>();

                return storedPaths
                    .Where(path =>
                        !string.IsNullOrWhiteSpace(path))
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                HttpContext.Session.Remove(
                    TemporaryInvoicePreviewSessionKey);

                return new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private void WriteTemporaryInvoicePreviewPaths(
            HashSet<string> paths)
        {
            if (paths.Count == 0)
            {
                HttpContext.Session.Remove(
                    TemporaryInvoicePreviewSessionKey);

                return;
            }

            HttpContext.Session.SetString(
                TemporaryInvoicePreviewSessionKey,
                JsonSerializer.Serialize(
                    paths.OrderBy(path => path)));
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
