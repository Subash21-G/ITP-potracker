using System.Reflection;
using POTracker.Controllers;
using POTracker.Controllers.Api;
using POTracker.Services;

namespace POTracker.UnitTests;

public sealed class InvoiceLineReferenceTests
{
    [Theory]
    [InlineData("N-10", "10")]
    [InlineData("M-10", "10")]
    [InlineData("S-10", "10")]
    [InlineData("10", "10")]
    [InlineData("m-20", "20")]
    [InlineData("S-30", "30")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("N", "")]
    public void AllInvoiceMatchingPaths_UseOnlyNumericLineReference(
        string? input, string expected)
    {
        Type[] matchingPaths =
        [
            typeof(VyaparInvoiceImportController),
            typeof(InvoicesApiController),
            typeof(PendingInvoiceMatcher)
        ];

        foreach (var path in matchingPaths)
        {
            var normalize = path.GetMethod(
                "NormalizeLineItem",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            Assert.Equal(expected, normalize.Invoke(null, [input]));
        }
    }
}
