using System.IO;

namespace POTracker.Services;

public sealed class PrivateDocumentStorageOptions
{
    public const string SectionName =
        "PrivateDocumentStorage";

    public string RootPath { get; set; } =
        Path.Combine(
            "App_Data",
            "private-uploads");
}
