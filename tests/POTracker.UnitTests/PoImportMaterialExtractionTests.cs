using System.Reflection;
using POTracker.Controllers;
using POTracker.ViewModels;

namespace POTracker.UnitTests;

public sealed class PoImportMaterialExtractionTests
{
    [Fact]
    public void ExtractItems_MapsRequestedWasherFieldsAndLeavesMissingValuesBlank()
    {
        const string pdfText = """
            N 10 11576700 28 UN RS02 26.00 UN 728.00 30.09.26 9% 65.52 9% 65.52
            Description: PLAIN WASHER 8X25X3 SS316L
            PLAIN WASHER 8 mmX25mmX3mm NORMAL STAINLESS STEEL AISI 316L NO SURFACE LINING
            NOMINAL DIAMETER WASHER (DN): 8 mm; INTERNAL DIAMETER WASHER (DI): 8.4mm;
            EXTERNAL DIAMETER WASHER (DE): 25mm; NOMINAL THICKNESS WASHER (L): 3mm;
            HOLE TYPE PLAIN WASHER: NORMAL; PLAIN WASHER MATERIAL: STAINLESS STEEL AISI 316L;
            COATING: NO SURFACE LINING
            Drawing: SWD 10007585436 000 01
            HSN/SAC Code: 7318.22.00
            """;

        var controller = new POImportController(
            null!,
            null!,
            null!,
            null!,
            null!);

        var extractItems =
            typeof(POImportController).GetMethod(
                "ExtractItems",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        Assert.NotNull(extractItems);

        var items = Assert.IsType<List<POImportItemViewModel>>(
            extractItems.Invoke(
                controller,
                new object[] { pdfText }));

        var item = Assert.Single(items);

        Assert.Equal("11576700", item.MaterialNo);
        Assert.Equal(
            "PLAIN WASHER 8X25X3 SS316L",
            item.ShortDescription);
        Assert.Contains(
            "PLAIN WASHER MATERIAL: STAINLESS STEEL AISI 316L",
            item.FullDescription);
        Assert.Equal(
            "SWD 10007585436 000 01",
            item.DrawingNo);
        Assert.Equal("01", item.DrawingVersion);
        Assert.Equal(
            "STAINLESS STEEL AISI 316L",
            item.Grade);
        Assert.Equal("8X25X3", item.Size);
        Assert.Null(item.Process);
        Assert.Null(item.Source);
    }
}
