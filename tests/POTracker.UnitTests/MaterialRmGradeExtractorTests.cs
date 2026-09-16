using POTracker.Services;

namespace POTracker.UnitTests;

public sealed class MaterialRmGradeExtractorTests
{
    [Fact]
    public void Extract_PrefersExplicitRmGrade()
    {
        var grade =
            MaterialRmGradeExtractor.Extract(
                "BOLT AISI304",
                "RM Grade: EN 24; SCREW MATERIAL: AISI 304");

        Assert.Equal(
            "EN 24",
            grade);
    }

    [Fact]
    public void Extract_ReadsMaterialAcrossPdfLineBreak()
    {
        var grade =
            MaterialRmGradeExtractor.Extract(
                null,
                "CABLE INLET ADAPT MATERIAL: STAINLESS STEEL AISI\n316;INTERNAL THREAD: M14");

        Assert.Equal(
            "STAINLESS STEEL AISI 316",
            grade);
    }

    [Fact]
    public void Extract_PrefersBodyOverFastenerAndBaseMaterials()
    {
        var grade =
            MaterialRmGradeExtractor.Extract(
                "SPLIT CLAMP",
                "FASTENERS MATERIAL: STAINLESS STEEL AISI 316;" +
                "BIPARTITE CLAMP BODY MATERIAL: POLYPROPYLENE;" +
                "CLAMP/COVER BASE MATERIAL: CARBON STEEL SAE 1010/20");

        Assert.Equal(
            "POLYPROPYLENE",
            grade);
    }

    [Fact]
    public void Extract_FallsBackToGradeInShortDescription()
    {
        var grade =
            MaterialRmGradeExtractor.Extract(
                "SIMPL OPEN CLAMP R14X14 AISI316L",
                null);

        Assert.Equal(
            "AISI 316L",
            grade);
    }

    [Fact]
    public void Extract_IgnoresRohsRiskMaterial()
    {
        var grade =
            MaterialRmGradeExtractor.Extract(
                null,
                "ROHS RISK MATERIAL: MEDIUM (WFR-35040);" +
                "THREADED PLUG MATERIAL: POLYAMIDE 6.6");

        Assert.Equal(
            "POLYAMIDE 6.6",
            grade);
    }

    [Theory]
    [InlineData(
        "SLOTTED CASTLE NUT CARBON STEEL",
        "CARBON STEEL")]
    [InlineData(
        "VIBRATION SENSOR SUPPORT CAST IRON FC-200",
        "CAST IRON FC-200")]
    [InlineData(
        "SUPPORT MEDIUM CARBON STEEL>1030<=1050 TO DEVICE",
        "MEDIUM CARBON STEEL>1030<=1050")]
    [InlineData(
        "MATERIAL: PHENO Page: 4 / 7 Order No: 123",
        "PHENO")]
    public void Extract_HandlesUnlabelledAndPdfTrailingGrades(
        string description,
        string expected)
    {
        var grade =
            MaterialRmGradeExtractor.Extract(
                null,
                description);

        Assert.Equal(
            expected,
            grade);
    }

    [Fact]
    public void Extract_ReturnsNullWhenNoGradeIsPresent()
    {
        var grade =
            MaterialRmGradeExtractor.Extract(
                "CUSTOM MACHINED PART",
                "LENGTH: 40mm;SURFACE: GALVANIZED");

        Assert.Null(grade);
    }
}
