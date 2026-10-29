using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ITPQuotation.Api.Data;
using ITPQuotation.Api.Models;
using ITPQuotation.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ITPQuotation.Api.Tests;

public sealed class PoTrackerExportTests
{
    [Fact]
    public async Task SendsContractAndPersistsReferenceAndRetriesSameRevision()
    {
        await using var db = await SeedAsync();
        var deliveries = new List<JsonElement>();
        var handler = new Handler(async request =>
        {
            Assert.Equal("http://localhost:5252/api/purchase-orders/from-quotation", request.RequestUri!.ToString());
            Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("X-Quotation-Api-Key")));
            deliveries.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement.Clone());
            return new HttpResponseMessage(deliveries.Count == 1 ? HttpStatusCode.Created : HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new PoTrackerExportResult(42, "PO-100", "QTN-100", 1, deliveries.Count > 1))
            };
        });
        using var http = new HttpClient(handler);
        var service = new PoTrackerExportService(db, Config(), http);
        var created = await service.ExportAsync(1, " PO-100 ", default);
        Assert.Equal(42, created!.PurchaseOrderId);
        var retried = await service.ExportAsync(1, "PO-100", default);
        Assert.True(retried!.AlreadyImported);
        Assert.Equal(deliveries[0].ToString(), deliveries[1].ToString());
        var payload = deliveries[0];
        Assert.Equal("ITPQuotation", payload.GetProperty("sourceSystem").GetString());
        Assert.Equal("CUST-7", payload.GetProperty("customerCode").GetString());
        Assert.Equal("RFQ-100", payload.GetProperty("rfqNumber").GetString());
        Assert.Equal(1, payload.GetProperty("revision").GetInt32());
        var item = Assert.Single(payload.GetProperty("items").EnumerateArray());
        Assert.Equal("MAT-100", item.GetProperty("materialNo").GetString());
        Assert.Equal(12.35m, item.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(30.88m, item.GetProperty("totalPrice").GetDecimal());
        db.ChangeTracker.Clear();
        var quote = await db.Quotations.SingleAsync();
        Assert.Equal(42, quote.PoTrackerPurchaseOrderId);
        Assert.Equal("PO-100", quote.PoTrackerPoNumber);
        Assert.Equal(0, quote.PoTrackerExportedRevision);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, 409)]
    [InlineData(HttpStatusCode.Unauthorized, 502)]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.InternalServerError, 502)]
    public async Task RejectedExportDoesNotStoreReference(HttpStatusCode remoteStatus, int expectedStatus)
    {
        await using var db = await SeedAsync();
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(remoteStatus))));
        var service = new PoTrackerExportService(db, Config(), http);
        var exception = await Assert.ThrowsAsync<PoTrackerExportException>(() => service.ExportAsync(1, "PO-100", default));
        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.Null((await db.Quotations.SingleAsync()).PoTrackerPurchaseOrderId);
    }

    [Theory]
    [InlineData("Draft", true, "2.5", 400)]
    [InlineData("Accepted", false, "2.5", 400)]
    [InlineData("Accepted", true, "2.555", 400)]
    public async Task InvalidStateMappingOrPrecisionNeverSends(string status, bool mapped, string quantity, int expectedStatus)
    {
        await using var db = await SeedAsync();
        var quote = await db.Quotations.Include(x => x.Items).SingleAsync();
        quote.Status = status;
        quote.Items.Single().Quantity = decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture);
        await db.SaveChangesAsync();
        using var http = new HttpClient(new Handler(_ => throw new Exception("Invalid export must not be sent.")));
        var service = new PoTrackerExportService(db, Config(mapped), http);
        var exception = await Assert.ThrowsAsync<PoTrackerExportException>(() => service.ExportAsync(1, "PO-100", default));
        Assert.Equal(expectedStatus, exception.StatusCode);
    }

    [Fact]
    public async Task TransportFailureCanBeRetriedWithoutStoringReference()
    {
        await using var db = await SeedAsync();
        using var http = new HttpClient(new Handler(_ => throw new HttpRequestException("Unreachable")));
        var exception = await Assert.ThrowsAsync<PoTrackerExportException>(() => new PoTrackerExportService(db, Config(), http).ExportAsync(1, "PO-100", default));
        Assert.Equal(502, exception.StatusCode);
        Assert.Null((await db.Quotations.SingleAsync()).PoTrackerPurchaseOrderId);
    }

    private static IConfiguration Config(bool mapped = true) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["POTracker:BaseUrl"] = "http://localhost:5252",
            ["POTracker:ApiKey"] = "test-key",
            ["POTracker:CustomerCodes:7"] = mapped ? "CUST-7" : null
        }).Build();

    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Quotations.Add(new Quotation
        {
            Id = 1, QuotationNumber = "QTN-100", CustomerId = 7, Revision = 0, Status = QuotationValues.Accepted,
            Rfq = new Rfq { Id = 1, RfqNumber = "RFQ-100", CustomerId = 7 },
            Items = [new QuotationItem { Id = 1, MaterialNo = "MAT-100", Description = "Shaft", Quantity = 2.5m, UnitPrice = 12.345m }]
        });
        await db.SaveChangesAsync();
        return db;
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
