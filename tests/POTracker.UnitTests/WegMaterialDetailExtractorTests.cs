using POTracker.Services;

namespace POTracker.UnitTests;

public sealed class WegMaterialDetailExtractorTests
{
    [Fact]
    public void ExtractsDetailsFromWasherPurchaseOrderItem()
    {
        const string shortDescription =
            "PLAIN WASHER 8X25X3 SS316L";

        const string fullDescription =
            "PLAIN WASHER 8 mmX25mmX3mm NORMAL STAINLESS STEEL " +
            "AISI 316L NO SURFACE LINING; NOMINAL DIAMETER " +
            "WASHER (DN): 8 mm; PLAIN WASHER MATERIAL: " +
            "STAINLESS STEEL AISI 316L; COATING: NO SURFACE LINING";

        Assert.Equal(
            "01",
            WegMaterialDetailExtractor.ExtractDrawingVersion(
                "SWD 10007585436 000 01"));

        Assert.Equal(
            "STAINLESS STEEL AISI 316L",
            MaterialRmGradeExtractor.Extract(
                shortDescription,
                fullDescription));

        Assert.Equal(
            "8X25X3",
            WegMaterialDetailExtractor.ExtractSize(
                shortDescription,
                fullDescription));

        Assert.Null(
            WegMaterialDetailExtractor.ExtractProcess(
                shortDescription,
                fullDescription));

        Assert.Null(
            WegMaterialDetailExtractor.ExtractSource(
                shortDescription,
                fullDescription));
    }

    [Fact]
    public void ExtractsOnlyExplicitProcessAndSource()
    {
        const string description =
            "SIZE: 12 mm x 40 mm; PROCESS: CNC TURNING; " +
            "RAW MATERIAL SOURCE: CUSTOMER SUPPLIED";

        Assert.Equal(
            "12 MMX40 MM",
            WegMaterialDetailExtractor.ExtractSize(
                null,
                description));

        Assert.Equal(
            "CNC TURNING",
            WegMaterialDetailExtractor.ExtractProcess(
                null,
                description));

        Assert.Equal(
            "CUSTOMER SUPPLIED",
            WegMaterialDetailExtractor.ExtractSource(
                null,
                description));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DWG-100")]
    public void DrawingVersionIsBlankWhenNotAvailable(
        string? drawing)
    {
        Assert.Null(
            WegMaterialDetailExtractor.ExtractDrawingVersion(
                drawing));
    }
}
