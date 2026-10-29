using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;
namespace POTracker.UnitTests;
public sealed class PartialInvoiceMatchingTests
{
    [Fact]
    public async Task PartialInvoiceThenBalanceInvoice_UsesRemainingQuantity()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var material = new Material { PurchaseOrder = new PurchaseOrder { PONumber = "450001" }, MaterialNo = "MAT001", LineItem = "M-10", Qty = 100, BalanceQty = 100 };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        var matcher = new PendingInvoiceMatcher(db);
        db.PendingInvoiceItems.Add(Pending("INV-1", "N-10", 30));
        await db.SaveChangesAsync();
        Assert.Equal(1, await matcher.MatchPendingInvoicesForMaterialAsync(material.Id));
        Assert.Equal(30m, material.InvoiceQty);
        Assert.Equal(70m, material.BalanceQty);
        Assert.Equal("Partially Invoiced", material.InvoiceStatus);
        // A stale cached balance must not permit an invoice exceeding 70.
        material.BalanceQty = 100;
        var excess = Pending("INV-EXCESS", "E-10", 80);
        db.PendingInvoiceItems.Add(excess);
        await db.SaveChangesAsync();
        Assert.Equal(0, await matcher.MatchPendingInvoicesForMaterialAsync(material.Id));
        Assert.Equal("Pending", excess.Status);
        Assert.Equal(30m, material.InvoiceQty);
        db.PendingInvoiceItems.Add(Pending("INV-2", "C-10", 70));
        await db.SaveChangesAsync();
        Assert.Equal(1, await matcher.MatchPendingInvoicesForMaterialAsync(material.Id));
        Assert.Equal(100m, material.InvoiceQty);
        Assert.Equal(0m, material.BalanceQty);
        Assert.Equal("Fully Invoiced", material.InvoiceStatus);
        Assert.Equal(2, await db.InvoiceItems.CountAsync());
        Assert.Equal(0, await matcher.MatchPendingInvoicesForMaterialAsync(material.Id));
        Assert.Equal(100m, material.InvoiceQty);
    }
    [Theory]
    [InlineData("N-10", "C-10")]
    [InlineData("", null)]
    public async Task AmbiguousOrMissingNumericLine_RemainsPending(string pendingLine, string? duplicateLine)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var po = new PurchaseOrder { PONumber = "450001" };
        var material = new Material { PurchaseOrder = po, MaterialNo = "MAT001", LineItem = "M-10", Qty = 100 };
        db.Materials.Add(material);
        if (duplicateLine != null)
            db.Materials.Add(new Material { PurchaseOrder = po, MaterialNo = "MAT001", LineItem = duplicateLine, Qty = 100 });
        var pending = Pending("INV-AMBIGUOUS", pendingLine, 30);
        db.PendingInvoiceItems.Add(pending);
        await db.SaveChangesAsync();
        Assert.Equal(0, await new PendingInvoiceMatcher(db).MatchPendingInvoicesForMaterialAsync(material.Id));
        Assert.Equal("Pending", pending.Status);
        Assert.Empty(await db.InvoiceItems.ToListAsync());
    }

    private static PendingInvoiceItem Pending(string number, string line, decimal qty) => new()
    {
        Invoice = new Invoice { InvoiceNumber = number }, InvoiceNumber = number,
        PONumber = "450001", MaterialNo = "MAT001", LineItem = line, InvoiceQty = qty
    };
}
