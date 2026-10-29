using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POTracker.Controllers.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
var key = Environment.GetEnvironmentVariable("SMOKE_KEY") ?? throw new Exception("Missing test key");
builder.Configuration["QuotationIntegration:ApiKey"] = key;
builder.Configuration["QuotationIntegration:CustomerMappings:SMOKE-CUSTOMER"] = "1";
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase("quotation-http-smoke")
    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
builder.Services.AddControllers(options => options.Filters.Add<SessionAuthorizationFilter>())
    .AddApplicationPart(typeof(QuotationImportApiController).Assembly);
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Purchasers.Add(new Purchaser { Id = 1, PurchaserName = "Isolated smoke customer" });
    await db.SaveChangesAsync();
}
app.MapControllers();
app.MapGet("/smoke/verify", async (HttpContext context, ApplicationDbContext db) =>
{
    if (context.Request.Headers["X-Quotation-Api-Key"] != key) return Results.Unauthorized();
    var orders = await db.PurchaseOrders.Include(x => x.Materials).ToListAsync();
    var order = orders.Single();
    var row = order.Materials.Single();
    return Results.Ok(new { count = orders.Count, id = order.Id, purchaserId = order.PurchaserId,
        quantity = row.Qty, unitPrice = row.Cost, totalPrice = row.TotalValue });
});
await app.StartAsync();
File.WriteAllText(Environment.GetEnvironmentVariable("SMOKE_URL_FILE")!, app.Urls.Single());
await app.WaitForShutdownAsync();
