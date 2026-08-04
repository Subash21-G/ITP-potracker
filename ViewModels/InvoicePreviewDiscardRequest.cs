namespace POTracker.ViewModels;

public sealed class InvoicePreviewDiscardRequest
{
    public List<string?> UploadedFilePaths
    {
        get;
        set;
    } = new();
}