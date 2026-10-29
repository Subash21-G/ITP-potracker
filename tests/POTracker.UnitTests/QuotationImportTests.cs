using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POTracker.Contracts.Api;
using POTracker.Controllers.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Security;

namespace POTracker.UnitTests;

public sealed class QuotationImportTests
{
    [Theory]
    [InlineData(null, "secret", false)]
    [InlineData("secret", "wrong", false)]
    [InlineData("secret", "secret", true)]
    public void IntegrationAuthorizationRequiresConfiguredMatchingKey(string? configured, string supplied, bool accepted)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["QuotationIntegration:ApiKey"] = configured }).Build();
        using var provider = new ServiceCollection().AddSingleton<IConfiguration>(configuration).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Headers["X-Quotation-Api-Key"] = supplied;
        var descriptor = new ActionDescriptor { EndpointMetadata = new List<object> { new QuotationIntegrationAttribute() } };
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), descriptor), new List<IFilterMetadata>());
        new SessionAuthorizationFilter().OnAuthorization(context);
        if (accepted) Assert.Null(context.Result);
        else Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task ImportsMaterialsAndReferenceAndReturnsSameOrderForRetry()
    {
        await using var database = CreateDatabase();
        database.Purchasers.Add(new Purchaser { Id = 1, PurchaserName = "Customer" });
        await database.SaveChangesAsync();
        var controller = CreateController(database);
        var request = Request();
        Assert.IsType<CreatedResult>(await controller.Import(request, default));
        Assert.IsType<OkObjectResult>(await controller.Import(request, default));
        var order = Assert.Single(database.PurchaseOrders.Include(x => x.Materials));
        Assert.Equal("QTN-2026-001", order.QuotationNumber);
        Assert.Equal(1, order.QuotationRevision);
        Assert.Equal(46000m, order.GrandTotal);
        var row = Assert.Single(order.Materials);
        Assert.Equal(25m, row.PendingQty);
        Assert.Equal(1840m, row.Cost);
        request.Items[0].Description = "Changed";
        Assert.IsType<ConflictObjectResult>(await controller.Import(request, default));
        request.Items[0].Description = "INTERM BEAR CAP";
        request.Revision = 2;
        Assert.IsType<ConflictObjectResult>(await controller.Import(request, default));
        Assert.Single(database.PurchaseOrders);
    }

    [Fact]
    public async Task UnmappedCustomerDoesNotCreateOrder()
    {
        await using var database = CreateDatabase();
        Assert.IsType<BadRequestObjectResult>(await CreateController(database).Import(Request(), default));
        Assert.Empty(database.PurchaseOrders);
    }

    [Fact]
    public void RejectsIncorrectLineTotal()
    {
        var request = Request();
        request.Items[0].TotalPrice = 1;
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, x => x.ErrorMessage!.Contains("totalPrice"));
    }

    private static ApplicationDbContext CreateDatabase() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static QuotationImportApiController CreateController(ApplicationDbContext database) => new(database,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["QuotationIntegration:CustomerMappings:CUST-100"] = "1" }).Build());

    private static QuotationImportRequest Request() => new()
    {
        QuotationNumber = "QTN-2026-001", Revision = 1, CustomerPoNumber = "PO-4587",
        CustomerCode = "CUST-100", RfqNumber = "RFQ-2026-024",
        Items = new() { new() { MaterialNo = "11753667", Description = "INTERM BEAR CAP",
            Quantity = 25, UnitPrice = 1840, TotalPrice = 46000 } }
    };
}
