using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Services;
using POTracker.ViewModels;

namespace POTracker.Controllers
{
    public partial class ImportController
    {
        // ============================================================
        // ANGULAR API - DOWNLOAD MATERIAL IMPORT TEMPLATE
        // ============================================================

        [HttpGet("~/api/material-import/template")]
        [Produces(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult DownloadTemplateApi()
        {
            if (!IsLoggedIn())
            {
                return Unauthorized(new
                {
                    message =
                        "Your session has expired. Sign in again."
                });
            }

            var fileContent =
                ExcelImportTemplateService
                    .CreateMaterialImportTemplate(
                        MaximumRowsPerImport);

            return File(
                fileContent,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "POTracker_Material_Import_Template.xlsx");
        }

        // ============================================================
        // ANGULAR API - UPLOAD AND PREVIEW MATERIAL EXCEL
        // ============================================================

        [HttpPost("~/api/material-import/preview")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
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

            if (excelFile == null ||
                excelFile.Length == 0)
            {
                return BadRequest(new
                {
                    message =
                        "Please select an Excel file."
                });
            }

            if (excelFile.Length > MaximumExcelFileSize)
            {
                return BadRequest(new
                {
                    message =
                        "The Excel file is too large. Maximum allowed size is 20 MB."
                });
            }

            var extension =
                Path.GetExtension(
                    excelFile.FileName);

            var allowedExtensions =
                new[]
                {
                    ".xlsx",
                    ".xlsm"
                };

            if (!allowedExtensions.Contains(
                    extension,
                    StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        "Only .xlsx and .xlsm Excel files are supported."
                });
            }

            try
            {
                await using var uploadStream =
                    new MemoryStream();

                await excelFile.CopyToAsync(
                    uploadStream);

                uploadStream.Position = 0;

                using var workbook =
                    new XLWorkbook(
                        uploadStream);

                var worksheet =
                    workbook.Worksheets
                        .FirstOrDefault();

                if (worksheet == null)
                {
                    return BadRequest(new
                    {
                        message =
                            "The Excel workbook does not contain any worksheet."
                    });
                }

                var headerMap =
                    BuildHeaderMap(
                        worksheet);

                var missingHeaders =
                    GetMissingRequiredHeaders(
                        headerMap);

                if (missingHeaders.Count > 0)
                {
                    return BadRequest(new
                    {
                        message =
                            "Missing required Excel columns: " +
                            string.Join(
                                ", ",
                                missingHeaders) +
                            "."
                    });
                }

                var lastRow =
                    worksheet
                        .LastRowUsed()
                        ?.RowNumber() ?? 1;

                var dataRowCount =
                    Math.Max(
                        0,
                        lastRow - 1);

                if (dataRowCount == 0)
                {
                    return BadRequest(new
                    {
                        message =
                            "The Excel file does not contain any material rows."
                    });
                }

                if (dataRowCount > MaximumRowsPerImport)
                {
                    return BadRequest(new
                    {
                        message =
                            $"A maximum of {MaximumRowsPerImport} rows can be imported at one time. " +
                            $"This file contains approximately {dataRowCount} data rows."
                    });
                }

                var materialRows =
                    new List<MaterialImportRowViewModel>();

                for (var rowNumber = 2;
                     rowNumber <= lastRow;
                     rowNumber++)
                {
                    if (IsRowEmpty(
                            worksheet,
                            rowNumber,
                            headerMap))
                    {
                        continue;
                    }

                    var materialRow =
                        ParseExcelRow(
                            worksheet,
                            rowNumber,
                            headerMap);

                    ValidateRow(
                        materialRow);

                    materialRows.Add(
                        materialRow);
                }

                if (materialRows.Count == 0)
                {
                    return BadRequest(new
                    {
                        message =
                            "No non-empty material rows were found in the Excel file."
                    });
                }

                ValidateDuplicateRows(
                    materialRows);

                ValidatePurchaseOrderConsistency(
                    materialRows);

                var batch =
                    new MaterialImportBatchViewModel
                    {
                        BatchId =
                            Guid.NewGuid()
                                .ToString("N"),

                        OriginalFileName =
                            Path.GetFileName(
                                excelFile.FileName),

                        CreatedAt =
                            DateTime.Now,

                        Rows =
                            materialRows
                    };

                await SaveBatchAsync(
                    batch);

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
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message =
                        "Unable to process the Excel file. " +
                        ex.Message
                });
            }
        }

