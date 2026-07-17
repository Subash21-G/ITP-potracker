namespace POTracker.ViewModels
{
    // ============================================================
    // BULK INVOICE IMPORT PREVIEW
    // ============================================================

    public class InvoiceBatchImportPreviewViewModel
    {
        public string BatchId { get; set; }
            = Guid.NewGuid().ToString("N");

        public List<InvoiceBatchImportFileViewModel> Files { get; set; }
            = new();

        public int TotalFiles =>
            Files.Count;

        public int SuccessfulFiles =>
            Files.Count(x => x.IsSuccessful);

        public int FailedFiles =>
            Files.Count(x => !x.IsSuccessful);

        public int DuplicateFiles =>
            Files.Count(x => x.IsDuplicateInBatch);

        public int ExistingInvoiceFiles =>
            Files.Count(x => x.ExistsInDatabase);

        public int TotalInvoiceRows =>
            Files
                .Where(x =>
                    x.IsSuccessful &&
                    x.Invoice != null)
                .Sum(x =>
                    x.Invoice!.Items?.Count ?? 0);
    }

    // ============================================================
    // INDIVIDUAL INVOICE FILE PREVIEW
    // ============================================================

    public class InvoiceBatchImportFileViewModel
    {
        public string BatchItemId { get; set; }
            = Guid.NewGuid().ToString("N");

        public string OriginalFileName { get; set; }
            = string.Empty;

        public long FileSize { get; set; }

        public bool IsSuccessful { get; set; }

        public bool IsSelected { get; set; } = true;

        public bool IsDuplicateInBatch { get; set; }

        public bool ExistsInDatabase { get; set; }

        public string? ErrorMessage { get; set; }

        public InvoiceImportPreviewViewModel? Invoice { get; set; }
    }

    // ============================================================
    // BULK INVOICE FINAL RESULT
    // ============================================================

    public class InvoiceBatchImportResultViewModel
    {
        public string BatchId { get; set; }
            = string.Empty;

        public List<InvoiceBatchImportResultItemViewModel> Results { get; set; }
            = new();

        public int TotalProcessed =>
            Results.Count;

        public int ImportedCount =>
            Results.Count(x => x.IsImported);

        public int UpdatedCount =>
            Results.Count(x => x.IsUpdated);

        public int FailedCount =>
            Results.Count(x =>
                !x.IsImported &&
                !x.IsUpdated);

        public int TotalRowsSaved =>
            Results.Sum(x => x.RowsSaved);
    }

    // ============================================================
    // INDIVIDUAL INVOICE SAVE RESULT
    // ============================================================

    public class InvoiceBatchImportResultItemViewModel
    {
        public string OriginalFileName { get; set; }
            = string.Empty;

        public string? InvoiceNumber { get; set; }

        public bool IsImported { get; set; }

        public bool IsUpdated { get; set; }

        public int RowsSaved { get; set; }

        public string Message { get; set; }
            = string.Empty;
    }
}