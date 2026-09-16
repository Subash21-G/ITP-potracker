using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Controllers.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.UnitTests;

public sealed class MasterSheetApiControllerTests
{
    [Fact]
    public async Task SaveRecord_PersistsOrderedProcessesAndSynchronizesMaterialFields()
    {
        await using var context = CreateContext();
        context.PurchaseOrders.Add(new PurchaseOrder
        {
            PONumber = "PO-MASTER-1",
            Status = "Open",
            Materials = new List<Material>
            {
                new()
                {
                    MaterialNo = "MAT-100",
                    ShortDescription = "Imported description",
                    Qty = 2m
                }
            }
        });
        await context.SaveChangesAsync();

        var controller = new MasterSheetApiController(context);
        var result = await controller.SaveRecord(new MasterSheetSaveRequestDto
        {
            MaterialNo = " MAT-100 ",
            ShortDescription = "Machined shaft",
            Grade = "EN8",
            DrawingNo = " DWG-100 ",
            DrawingVersion = "V3",
            FinishSize = "25 mm",
            RequiredRmSize = "30 mm",
            ProcessSteps = new List<MasterSheetProcessStepRequestDto>
            {
                new()
                {
                    ProcessName = "Turning",
                    VendorName = "Vendor A",
                    Source = "Outsource"
                },
                new()
                {
                    ProcessName = "Grinding",
                    VendorName = "Vendor B",
                    Source = "Outsource"
                },
                new()
                {
                    ProcessName = "Inspection",
                    Source = "In-house"
                }
            }
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MasterSheetRecordDto>(ok.Value);

        Assert.Equal("MAT-100", response.MaterialNo);
        Assert.Equal(3, response.ProcessSteps.Count);
        Assert.Equal(
            new[] { 1, 2, 3 },
            response.ProcessSteps.Select(step => step.Sequence));
        Assert.Equal(
            new[] { "Turning", "Grinding", "Inspection" },
            response.ProcessSteps.Select(step => step.ProcessName));

        var savedMaterial = await context.Materials.SingleAsync();
        Assert.Equal("Machined shaft", savedMaterial.ShortDescription);
        Assert.Equal("EN8", savedMaterial.RMGrade);
        Assert.Equal("DWG-100", response.DrawingNo);
        Assert.Equal("DWG-100", savedMaterial.DrawingNo);
        Assert.Equal("V3", savedMaterial.DrawingVersion);
        Assert.Equal("25 mm", savedMaterial.SurfaceFinish);
        Assert.Equal("30 mm", savedMaterial.RMSize);

        context.ChangeTracker.Clear();
        var reloadResult = await controller.GetRecord("MAT-100");
        var reloadOk = Assert.IsType<OkObjectResult>(reloadResult.Result);
        var reloaded = Assert.IsType<MasterSheetRecordDto>(reloadOk.Value);

        Assert.Equal("Machined shaft", reloaded.ShortDescription);
        Assert.Equal("DWG-100", reloaded.DrawingNo);
        Assert.Equal(3, reloaded.ProcessSteps.Count);
        Assert.Equal("Vendor B", reloaded.ProcessSteps[1].VendorName);
    }

    [Fact]
    public async Task SaveRecord_UpdateRemovesOldStepsAndKeepsContinuousSequence()
    {
        await using var context = CreateContext();
        var controller = new MasterSheetApiController(context);

        await controller.SaveRecord(new MasterSheetSaveRequestDto
        {
            MaterialNo = "MAT-200",
            ProcessSteps = new List<MasterSheetProcessStepRequestDto>
            {
                new() { ProcessName = "Cutting", Source = "In-house" },
                new() { ProcessName = "Turning", Source = "In-house" },
                new() { ProcessName = "Grinding", Source = "Outsource" }
            }
        });

        var update = await controller.SaveRecord(new MasterSheetSaveRequestDto
        {
            MaterialNo = "MAT-200",
            ProcessSteps = new List<MasterSheetProcessStepRequestDto>
            {
                new()
                {
                    ProcessName = "Final inspection",
                    VendorName = "Quality Lab",
                    Source = "Outsource"
                }
            }
        });

        var ok = Assert.IsType<OkObjectResult>(update.Result);
        var response = Assert.IsType<MasterSheetRecordDto>(ok.Value);
        var process = Assert.Single(response.ProcessSteps);

        Assert.Equal(1, process.Sequence);
        Assert.Equal("Final inspection", process.ProcessName);
        Assert.Equal("Quality Lab", process.VendorName);
        Assert.Single(await context.MasterSheetProcessSteps.ToListAsync());
    }

    [Fact]
    public async Task ProcessOptions_ArePersistedAndCanBeRemoved()
    {
        await using var context = CreateContext();
        var controller = new MasterSheetApiController(context);

        var createResult = await controller.AddProcessOption(
            new MasterSheetProcessOptionRequestDto
            {
                ProcessName = " Heat treatment ",
                VendorName = " Vendor C ",
                Source = "Outsource"
            });

        var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var option = Assert.IsType<MasterSheetProcessOptionDto>(created.Value);
        Assert.Equal("Heat treatment", option.ProcessName);
        Assert.Equal("Vendor C", option.VendorName);

        var listResult = await controller.GetProcessOptions();
        var listOk = Assert.IsType<OkObjectResult>(listResult.Result);
        var options = Assert.IsAssignableFrom<
            IReadOnlyList<MasterSheetProcessOptionDto>>(listOk.Value);
        Assert.Single(options);

        var deleteResult = await controller.DeleteProcessOption(option.Id);
        Assert.IsType<NoContentResult>(deleteResult);
        Assert.Empty(await context.MasterSheetProcessOptions.ToListAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }
}
