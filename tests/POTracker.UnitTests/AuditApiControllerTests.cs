using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using POTracker.Contracts.Api;
using POTracker.Controllers.Api;
using POTracker.Data;
using POTracker.Models;
using Xunit;

namespace POTracker.UnitTests;

public sealed class AuditApiControllerTests
{
    [Fact]
    public async Task Index_ReturnsFallbackMaterials_WhenNoAuditLogsExist()
    {
        await using var context = CreateContext();
        await AddMaterial(context, "MAT-001", "PO-1001", "In Progress", "Dispatched", "John");

        var controller = new AuditApiController(context);
        var result = await controller.Index(
            search: null,
            owner: null,
            processStatus: null,
            dispatchStatus: null,
            dateFrom: null,
            dateTo: null,
            materialNo: null,
            poNumber: null,
            action: null,
            columnProcessStatus: null,
            columnDispatchStatus: null,
            columnOwner: null,
            createdDateFrom: null,
            createdDateTo: null,
            sortColumn: null,
            sortDirection: null,
            excelFilters: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuditPagedResponseDto>(ok.Value);

        Assert.Equal(1, response.TotalCount);
        var item = Assert.Single(response.Items);
        Assert.Equal("MAT-001", item.MaterialNo);
        Assert.Equal("PO-1001", item.PONumber);
        Assert.Equal("Material schedule updated", item.Action);
    }

    [Fact]
    public async Task Index_ReturnsRealAuditLogs_WhenAuditLogsExist()
    {
        await using var context = CreateContext();
        context.AuditLogs.Add(new AuditLog
        {
            Action = "Purchase Order Created",
            MaterialNo = "MAT-AUDIT-1",
            PONumber = "PO-2001",
            ProcessStatus = "Pending",
            DispatchStatus = "Pending",
            Owner = "Operations Lead",
            UserName = "AdminUser",
            CreatedDate = DateTime.Now
        });
        await context.SaveChangesAsync();

        var controller = new AuditApiController(context);
        var result = await controller.Index(
            search: null,
            owner: null,
            processStatus: null,
            dispatchStatus: null,
            dateFrom: null,
            dateTo: null,
            materialNo: null,
            poNumber: null,
            action: null,
            columnProcessStatus: null,
            columnDispatchStatus: null,
            columnOwner: null,
            createdDateFrom: null,
            createdDateTo: null,
            sortColumn: null,
            sortDirection: null,
            excelFilters: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuditPagedResponseDto>(ok.Value);

        Assert.Equal(1, response.TotalCount);
        var item = Assert.Single(response.Items);
        Assert.Equal("MAT-AUDIT-1", item.MaterialNo);
        Assert.Equal("PO-2001", item.PONumber);
        Assert.Equal("Purchase Order Created", item.Action);
        Assert.Equal("Operations Lead", item.Owner);
    }

    [Fact]
    public async Task Index_FiltersAuditLogs_BySearchAndStatus()
    {
        await using var context = CreateContext();
        context.AuditLogs.AddRange(
            new AuditLog
            {
                Action = "Material Created",
                MaterialNo = "MAT-ALPHA",
                PONumber = "PO-3001",
                ProcessStatus = "Completed",
                DispatchStatus = "Dispatched",
                Owner = "AlphaOwner",
                CreatedDate = DateTime.Now
            },
            new AuditLog
            {
                Action = "Material Created",
                MaterialNo = "MAT-BETA",
                PONumber = "PO-3002",
                ProcessStatus = "Pending",
                DispatchStatus = "Pending",
                Owner = "BetaOwner",
                CreatedDate = DateTime.Now
            });
        await context.SaveChangesAsync();

        var controller = new AuditApiController(context);
        var result = await controller.Index(
            search: "ALPHA",
            owner: null,
            processStatus: null,
            dispatchStatus: null,
            dateFrom: null,
            dateTo: null,
            materialNo: null,
            poNumber: null,
            action: null,
            columnProcessStatus: null,
            columnDispatchStatus: null,
            columnOwner: null,
            createdDateFrom: null,
            createdDateTo: null,
            sortColumn: null,
            sortDirection: null,
            excelFilters: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuditPagedResponseDto>(ok.Value);

        Assert.Equal(1, response.TotalCount);
        Assert.Equal("MAT-ALPHA", response.Items[0].MaterialNo);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Material> AddMaterial(
        ApplicationDbContext context,
        string materialNo,
        string poNumber,
        string processStatus,
        string dispatchStatus,
        string owner)
    {
        var purchaseOrder = new PurchaseOrder
        {
            PONumber = poNumber
        };

        var material = new Material
        {
            PurchaseOrder = purchaseOrder,
            MaterialNo = materialNo,
            ProcessStatus = processStatus,
            DispatchStatus = dispatchStatus,
            AssignedDashboard = owner,
            CreatedDate = DateTime.Now
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        return material;
    }
}

