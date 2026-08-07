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
        [POTracker.Security.ValidateApiAntiforgeryToken]
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
                string? search,
                string? status,
                string? poNumber,
                string? lineItem,
                string? materialNumber,
                string? invoiceNumber,
                decimal? minInvoiceQuantity,
                decimal? maxInvoiceQuantity,
                string? columnStatus,
                string? message,
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

            var normalizedSearch =
                CleanImportHistoryFilter(
                    search);

            if (normalizedSearch != null)
            {
                var searchMatchesSuccess =
                    "Success".Contains(
                        normalizedSearch,
                        StringComparison.OrdinalIgnoreCase);

                var searchMatchesPending =
                    "Pending".Contains(
                        normalizedSearch,
                        StringComparison.OrdinalIgnoreCase);

                var searchMatchesDeleted =
                    "Deleted".Contains(
                        normalizedSearch,
                        StringComparison.OrdinalIgnoreCase);

                var searchMatchesSuccessMessage =
                    "Imported successfully.".Contains(
                        normalizedSearch,
                        StringComparison.OrdinalIgnoreCase);

                var searchMatchesDeletedMessage =
                    "The related invoice was deleted.".Contains(
                        normalizedSearch,
                        StringComparison.OrdinalIgnoreCase);

                var searchMatchesPendingMessage =
                    (
                        "Stored as Pending for review or " +
                        "automatic matching."
                    ).Contains(
                        normalizedSearch,
                        StringComparison.OrdinalIgnoreCase);

                query =
                    query.Where(row =>
                        row.PONumber.Contains(
                            normalizedSearch) ||
                        row.LineItem.Contains(
                            normalizedSearch) ||
                        row.MaterialNumber.Contains(
                            normalizedSearch) ||
                        row.InvoiceNumber.Contains(
                            normalizedSearch) ||
                        (
                            row.ErrorMessage != null &&
                            row.ErrorMessage.Contains(
                                normalizedSearch)
                        ) ||
                        (
                            searchMatchesSuccess &&
                            row.Status == "Success"
                        ) ||
                        (
                            searchMatchesDeleted &&
                            row.Status == "Deleted"
                        ) ||
                        (
                            searchMatchesPending &&
                            (
                                row.Status == null ||
                                (
                                    row.Status != "Success" &&
                                    row.Status != "Deleted"
                                )
                            )
                        ) ||
                        (
                            searchMatchesSuccessMessage &&
                            (
                                row.ErrorMessage == null ||
                                row.ErrorMessage == string.Empty
                            ) &&
                            row.Status == "Success"
                        ) ||
                        (
                            searchMatchesDeletedMessage &&
                            (
                                row.ErrorMessage == null ||
                                row.ErrorMessage == string.Empty
                            ) &&
                            row.Status == "Deleted"
                        ) ||
                        (
                            searchMatchesPendingMessage &&
                            (
                                row.ErrorMessage == null ||
                                row.ErrorMessage == string.Empty
                            ) &&
                            (
                                row.Status == null ||
                                (
                                    row.Status != "Success" &&
                                    row.Status != "Deleted"
                                )
                            )
                        ));
            }

            query =
                ApplyImportHistoryStatusFilter(
                    query,
                    status);

            var normalizedPONumber =
                CleanImportHistoryFilter(
                    poNumber);

            if (normalizedPONumber != null)
            {
                query =
                    query.Where(row =>
                        row.PONumber.Contains(
                            normalizedPONumber));
            }

            var normalizedLineItem =
                CleanImportHistoryFilter(
                    lineItem);

            if (normalizedLineItem != null)
            {
                query =
                    query.Where(row =>
                        row.LineItem.Contains(
                            normalizedLineItem));
            }

            var normalizedMaterialNumber =
                CleanImportHistoryFilter(
                    materialNumber);

            if (normalizedMaterialNumber != null)
            {
                query =
                    query.Where(row =>
                        row.MaterialNumber.Contains(
                            normalizedMaterialNumber));
            }

            var normalizedInvoiceNumber =
                CleanImportHistoryFilter(
                    invoiceNumber);

            if (normalizedInvoiceNumber != null)
            {
                query =
                    query.Where(row =>
                        row.InvoiceNumber.Contains(
                            normalizedInvoiceNumber));
            }

            if (minInvoiceQuantity.HasValue)
            {
                query =
                    query.Where(row =>
                        row.InvoiceQty >=
                        minInvoiceQuantity.Value);
            }

            if (maxInvoiceQuantity.HasValue)
            {
                query =
                    query.Where(row =>
                        row.InvoiceQty <=
                        maxInvoiceQuantity.Value);
            }

            query =
                ApplyImportHistoryStatusFilter(
                    query,
                    columnStatus);

            query =
                ApplyImportHistoryMessageFilter(
                    query,
                    message);

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

        private static IQueryable<
            POTracker.Models.VyaparInvoiceImportRow>
            ApplyImportHistoryStatusFilter(
                IQueryable<
                    POTracker.Models.VyaparInvoiceImportRow> query,
                string? value)
        {
            var normalized =
                CleanImportHistoryFilter(
                    value)?
                    .ToLowerInvariant();

            return normalized switch
            {
                "success" =>
                    query.Where(row =>
                        row.Status == "Success"),

                "deleted" =>
                    query.Where(row =>
                        row.Status == "Deleted"),

                "pending" =>
                    query.Where(row =>
                        row.Status == null ||
                        (
                            row.Status != "Success" &&
                            row.Status != "Deleted"
                        )),

                _ =>
                    query
            };
        }

        private static IQueryable<
            POTracker.Models.VyaparInvoiceImportRow>
            ApplyImportHistoryMessageFilter(
                IQueryable<
                    POTracker.Models.VyaparInvoiceImportRow> query,
                string? value)
        {
            var normalized =
                CleanImportHistoryFilter(
                    value);

            if (normalized == null)
            {
                return query;
            }

            var matchesSuccessFallback =
                "Imported successfully.".Contains(
                    normalized,
                    StringComparison.OrdinalIgnoreCase);

            var matchesDeletedFallback =
                "The related invoice was deleted.".Contains(
                    normalized,
                    StringComparison.OrdinalIgnoreCase);

            var matchesPendingFallback =
                (
                    "Stored as Pending for review or " +
                    "automatic matching."
                ).Contains(
                    normalized,
                    StringComparison.OrdinalIgnoreCase);

            return query.Where(row =>
                (
                    row.ErrorMessage != null &&
                    row.ErrorMessage.Contains(
                        normalized)
                ) ||
                (
                    matchesSuccessFallback &&
                    (
                        row.ErrorMessage == null ||
                        row.ErrorMessage == string.Empty
                    ) &&
                    row.Status == "Success"
                ) ||
                (
                    matchesDeletedFallback &&
                    (
                        row.ErrorMessage == null ||
                        row.ErrorMessage == string.Empty
                    ) &&
                    row.Status == "Deleted"
                ) ||
                (
                    matchesPendingFallback &&
                    (
                        row.ErrorMessage == null ||
                        row.ErrorMessage == string.Empty
                    ) &&
                    (
                        row.Status == null ||
                        (
                            row.Status != "Success" &&
                            row.Status != "Deleted"
                        )
                    )
                ));
        }

        private static string? CleanImportHistoryFilter(
            string? value)
        {
            var cleaned =
                value?.Trim();

            return string.IsNullOrWhiteSpace(
                cleaned)
                    ? null
                    : cleaned;
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