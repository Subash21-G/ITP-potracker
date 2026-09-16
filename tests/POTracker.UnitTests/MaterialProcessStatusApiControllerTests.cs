using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Controllers.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.UnitTests;

public sealed class MaterialProcessStatusApiControllerTests
{
    [Fact]
    public async Task Update_ChangesOnlyTheSelectedMaterialProcessStatus()
    {
        await using var context = CreateContext();
        var material = new Material
        {
            MaterialNo = "MAT-100",
            ProcessStatus = "Pending",
            Remark = "Keep this remark"
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        var controller = new MaterialProcessStatusApiController(context);
        var result = await controller.Update(
            material.Id,
            new MaterialProcessStatusUpdateRequestDto
            {
                ProcessStatus = "In Process"
            });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MaterialProcessStatusUpdateResponseDto>(ok.Value);
        var saved = await context.Materials.SingleAsync();

        Assert.Equal("In Process", saved.ProcessStatus);
        Assert.Equal("Keep this remark", saved.Remark);
        Assert.NotNull(saved.UpdatedDate);
        Assert.Equal("In Process", response.ProcessStatus);
    }

    [Fact]
    public async Task Update_RejectsAnUnsupportedProcessStatus()
    {
        await using var context = CreateContext();
        var material = new Material
        {
            MaterialNo = "MAT-100",
            ProcessStatus = "Pending"
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        var controller = new MaterialProcessStatusApiController(context);
        var result = await controller.Update(
            material.Id,
            new MaterialProcessStatusUpdateRequestDto
            {
                ProcessStatus = "Unknown"
            });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Pending", material.ProcessStatus);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }
}
