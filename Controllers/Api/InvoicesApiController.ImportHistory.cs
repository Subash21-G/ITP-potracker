using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace POTracker.Controllers.Api
{
    public sealed partial class InvoicesApiController
    {
        // ============================================================
        // IMPORT-HISTORY BULK DELETE
        // Deletes audit history only.
        // Invoices and material quantities are not changed.
        // ============================================================

        [HttpPost("import-history/bulk-delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<
            InvoiceImportHistoryBulkDeleteResponseDto>>
            BulkDeleteImportHistory(
                InvoiceImportHistoryBulkDeleteRequestDto? request)
        {
            var validIds = (request?.BatchIds ?? new())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (validIds.Count == 0)
            {
                return BadRequest(
                    new InvoiceImportHistoryBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Select at least one invoice import-history record."
                    });
            }

            if (validIds.Count > 200)
            {
                return BadRequest(
                    new InvoiceImportHistoryBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "A maximum of 200 invoice import-history " +
                            "records can be deleted at one time."
                    });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var batches = await _context
                    .VyaparInvoiceImportBatches
                    .Where(batch =>
                        validIds.Contains(batch.Id))
                    .ToListAsync();

                if (batches.Count == 0)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        new InvoiceImportHistoryBulkDeleteResponseDto
                        {
                            Success = false,
                            Message =
                                "The selected invoice import-history " +
                                "records were not found."
                        });
                }

                var foundIds = batches
                    .Select(batch => batch.Id)
                    .ToList();

                var rows = await _context
                    .VyaparInvoiceImportRows
                    .Where(row =>
                        foundIds.Contains(row.BatchId))
                    .ToListAsync();

                if (rows.Count > 0)
                {
                    _context.VyaparInvoiceImportRows
                        .RemoveRange(rows);
                }

                _context.VyaparInvoiceImportBatches
                    .RemoveRange(batches);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(
                    new InvoiceImportHistoryBulkDeleteResponseDto
                    {
                        Success = true,
                        Message =
                            $"{batches.Count} invoice import-history " +
                            "record(s) were deleted successfully. " +
                            "Actual invoices and material quantities " +
                            "were not changed.",
                        DeletedBatchCount = batches.Count,
                        DeletedRowCount = rows.Count,
                        DeletedBatchIds = foundIds
                    });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new InvoiceImportHistoryBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Unable to delete the selected invoice " +
                            "import-history records. No partial " +
                            "deletion was saved."
                    });
            }
        }

        // ============================================================
        // IMPORT-HISTORY BATCH REPORT
        // ============================================================

        [HttpGet("import-history/{batchId:int}/report")]
        public async Task<ActionResult<
            InvoiceImportReportResponseDto>> ImportHistoryReport(
                int batchId,
                int page = 1,
                int pageSize = 50)
        {
            if (batchId <= 0)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "Invalid invoice import-history batch ID."
                    });
            }

            page = Math.Max(page, 1);

            var allowedPageSizes =
                new[] { 25, 50, 100, 200 };

            if (!allowedPageSizes.Contains(pageSize))
            {
                pageSize = 50;
            }

            var batch = await _context
                .VyaparInvoiceImportBatches
                .AsNoTracking()
                .Where(item => item.Id == batchId)
                .Select(item => new
                {
                    item.Id,
                    item.FileName,
                    item.ImportDate
                })
                .FirstOrDefaultAsync();

            if (batch == null)
            {
                return NotFound(
                    new
                    {
                        message =
                            "Invoice import-history batch was not found."
                    });
            }

            var query = _context
                .VyaparInvoiceImportRows
                .AsNoTracking()
                .Where(row => row.BatchId == batchId);

            var summaryData = await query
                .GroupBy(row => 1)
                .Select(group => new
                {
                    TotalRows = group.Count(),
                    SuccessRows = group.Count(row =>
                        row.Status == "Success"),
                    DeletedRows = group.Count(row =>
                        row.Status == "Deleted"),
                    PendingRows = group.Count(row =>
                        row.Status == null ||
                        (row.Status != "Success" &&
                         row.Status != "Deleted")),
                    TotalInvoiceQuantity =
                        group.Sum(row => row.InvoiceQty)
                })
                .FirstOrDefaultAsync();

            var totalRows =
                summaryData?.TotalRows ?? 0;

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalRows / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var databaseRows = await query
                .OrderBy(row => row.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(row => new
                {
                    row.Id,
                    row.PONumber,
                    row.LineItem,
                    row.MaterialNumber,
                    row.InvoiceNumber,
                    row.InvoiceQty,
                    row.Status,
                    row.ErrorMessage
                })
                .ToListAsync();

            var items = databaseRows
                .Select(row =>
                    new InvoiceImportReportRowDto
                    {
                        Id = row.Id,
                        PONumber =
                            row.PONumber ?? string.Empty,
                        LineItem =
                            row.LineItem ?? string.Empty,
                        MaterialNumber =
                            row.MaterialNumber ?? string.Empty,
                        InvoiceNumber =
                            row.InvoiceNumber ?? string.Empty,
                        InvoiceQuantity =
                            row.InvoiceQty,
                        Status =
                            NormalizeImportHistoryStatus(
                                row.Status),
                        Message =
                            BuildImportHistoryMessage(
                                row.Status,
                                row.ErrorMessage)
                    })
                .ToList();

            var successRows =
                summaryData?.SuccessRows ?? 0;

            var pendingRows =
                summaryData?.PendingRows ?? 0;

            return Ok(
                new InvoiceImportReportResponseDto
                {
                    BatchId = batch.Id,
                    FileName =
                        batch.FileName ?? string.Empty,
                    ImportDate = batch.ImportDate,
                    Summary =
                        new InvoiceImportReportSummaryDto
                        {
                            TotalRows = totalRows,
                            SuccessRows = successRows,
                            PendingRows = pendingRows,
                            DeletedRows =
                                summaryData?.DeletedRows ?? 0,
                            TotalInvoiceQuantity =
                                summaryData
                                    ?.TotalInvoiceQuantity ?? 0m,
                            SuccessPercent =
                                CalculateImportHistoryPercent(
                                    successRows,
                                    totalRows),
                            PendingPercent =
                                CalculateImportHistoryPercent(
                                    pendingRows,
                                    totalRows)
                        },
                    Items = items,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                });
        }

        private static string NormalizeImportHistoryStatus(
            string? status)
        {
            if (string.Equals(
                    status,
                    "Success",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Success";
            }

            if (string.Equals(
                    status,
                    "Deleted",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Deleted";
            }

            return "Pending";
        }

        private static string BuildImportHistoryMessage(
            string? status,
            string? errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                return errorMessage.Trim();
            }

            var normalizedStatus =
                NormalizeImportHistoryStatus(status);

            return normalizedStatus switch
            {
                "Success" =>
                    "Imported successfully.",

                "Deleted" =>
                    "The related invoice was deleted.",

                _ =>
                    "Stored as Pending for review or " +
                    "automatic matching."
            };
        }

        private static int CalculateImportHistoryPercent(
            int value,
            int total)
        {
            if (total <= 0)
            {
                return 0;
            }

            return Convert.ToInt32(
                Math.Round(
                    (decimal)value / total * 100m));
        }
    }
}