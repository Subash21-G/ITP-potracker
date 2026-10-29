using System.Net.Http.Json;
using System.Text.Json;
using ITPQuotation.Api.Data;
using ITPQuotation.Api.Models;
using ITPQuotation.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var url = Environment.GetEnvironmentVariable("SMOKE_URL")!;
var key = Environment.GetEnvironmentVariable("SMOKE_KEY")!;
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase("quotation-sender-http-smoke").Options);
var quote = new Quotation { Id = 1, QuotationNumber = "HTTP-SMOKE-100", Revision = 0, CustomerId = 1,
    Status = QuotationValues.Accepted, Rfq = new Rfq { Id = 1, CustomerId = 1, RfqNumber = "HTTP-RFQ-100" },
    Items = [new QuotationItem { Id = 1, MaterialNo = "HTTP-MAT-100", Description = "Smoke shaft", Quantity = 2.5m, UnitPrice = 12.345m }] };
db.Quotations.Add(quote);
await db.SaveChangesAsync();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["POTracker:BaseUrl"] = url, ["POTracker:ApiKey"] = key,
    ["POTracker:CustomerCodes:1"] = "SMOKE-CUSTOMER"
}).Build();
using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
var service = new PoTrackerExportService(db, config, http);
var first = await service.ExportAsync(1, "HTTP-PO-100", default) ?? throw new Exception("Export missing");
if (first.AlreadyImported || first.Revision != 1) throw new Exception("Unexpected initial receipt");
var retry = await service.ExportAsync(1, "HTTP-PO-100", default) ?? throw new Exception("Retry missing");
if (!retry.AlreadyImported || retry.PurchaseOrderId != first.PurchaseOrderId) throw new Exception("Retry created a duplicate");
db.ChangeTracker.Clear();
var stored = await db.Quotations.Include(x => x.Items).SingleAsync();
if (stored.PoTrackerPurchaseOrderId != first.PurchaseOrderId) throw new Exception("Reference not persisted");
stored.Items.Single().Description = "Changed description";
await db.SaveChangesAsync();
try { await service.ExportAsync(1, "HTTP-PO-100", default); throw new Exception("Changed data was accepted"); }
catch (PoTrackerExportException e) when (e.StatusCode == 409) { }
config["POTracker:ApiKey"] = "wrong-test-key";
try { await service.ExportAsync(1, "HTTP-PO-100", default); throw new Exception("Invalid key was accepted"); }
catch (PoTrackerExportException e) when (e.StatusCode == 502) { }
using var verify = new HttpRequestMessage(HttpMethod.Get, url + "/smoke/verify");
verify.Headers.Add("X-Quotation-Api-Key", key);
using var response = await http.SendAsync(verify);
response.EnsureSuccessStatusCode();
var result = await response.Content.ReadFromJsonAsync<JsonElement>();
if (result.GetProperty("count").GetInt32() != 1 || result.GetProperty("purchaserId").GetInt32() != 1
    || result.GetProperty("quantity").GetDecimal() != 2.5m || result.GetProperty("unitPrice").GetDecimal() != 12.35m
    || result.GetProperty("totalPrice").GetDecimal() != 30.88m) throw new Exception("Order data differs");
Console.WriteLine("PASS: real quotation sender -> HTTP -> real POTracker receiver; reference saved, retry deduplicated, conflict and invalid key rejected, material rows verified. Isolated in-memory databases only.");
