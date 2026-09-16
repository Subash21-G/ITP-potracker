using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Controllers.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;

namespace POTracker.UnitTests;

public sealed class PurchaseOrderDetailsExcelFilterTests
{
    [Fact]
    public async Task Details_AppliesExcelMultiSelectAndNumericSortAcrossMaterialPage()
    {
        await using var context = CreateContext();
        var purchaseOrder = await AddPurchaseOrderWithMaterials(context);
        var controller = CreateController(context);
        var filters = JsonSerializer.Serialize(new Dictionary<string, string[]>
        {
            ["dispatch"] = new[] { "Pending" }
        });

        var result = await controller.Details(
            id: purchaseOrder.Id,
            lineItem: null,
            material: null,
            description: null,
            minQuantity: null,
            maxQuantity: null,
            minUnitPrice: null,
            maxUnitPrice: null,
            minInvoiceBalance: null,
            maxInvoiceBalance: null,
            minDispatchBalance: null,
            maxDispatchBalance: null,
            deliveryDateFrom: null,
            deliveryDateTo: null,
            overdueStatus: null,
            processStatus: null,
            invoiceStatus: null,
            dispatchStatus: null,
            materialSortColumn: "quantity",
            materialSortDirection: "desc",
            materialExcelFilters: filters);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PurchaseOrderDetailsDto>(ok.Value);

        Assert.Equal(2, response.Materials.TotalRecords);
        Assert.Equal(180m, response.Materials.Items[0].Quantity);
        Assert.Equal(60m, response.Materials.Items[1].Quantity);
        Assert.All(
            response.Materials.Items,
            materialRow => Assert.Equal("Pending", materialRow.DispatchStatus));
    }

    [Fact]
    public async Task ExcelFilterValues_ReturnsDistinctValuesAndRespectsOtherColumns()
    {
        await using var context = CreateContext();
        var purchaseOrder = await AddPurchaseOrderWithMaterials(context);
        var controller = CreateController(context);
        var filters = JsonSerializer.Serialize(new Dictionary<string, string[]>
        {
            ["dispatch"] = new[] { "Pending" }
        });

        var result = await controller.GetPurchaseOrderMaterialExcelFilterValues(
            id: purchaseOrder.Id,
            column: "quantity",
            materialExcelFilters: filters);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var values = Assert.IsAssignableFrom<IReadOnlyList<ExcelFilterValueDto>>(ok.Value);

        Assert.Equal(2, values.Count);
        Assert.Contains(values, value => value.Value == "180" && value.Count == 1);
        Assert.Contains(values, value => value.Value == "60" && value.Count == 1);
        Assert.DoesNotContain(values, value => value.Value == "30");
    }

    [Fact]
    public async Task ExcelFilterValues_LineItemsUseNumericLabelsInNaturalOrder()
    {
        await using var context = CreateContext();
        var purchaseOrder = await AddPurchaseOrderWithMaterials(context);
        var controller = CreateController(context);

        var result = await controller.GetPurchaseOrderMaterialExcelFilterValues(
            id: purchaseOrder.Id,
            column: "line",
            materialExcelFilters: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var values = Assert.IsAssignableFrom<IReadOnlyList<ExcelFilterValueDto>>(ok.Value);

        Assert.Equal(
            new[] { "10", "20", "100" },
            values.Select(value => value.Label));
        Assert.Equal(
            new[] { "N-10", "N-20", "N-100" },
            values.Select(value => value.Value));
    }

    private static PurchaseOrdersApiController CreateController(
        ApplicationDbContext context)
    {
        return new PurchaseOrdersApiController(
            context,
            new PrivateDocumentReferenceService());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<PurchaseOrder> AddPurchaseOrderWithMaterials(
        ApplicationDbContext context)
    {
        var purchaseOrder = new PurchaseOrder
        {
            PONumber = "4519180201",
            Status = "Open",
            Purchaser = new Purchaser
            {
                PurchaserName = "WEG Industries (India) PVT LTD"
            },
            Materials = new List<Material>
            {
                new()
                {
                    LineItem = "N-10",
                    MaterialNo = "11573477",
                    ShortDescription = "Clamp 85x85",
                    Qty = 180m,
                    Unit = "UN",
                    Cost = 90m,
                    BalanceQty = 180m,
                    PendingQty = 180m,
                    ProcessStatus = "Pending",
                    InvoiceStatus = "Not Invoiced",
                    DispatchStatus = "Pending"
                },
                new()
                {
                    LineItem = "N-100",
                    MaterialNo = "10171523",
                    ShortDescription = "Clamp R3X3",
                    Qty = 60m,
                    Unit = "UN",
                    Cost = 37m,
                    BalanceQty = 60m,
                    PendingQty = 60m,
                    ProcessStatus = "Pending",
                    InvoiceStatus = "Not Invoiced",
                    DispatchStatus = "Pending"
                },
                new()
                {
                    LineItem = "N-20",
                    MaterialNo = "10174177",
                    ShortDescription = "Completed clamp",
                    Qty = 30m,
                    Unit = "UN",
                    Cost = 25m,
                    BalanceQty = 0m,
                    PendingQty = 0m,
                    ProcessStatus = "Complete",
                    InvoiceStatus = "Fully Invoiced",
                    DispatchStatus = "Dispatched"
                }
            }
        };

        context.PurchaseOrders.Add(purchaseOrder);
        await context.SaveChangesAsync();

        return purchaseOrder;
    }
}
