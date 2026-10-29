using System;
using System.Collections.Generic;

namespace POTracker.Contracts.Api
{
    /// <summary>
    /// One invoice row displayed in the Angular invoice list.
    /// </summary>
    public sealed class InvoiceListItemDto
    {
        public int Id { get; init; }

        public string InvoiceNumber { get; init; } = string.Empty;

        public DateTime? InvoiceDate { get; init; }

        public string CustomerName { get; init; } = string.Empty;

        public string UploadedFileName { get; init; } = string.Empty;

        public DateTime CreatedDate { get; init; }

        public int ItemCount { get; init; }

        public decimal TotalQuantity { get; init; }

        public decimal TotalGstAmount { get; init; }

        public decimal TotalAmount { get; init; }
    }

    /// <summary>
    /// Summary cards displayed above the Angular invoice list.
    /// </summary>
    public sealed class InvoiceListSummaryDto
    {
        public int TotalInvoices { get; init; }

        public int TotalItems { get; init; }

        public decimal TotalQuantity { get; init; }

        public decimal TotalGstAmount { get; init; }

        public decimal TotalInvoiceAmount { get; init; }

        public int PendingItemCount { get; init; }
    }

    /// <summary>
    /// Paginated invoice list response.
    /// </summary>
    public sealed class InvoiceListResponseDto
    {
        public List<InvoiceListItemDto> Items { get; init; } = new();

        public InvoiceListSummaryDto Summary { get; init; } = new();

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalCount { get; init; }

        public int TotalPages { get; init; }
    }

    /// <summary>
    /// One item inside an invoice details page.
    /// </summary>
    public sealed class InvoiceItemApiDto
    {
        public int Id { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public string LineItem { get; init; } = string.Empty;

        public string MaterialNo { get; init; } = string.Empty;

        public string POMaterialNo { get; init; } = string.Empty;

        public bool MaterialNumbersMatch { get; set; }

        public bool IsPending { get; init; }

        public decimal DispatchedQty { get; set; }

        public string DispatchStatus { get; set; } = string.Empty;

        public List<InvoiceDispatchApiDto> Dispatches { get; set; } = new();

        public decimal Qty { get; init; }

        public decimal PriceUnit { get; init; }

        public decimal GstAmount { get; init; }

        public decimal Amount { get; init; }

        public DateTime? PODate { get; init; }
    }

    public sealed class InvoiceDispatchApiDto
    {
        public string DispatchNo { get; init; } = string.Empty;

        public decimal DispatchQty { get; init; }
    }

    /// <summary>
    /// Full invoice details returned to Angular.
    /// </summary>
    public sealed class InvoiceDetailsApiDto
    {
        public int Id { get; init; }

        public string InvoiceNumber { get; init; } = string.Empty;

        public DateTime? InvoiceDate { get; init; }

        public string CustomerName { get; init; } = string.Empty;

        public string UploadedFileName { get; init; } = string.Empty;

        public string UploadedFilePath { get; init; } = string.Empty;

        public DateTime CreatedDate { get; init; }

        public decimal TotalQuantity { get; init; }

        public decimal TotalGstAmount { get; init; }

        public decimal TotalAmount { get; init; }

        public List<InvoiceItemApiDto> Items { get; init; } = new();

    }

    /// <summary>
    /// One Vyapar import batch displayed in Angular import history.
    /// </summary>
    public sealed class InvoiceImportBatchDto
    {
        public int Id { get; init; }

        public string FileName { get; init; } = string.Empty;

        public DateTime ImportDate { get; init; }

        public int TotalRows { get; init; }

        public int SuccessRows { get; init; }

        public int FailedRows { get; init; }

        public int PendingRows { get; init; }

        public int DeletedRows { get; init; }
    }

    /// <summary>
    /// One unmatched invoice row waiting for a material match.
    /// </summary>
    public sealed class PendingInvoiceItemApiDto
    {
        public int Id { get; init; }

