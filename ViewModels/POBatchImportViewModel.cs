namespace POTracker.ViewModels
{
    // ============================================================
    // MULTIPLE PO FILES PREVIEW
    // ============================================================
    public class POBatchImportPreviewViewModel
    {
        public string BatchId { get; set; }
            = Guid.NewGuid().ToString("N");

        public List<POBatchImportFileViewModel> Files { get; set; }
            = new();

        public int TotalFiles =>
            Files.Count;

        public int SuccessfulFiles =>
            Files.Count(x => x.IsSuccessful);

        public int FailedFiles =>
            Files.Count(x => !x.IsSuccessful);

        public int DuplicateFiles =>
            Files.Count(x => x.IsDuplicateInBatch);

        public int ExistingPOFiles =>
            Files.Count(x => x.ExistsInDatabase);

        public int TotalMaterialRows =>
            Files
                .Where(x =>
                    x.IsSuccessful &&
                    x.PurchaseOrder != null)
                .Sum(x =>
                    x.PurchaseOrder!.Items?.Count ?? 0);
    }

    // ============================================================
    // INDIVIDUAL PDF FILE RESULT
    // ============================================================
    public class POBatchImportFileViewModel
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

        // Reuses your existing single-PO preview model.
        public POImportPreviewViewModel? PurchaseOrder { get; set; }
    }

    // ============================================================
    // FINAL BATCH IMPORT RESULT
    // ============================================================
    public class POBatchImportResultViewModel
    {
        public string BatchId { get; set; }
            = string.Empty;

        public List<POBatchImportResultItemViewModel> Results { get; set; }
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

        public int TotalMaterialRowsSaved =>
            Results.Sum(x => x.MaterialRowsSaved);
    }

    // ============================================================
    // INDIVIDUAL SAVE RESULT
    // ============================================================
    public class POBatchImportResultItemViewModel
    {
        public string OriginalFileName { get; set; }
            = string.Empty;

        public string? PurchaseOrderNo { get; set; }

        public bool IsImported { get; set; }

        public bool IsUpdated { get; set; }

        public int MaterialRowsSaved { get; set; }

        public string Message { get; set; }
            = string.Empty;
    }
}