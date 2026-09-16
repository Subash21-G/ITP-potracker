using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Controllers.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.UnitTests;

public sealed class MaterialPlanningApiControllerTests
{
    [Fact]
    public async Task SourcingWorkspace_FiltersOutsourcingAndReturnsAllSummaryCounts()
    {
        await using var context = CreateContext();
        await Seed(context);
        var controller = new MaterialPlanningApiController(context);

        var result = await controller.Index(
            workspace: "sourcing",
            scope: "outsourcing",
            search: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MaterialPlanningListResponseDto>(ok.Value);

        Assert.Single(response.Items);
        Assert.Equal("Outsourcing", response.Items[0].SourcingType);
        Assert.Equal("Precision Works", response.Items[0].OutsourcedTo);
        Assert.Equal(2, response.Summary.TotalMaterials);
        Assert.Equal(1, response.Summary.Inhouse);
        Assert.Equal(1, response.Summary.Outsourcing);
    }

    [Fact]
    public async Task CostVariationWorkspace_ReturnsOnlyMarkedMaterials()
    {
        await using var context = CreateContext();
        await Seed(context);
        var controller = new MaterialPlanningApiController(context);

        var result = await controller.Index(
            workspace: "cost-variation",
            search: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MaterialPlanningListResponseDto>(ok.Value);

        var item = Assert.Single(response.Items);
        Assert.True(item.HasCostVariation);
        Assert.Equal(150m, item.VariationCost);
        Assert.Equal("Priority", item.PriorityLevel);
    }

    [Fact]
    public async Task PriorityUrgentWorkspace_PutsUrgentMaterialsFirst()
    {
        await using var context = CreateContext();
        await Seed(context);
        var controller = new MaterialPlanningApiController(context);

        var result = await controller.Index(
            workspace: "priority-urgent",
            search: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MaterialPlanningListResponseDto>(ok.Value);

        Assert.Equal(2, response.Items.Count);
        Assert.Equal("Urgent", response.Items[0].PriorityLevel);
        Assert.Equal("Priority", response.Items[1].PriorityLevel);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task Seed(ApplicationDbContext context)
    {
        context.PurchaseOrders.Add(new PurchaseOrder
        {
            PONumber = "PO-PLANNING-1",
            Status = "Open",
            Materials = new List<Material>
            {
                new()
                {
                    LineItem = "N-10",
                    MaterialNo = "MAT-INHOUSE",
                    ShortDescription = "Priority inhouse material",
                    Qty = 10m,
                    Unit = "EA",
                    Cost = 125m,
                    SourcingType = "Inhouse",
                    HasCostVariation = true,
                    VariationCost = 150m,
                    PriorityLevel = "Priority",
                    ProcessStatus = "Pending",
                    DeliveryDate = new DateTime(2026, 9, 20)
                },
                new()
                {
                    LineItem = "N-20",
                    MaterialNo = "MAT-OUTSOURCE",
                    ShortDescription = "Urgent outsourced material",
                    Qty = 5m,
                    Unit = "EA",
                    Cost = 300m,
                    SourcingType = "Outsourcing",
                    OutsourcedTo = "Precision Works",
                    PriorityLevel = "Urgent",
                    ProcessStatus = "In Process",
                    DeliveryDate = new DateTime(2026, 9, 10)
                }
            }
        });

        await context.SaveChangesAsync();
    }
}