        public int InvoiceId { get; init; }

        public string InvoiceNumber { get; init; } = string.Empty;

        public DateTime? InvoiceDate { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public string LineItem { get; init; } = string.Empty;

        public string MaterialNo { get; init; } = string.Empty;

        public string CustomerName { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string HsnSacCode { get; init; } = string.Empty;

        public decimal Qty { get; init; }

        public decimal PriceUnit { get; init; }

        public decimal GstAmount { get; init; }

        public decimal Amount { get; init; }

        public string ErrorMessage { get; init; } = string.Empty;

        public DateTime CreatedDate { get; init; }
    }

    /// <summary>
    /// Standard response returned after importing an invoice Excel file.
    /// </summary>
    public sealed class InvoiceImportResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int BatchId { get; init; }

        public int TotalRows { get; init; }

        public int SuccessRows { get; init; }

        public int FailedRows { get; init; }

        public int PendingRows { get; init; }

        }


    /// <summary>
    /// One PO material candidate shown for manual pending-row matching.
    /// </summary>
    public sealed class PendingInvoiceMatchCandidateDto
    {
        public int MaterialId { get; init; }

        public int PurchaseOrderId { get; init; }

        public string PONumber { get; init; } = string.Empty;

        public string LineItem { get; init; } = string.Empty;

        public string MaterialNo { get; init; } = string.Empty;

        public string ShortDescription { get; init; } = string.Empty;

        public decimal POQty { get; init; }

        public decimal InvoiceQty { get; init; }

        public decimal BalanceQty { get; init; }

        public decimal DispatchQty { get; init; }

        public decimal PendingQty { get; init; }

        public int MatchScore { get; init; }

        public bool IsExactPO { get; init; }

        public bool IsExactMaterial { get; init; }

        public bool IsExactLineItem { get; init; }

        public bool HasEnoughBalance { get; init; }
    }

    /// <summary>
    /// Candidate response for one pending invoice row.
    /// </summary>
    public sealed class PendingInvoiceMatchCandidatesResponseDto
    {
        public PendingInvoiceItemApiDto PendingItem { get; init; } = new();

        public List<PendingInvoiceMatchCandidateDto> Candidates { get; init; } =
            new();
    }

    /// <summary>
    /// Manual pending-row match request.
    /// </summary>
    public sealed class PendingInvoiceManualMatchRequestDto
    {
        public int MaterialId { get; init; }

        public bool ConfirmMismatch { get; init; }
    }

    /// <summary>
    /// Result returned after a manual pending-row match.
    /// </summary>
    public sealed class PendingInvoiceManualMatchResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int PendingItemId { get; init; }

        public int MaterialId { get; init; }

        public int InvoiceId { get; init; }
    }

    /// <summary>
    /// Result returned after retrying all pending invoice matches.
    /// </summary>
    public sealed class PendingInvoiceRetryResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int PendingBefore { get; init; }

        public int MatchedRows { get; init; }

        public int RemainingRows { get; init; }
    }

    /// <summary>
    /// Standard response returned after deleting an invoice.
    /// </summary>
    public sealed class InvoiceDeleteResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int InvoiceId { get; init; }
    }

    /// <summary>
    /// Standard response returned after deleting an import batch.
    /// </summary>
    public sealed class InvoiceImportBatchDeleteResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int BatchId { get; init; }
    }
    /// <summary>
    /// Invoice IDs selected for bulk deletion.
    /// </summary>
    public sealed class InvoiceBulkDeleteRequestDto
    {
        public List<int> InvoiceIds { get; init; } = new();
    }

    /// <summary>
    /// Result returned after deleting multiple invoices.
    /// </summary>
    public sealed class InvoiceBulkDeleteResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int RequestedCount { get; init; }

        public int DeletedCount { get; init; }

        public int MissingCount { get; init; }

        public List<int> DeletedInvoiceIds { get; init; } = new();
    }
}
