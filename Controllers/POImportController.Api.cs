using Microsoft.AspNetCore.Mvc;
using POTracker.ViewModels;

namespace POTracker.Controllers
{
    public partial class POImportController
    {
        // ============================================================
        // ANGULAR API - PREVIEW SINGLE PO PDF
        // ============================================================

        [HttpPost("~/api/po-import/preview")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [RequestSizeLimit(20L * 1024L * 1024L)]
        [RequestFormLimits(
            ValueCountLimit = 10000,
            ValueLengthLimit = int.MaxValue,
            MultipartBodyLengthLimit = 20L * 1024L * 1024L)]
        public async Task<IActionResult> PreviewApi(
            [FromForm] IFormFile? pdfFile)
        {
            var result =
                await ProcessPdfFileAsync(pdfFile);

            if (!result.IsSuccessful ||
                result.PurchaseOrder == null)
            {
                return BadRequest(new
                {
                    message =
                        string.IsNullOrWhiteSpace(
                            result.ErrorMessage)
                            ? "Unable to extract the PO PDF."
                            : result.ErrorMessage
                });
            }

            return Ok(result);
        }

        // ============================================================
        // ANGULAR API - PREVIEW MULTIPLE PO PDF FILES
        // ============================================================

        [HttpPost("~/api/po-import/preview-batch")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [RequestSizeLimit(200L * 1024L * 1024L)]
        [RequestFormLimits(
            ValueCountLimit = 10000,
            ValueLengthLimit = int.MaxValue,
            MultipartBodyLengthLimit = 200L * 1024L * 1024L)]
        public async Task<IActionResult> PreviewBatchApi(
            [FromForm] List<IFormFile>? pdfFiles)
        {
            if (pdfFiles == null ||
                pdfFiles.Count == 0)
            {
                return BadRequest(new
                {
                    message =
                        "Please select one or more PO PDF files."
                });
            }

            if (pdfFiles.Count > MaxBatchFileCount)
            {
                return BadRequest(new
                {
                    message =
                        $"You can upload a maximum of " +
                        $"{MaxBatchFileCount} PO PDF files."
                });
            }

            var totalUploadSize =
                pdfFiles.Sum(file =>
                    file?.Length ?? 0);

            const long maximumTotalSize =
                200L * 1024L * 1024L;

            if (totalUploadSize >
                maximumTotalSize)
            {
                return BadRequest(new
                {
                    message =
                        "The combined upload size cannot exceed 200 MB."
                });
            }

            var model =
                new POBatchImportPreviewViewModel();

            foreach (var pdfFile in pdfFiles)
            {
                var result =
                    await ProcessPdfFileAsync(
                        pdfFile);

                model.Files.Add(result);
            }

            var successfulFiles =
                model.Files
                    .Where(file =>
                        file.IsSuccessful &&
                        file.PurchaseOrder != null &&
                        !string.IsNullOrWhiteSpace(
                            file.PurchaseOrder
                                .PurchaseOrderNo))
                    .ToList();

            var duplicateGroups =
                successfulFiles
                    .GroupBy(
                        file =>
                            file.PurchaseOrder!
                                .PurchaseOrderNo!
                                .Trim(),
                        StringComparer.OrdinalIgnoreCase)
                    .Where(group =>
                        group.Count() > 1)
                    .ToList();

            foreach (var duplicateGroup
                     in duplicateGroups)
            {
                foreach (var duplicateFile
                         in duplicateGroup.Skip(1))
                {
                    duplicateFile.IsDuplicateInBatch =
                        true;

                    duplicateFile.IsSelected =
                        false;

                    duplicateFile.ErrorMessage =
                        "The same PO number is already present " +
                        "in another selected PDF file.";
                }
            }

            return Ok(model);
        }

        // ============================================================
        // ANGULAR API - SAVE SINGLE PO
        // ============================================================

        [HttpPost("~/api/po-import/save")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<IActionResult> SaveApi(
            [FromBody]
            POImportPreviewViewModel model)
        {
            try
            {
                var result =
                    await SavePurchaseOrderAsync(
                        model);

                return Ok(new
                {
                    message =
                        result.WasCreated
                            ? "PO imported successfully."
                            : "Existing PO updated successfully.",

                    result.WasCreated,
                    result.PurchaseOrderId,
                    result.MaterialRowsSaved,
                    result.MatchedInvoiceRows
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    new
                    {
                        message =
                            "Unable to save the imported PO. " +
                            ex.Message
                    });
            }
        }

        // ============================================================
        // ANGULAR API - SAVE MULTIPLE PO FILES
        // ============================================================

        [HttpPost("~/api/po-import/save-batch")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<IActionResult> SaveBatchApi(
            [FromBody]
            POBatchImportPreviewViewModel model)
        {
            if (model.Files == null ||
                model.Files.Count == 0)
            {
                return BadRequest(new
                {
                    message =
                        "No batch PO files were available to save."
                });
            }

            var selectedFiles =
                model.Files
                    .Where(file =>
                        file.IsSelected)
                    .ToList();

            if (selectedFiles.Count == 0)
            {
                return BadRequest(new
                {
                    message =
                        "Please select at least one valid PO file."
                });
            }

            var batchResult =
                new POBatchImportResultViewModel
                {
                    BatchId = model.BatchId
                };

            var processedPurchaseOrderNumbers =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var file in selectedFiles)
            {
                var itemResult =
                    new POBatchImportResultItemViewModel
                    {
                        OriginalFileName =
                            file.OriginalFileName,

                        PurchaseOrderNo =
                            file.PurchaseOrder?
                                .PurchaseOrderNo
                    };

                if (!file.IsSuccessful)
                {
                    itemResult.Message =
                        string.IsNullOrWhiteSpace(
                            file.ErrorMessage)
                            ? "This PDF was not extracted successfully."
                            : file.ErrorMessage;

                    batchResult.Results.Add(
                        itemResult);

                    continue;
                }

                if (file.IsDuplicateInBatch)
                {
                    itemResult.Message =
                        "This PO was skipped because it is duplicated " +
                        "in the selected batch.";

                    batchResult.Results.Add(
                        itemResult);

                    continue;
                }

                if (file.PurchaseOrder == null)
                {
                    itemResult.Message =
                        "The extracted purchase order data is missing.";

                    batchResult.Results.Add(
                        itemResult);

                    continue;
                }

                var purchaseOrderNumber =
                    file.PurchaseOrder
                        .PurchaseOrderNo?
                        .Trim();

                if (string.IsNullOrWhiteSpace(
                        purchaseOrderNumber))
                {
                    itemResult.Message =
                        "Purchase order number is missing.";

                    batchResult.Results.Add(
                        itemResult);

                    continue;
                }

                if (!processedPurchaseOrderNumbers.Add(
                        purchaseOrderNumber))
                {
                    itemResult.Message =
                        "This PO was already processed in this batch.";

                    batchResult.Results.Add(
                        itemResult);

                    continue;
                }

                try
                {
                    var saveResult =
                        await SavePurchaseOrderAsync(
                            file.PurchaseOrder);

                    itemResult.IsImported =
                        saveResult.WasCreated;

                    itemResult.IsUpdated =
                        !saveResult.WasCreated;

                    itemResult.MaterialRowsSaved =
                        saveResult.MaterialRowsSaved;

                    itemResult.Message =
                        saveResult.WasCreated
                            ? $"PO imported successfully. " +
                              $"{saveResult.MaterialRowsSaved} material " +
                              $"row(s) saved. Pending invoices matched: " +
                              $"{saveResult.MatchedInvoiceRows}."
                            : $"Existing PO updated successfully. " +
                              $"{saveResult.MaterialRowsSaved} material " +
                              $"row(s) saved. Pending invoices matched: " +
                              $"{saveResult.MatchedInvoiceRows}.";
                }
                catch (Exception ex)
                {
                    _context.ChangeTracker.Clear();

                    itemResult.Message =
                        "Unable to save this PO. " +
                        ex.Message;
                }

                batchResult.Results.Add(
                    itemResult);
            }

            return Ok(batchResult);
        }
    }
}