        // ============================================================
        // ANGULAR API - COMMIT MATERIAL IMPORT
        // ============================================================

        [HttpPost("~/api/material-import/commit")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
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

            var batch =
                await LoadBatchAsync(
                    request.BatchId);

            if (batch == null)
            {
                return BadRequest(new
                {
                    message =
                        "The import batch has expired or could not be found. Please upload the file again."
                });
            }

            if (batch.Rows.Count == 0)
            {
                return BadRequest(new
                {
                    message =
                        "The import batch does not contain any material rows."
                });
            }

            if (batch.Rows.Any(row =>
                    !row.IsValid))
            {
                return BadRequest(new
                {
                    message =
                        "This import contains invalid rows. Correct the Excel file and upload it again."
                });
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var purchaserMap =
                    await CreateOrUpdatePurchasersAsync(
                        batch.Rows);

                var purchaseOrderMap =
                    await CreateOrUpdatePurchaseOrdersAsync(
                        batch.Rows,
                        purchaserMap);

                await _context.SaveChangesAsync();

                var purchaseOrderIds =
                    purchaseOrderMap.Values
                        .Select(purchaseOrder =>
                            purchaseOrder.Id)
                        .Distinct()
                        .ToList();

                var existingMaterials =
                    await _context.Materials
                        .Where(material =>
                            purchaseOrderIds.Contains(
                                material.PurchaseOrderId))
                        .ToListAsync();

                var materialMap =
                    existingMaterials
                        .GroupBy(material =>
                            CreateMaterialKey(
                                material.PurchaseOrderId,
                                material.LineItem,
                                material.MaterialNo))
                        .ToDictionary(
                            group =>
                                group.Key,
                            group =>
                                group.First(),
                            StringComparer.OrdinalIgnoreCase);

                var createdCount = 0;
                var updatedCount = 0;

                foreach (var row in batch.Rows)
                {
                    var poKey =
                        NormalizeKey(
                            row.PONumber);

                    var purchaseOrder =
                        purchaseOrderMap[poKey];

                    var lineItem =
                        NormalizeLineItem(
                            row.LineItem,
                            row.RowNumber);

                    var materialKey =
                        CreateMaterialKey(
                            purchaseOrder.Id,
                            lineItem,
                            row.MaterialNo);

                    if (materialMap.TryGetValue(
                            materialKey,
                            out var material))
                    {
                        ApplyRowToExistingMaterial(
                            material,
                            row,
                            purchaseOrder.Id,
                            lineItem);

                        _context.Materials.Update(
                            material);

                        updatedCount++;
                    }
                    else
                    {
                        material =
                            CreateMaterial(
                                row,
                                purchaseOrder.Id,
                                lineItem);

                        _context.Materials.Add(
                            material);

                        materialMap[materialKey] =
                            material;

                        createdCount++;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var matchedInvoiceRows = 0;

                foreach (var purchaseOrderId
                         in purchaseOrderIds)
                {
                    try
                    {
                        matchedInvoiceRows +=
                            await _pendingInvoiceMatcher
                                .MatchPendingInvoicesForPurchaseOrderAsync(
                                    purchaseOrderId);
                    }
                    catch
                    {
                        // Material import remains successful when an
                        // optional pending-invoice match fails.
                    }
                }

                DeleteBatchFile(
                    batch.BatchId);

                var message =
                    $"Material import completed. Created: {createdCount}, " +
                    $"Updated: {updatedCount}, Pending invoices matched: {matchedInvoiceRows}.";

                return Ok(new
                {
                    message,

                    batchId =
                        batch.BatchId,

                    createdCount,
                    updatedCount,
                    matchedInvoiceRows
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return BadRequest(new
                {
                    message =
                        "Material import failed. No partial import was saved. " +
                        ex.Message
                });
            }
        }

        // ============================================================
        // ANGULAR API - CANCEL MATERIAL IMPORT PREVIEW
        // ============================================================

        [HttpDelete("~/api/material-import/{batchId}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
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
