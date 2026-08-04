using POTracker.Services;

namespace POTracker.UnitTests;

public sealed class PrivateDocumentReferenceServiceTests
{
    private readonly PrivateDocumentReferenceService
        _service = new();

    [Theory]
    [InlineData(
        "/uploads/po/order.pdf",
        "po",
        "po/order.pdf")]
    [InlineData(
        "uploads/invoices/invoice.pdf",
        "invoices",
        "invoices/invoice.pdf")]
    [InlineData(
        "wwwroot/uploads/po/order.pdf",
        "po",
        "po/order.pdf")]
    [InlineData(
        "/api/private-documents/po/order.pdf",
        "po",
        "po/order.pdf")]
    [InlineData(
        "order.pdf",
        "po",
        "po/order.pdf")]
    [InlineData(
        @"C:\application\wwwroot\uploads\po\order.pdf",
        "po",
        "po/order.pdf")]
    [InlineData(
        "https://localhost:7271/uploads/invoices/" +
        "tax%20invoice.pdf?download=1",
        "invoices",
        "invoices/tax invoice.pdf")]
    public void NormalizeRelativePath_HandlesSupportedFormats(
        string storedValue,
        string defaultCategory,
        string expected)
    {
        var result =
            _service.NormalizeRelativePath(
                storedValue,
                defaultCategory);

        Assert.Equal(
            expected,
            result);
    }

    [Fact]
    public void ToDownloadUrl_EncodesCategoryAndFileName()
    {
        var result =
            _service.ToDownloadUrl(
                "/uploads/invoices/tax invoice.pdf",
                "invoices");

        Assert.Equal(
            "/api/private-documents/" +
            "invoices/tax%20invoice.pdf",
            result);
    }

    [Theory]
    [InlineData(null, "po")]
    [InlineData("", "po")]
    [InlineData("../order.pdf", "po")]
    [InlineData(
        "/uploads/po/subfolder/order.pdf",
        "po")]
    [InlineData(
        "/api/private-documents/po/%2E%2E",
        "po")]
    [InlineData(
        "/uploads/%2E%2E/order.pdf",
        "po")]
    public void NormalizeRelativePath_RejectsUnsafeReferences(
        string? storedValue,
        string defaultCategory)
    {
        var result =
            _service.NormalizeRelativePath(
                storedValue,
                defaultCategory);

        Assert.Null(result);
    }
}