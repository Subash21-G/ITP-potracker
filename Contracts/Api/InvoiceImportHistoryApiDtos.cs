using System;
using System.Collections.Generic;

namespace POTracker.Contracts.Api
{
    /// <summary>
    /// IDs of import-history batches selected for deletion.
    /// This deletes audit history only.
    /// </summary>
    public sealed class InvoiceImportHistoryBulkDeleteRequestDto
    {
        public List<int> BatchIds { get; init; } = new();
    }

    /// <summary>
    /// Result returned after deleting multiple import-history batches.
    /// </summary>
    public sealed class InvoiceImportHistoryBulkDeleteResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int DeletedBatchCount { get; init; }

        public int DeletedRowCount { get; init; }

        public List<int> DeletedBatchIds { get; init; } = new();
    }

    /// <summary>
    /// Summary information for one invoice import report.
    /// </summary>
    public sealed class InvoiceImportReportSummaryDto
    {
        public int TotalRows { get; init; }

        public int SuccessRows { get; init; }

        public int PendingRows { get; init; }

        public int DeletedRows { get; init; }

        public decimal TotalInvoiceQuantity { get; init; }

        public int SuccessPercent { get; init; }

        public int PendingPercent { get; init; }
    }

    /// <summary>
    /// One row displayed in an invoice import report.
    /// </summary>
    public sealed class InvoiceImportReportRowDto
    {
        public int Id { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public string LineItem { get; init; } = string.Empty;

        public string MaterialNumber { get; init; } = string.Empty;

        public string InvoiceNumber { get; init; } = string.Empty;

        public decimal InvoiceQuantity { get; init; }

        public string Status { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// Paginated report for one invoice import-history batch.
    /// </summary>
    public sealed class InvoiceImportReportResponseDto
    {
        public int BatchId { get; init; }

        public string FileName { get; init; } = string.Empty;

        public DateTime ImportDate { get; init; }

        public InvoiceImportReportSummaryDto Summary { get; init; } =
            new();

        public List<InvoiceImportReportRowDto> Items { get; init; } =
            new();

        public int CurrentPage { get; init; }

        public int PageSize { get; init; }

        public int TotalPages { get; init; }
    }
}