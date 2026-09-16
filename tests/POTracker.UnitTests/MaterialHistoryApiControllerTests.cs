using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using POTracker.Contracts.Api;
using POTracker.Controllers.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.UnitTests;

public sealed class MaterialHistoryApiControllerTests
{
    [Fact]
    public async Task Create_RequiresReasonForRejectedMaterial()
    {
        await using var context = CreateContext();
        var material = await AddMaterial(context);
        var controller = new MaterialHistoryApiController(context);

        var result = await controller.Create(
            new MaterialHistoryUpsertRequestDto
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.Rejected,
                Remark = "  "
            });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(context.MaterialHistories);
    }

    [Fact]
    public async Task CreateAndUpdate_PersistsEditableHistory()
    {
        await using var context = CreateContext();
        var material = await AddMaterial(context);
        var controller = new MaterialHistoryApiController(context);

        var createResult = await controller.Create(
            new MaterialHistoryUpsertRequestDto
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.PerfectDelivery,
                Remark = "Delivered on time",
                OccurredDate = new DateTime(2026, 8, 20)
            });

        var createOk = Assert.IsType<OkObjectResult>(createResult.Result);
        var createResponse = Assert.IsType<MaterialHistorySaveResponseDto>(createOk.Value);

        var updateResult = await controller.Update(
            createResponse.History.Id,
            new MaterialHistoryUpsertRequestDto
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.Rejected,
                Remark = "Dimension outside tolerance",
                OccurredDate = new DateTime(2026, 8, 21)
            });

        var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updateResponse = Assert.IsType<MaterialHistorySaveResponseDto>(updateOk.Value);
        var saved = await context.MaterialHistories.SingleAsync();

        Assert.Equal(MaterialHistoryOutcomes.Rejected, saved.Outcome);
        Assert.Equal("Dimension outside tolerance", saved.Remark);
        Assert.NotNull(saved.UpdatedDate);
        Assert.Equal(new DateTime(2026, 8, 21), updateResponse.History.OccurredDate);
    }

    [Fact]
    public async Task Index_ReturnsOutcomeSummaryForMaterial()
    {
        await using var context = CreateContext();
        var material = await AddMaterial(context);

        context.MaterialHistories.AddRange(
            new MaterialHistory
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.PerfectDelivery
            },
            new MaterialHistory
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.ReworkAndDelivery,
                Remark = "Edge reworked"
            });

        await context.SaveChangesAsync();

        var controller = new MaterialHistoryApiController(context);
        var result = await controller.Index(
            search: null,
            outcome: MaterialHistoryOutcomes.PerfectDelivery,
            materialId: material.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MaterialHistoryListResponseDto>(ok.Value);

        Assert.Single(response.Items);
        Assert.Equal(2, response.Summary.TotalEntries);
        Assert.Equal(1, response.Summary.PerfectDeliveries);
        Assert.Equal(1, response.Summary.ReworkDeliveries);
    }

    [Fact]
    public async Task Index_AppliesMultiSelectFilterAndRequestedSort()
    {
        await using var context = CreateContext();
        var material = await AddMaterial(context);

        context.MaterialHistories.AddRange(
            new MaterialHistory
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.Rejected,
                Remark = "Second rejection",
                OccurredDate = new DateTime(2026, 8, 22)
            },
            new MaterialHistory
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.PerfectDelivery,
                Remark = "Accepted",
                OccurredDate = new DateTime(2026, 8, 20)
            },
            new MaterialHistory
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.Rejected,
                Remark = "First rejection",
                OccurredDate = new DateTime(2026, 8, 21)
            });

        await context.SaveChangesAsync();

        var filters = JsonSerializer.Serialize(new Dictionary<string, string[]>
        {
            ["result"] = new[] { MaterialHistoryOutcomes.Rejected }
        });
        var controller = new MaterialHistoryApiController(context);
        var result = await controller.Index(
            search: null,
            outcome: null,
            materialId: null,
            sortColumn: "date",
            sortDirection: "asc",
            excelFilters: filters);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MaterialHistoryListResponseDto>(ok.Value);

        Assert.Equal(2, response.Items.Count);
        Assert.Equal(new DateTime(2026, 8, 21), response.Items[0].OccurredDate);
        Assert.Equal(new DateTime(2026, 8, 22), response.Items[1].OccurredDate);
        Assert.All(
            response.Items,
            item => Assert.Equal(MaterialHistoryOutcomes.Rejected, item.Outcome));
    }

    [Fact]
    public async Task GetExcelFilterValues_ReturnsDistinctValuesWithCounts()
    {
        await using var context = CreateContext();
        var material = await AddMaterial(context);

        context.MaterialHistories.AddRange(
            new MaterialHistory
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.Rejected,
                Remark = "Dimension issue",
                OccurredDate = new DateTime(2026, 8, 21)
            },
            new MaterialHistory
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.Rejected,
                Remark = "Surface issue",
                OccurredDate = new DateTime(2026, 8, 22)
            },
            new MaterialHistory
            {
                MaterialId = material.Id,
                Outcome = MaterialHistoryOutcomes.PerfectDelivery,
                OccurredDate = new DateTime(2026, 8, 23)
            });

        await context.SaveChangesAsync();

        var controller = new MaterialHistoryApiController(context);
        var result = await controller.GetExcelFilterValues(
            column: "result",
            search: null,
            outcome: null,
            materialId: null,
            excelFilters: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var values = Assert.IsAssignableFrom<
            IReadOnlyList<MaterialHistoryExcelFilterValueDto>>(ok.Value);
        var rejected = Assert.Single(
            values,
            value => value.Value == MaterialHistoryOutcomes.Rejected);

        Assert.Equal("Rejected", rejected.Label);
        Assert.Equal(2, rejected.Count);
        Assert.Contains(
            values,
            value => value.Value == MaterialHistoryOutcomes.PerfectDelivery &&
                value.Count == 1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Material> AddMaterial(ApplicationDbContext context)
    {
        var purchaseOrder = new PurchaseOrder
        {
            PONumber = "PO-1001"
        };

        var material = new Material
        {
            PurchaseOrder = purchaseOrder,
            MaterialNo = "MAT-100",
            LineItem = "10",
            ShortDescription = "Machined ring"
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        return material;
    }
}
