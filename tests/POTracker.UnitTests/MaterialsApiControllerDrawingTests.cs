using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POTracker.Contracts.Api;
using POTracker.Controllers.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;

namespace POTracker.UnitTests;

public sealed class MaterialsApiControllerDrawingTests
{
    [Fact]
    public async Task UploadFiles_AppliesDrawingToEveryMatchingDrawingCode()
    {
        var environment =
            CreateTestEnvironment();

        try
        {
            await using var context =
                CreateContext();

            var selectedMaterial =
                new Material
                {
                    MaterialNo = "MAT-100",
                    DrawingNo = "SWD 10007338793 000",
                    DrawingVersion = "00"
                };

            var matchingMaterial =
                new Material
                {
                    MaterialNo = "MAT-200",
                    DrawingNo = " swd 10007338793 000 ",
                    DrawingVersion = " 00 ",
                    DrawingFilePath =
                        "/api/private-documents/drawings/old.pdf"
                };

            var unrelatedMaterial =
                new Material
                {
                    MaterialNo = "MAT-100",
                    DrawingNo = "SWD 10007338793 000",
                    DrawingVersion = "01"
                };

            context.Materials.AddRange(
                selectedMaterial,
                matchingMaterial,
                unrelatedMaterial);

            await context.SaveChangesAsync();

            using var memoryCache =
                new MemoryCache(
                    new MemoryCacheOptions());

            var privateDocumentStorage =
                new PrivateDocumentStorageService(
                    environment,
                    Options.Create(
                        new PrivateDocumentStorageOptions()));

            var controller =
                new MaterialsApiController(
                    context,
                    environment,
                    new PendingInvoiceMatcher(context),
                    new ExternalStockService(
                        new TestHttpClientFactory(),
                        memoryCache,
                        NullLogger<ExternalStockService>.Instance),
                    privateDocumentStorage,
                    new PrivateDocumentReferenceService());

            var drawingContent =
                Encoding.UTF8.GetBytes(
                    "shared drawing");

            await using var drawingStream =
                new MemoryStream(
                    drawingContent);

            var drawingFile =
                new FormFile(
                    drawingStream,
                    0,
                    drawingContent.Length,
                    "drawingFile",
                    "assembly.pdf");

            var result =
                await controller.UploadFiles(
                    selectedMaterial.Id,
                    null,
                    drawingFile);

            var ok =
                Assert.IsType<OkObjectResult>(
                    result.Result);

            var response =
                Assert.IsType<MaterialSaveResponseDto>(
                    ok.Value);

            context.ChangeTracker.Clear();

            var savedMaterials =
                await context.Materials
                    .OrderBy(material =>
                        material.Id)
                    .ToListAsync();

            Assert.NotNull(
                savedMaterials[0].DrawingFilePath);

            Assert.Equal(
                savedMaterials[0].DrawingFilePath,
                savedMaterials[1].DrawingFilePath);

            Assert.Equal(
                savedMaterials[0].DrawingUpdatedDate,
                savedMaterials[1].DrawingUpdatedDate);

            Assert.Null(
                savedMaterials[2].DrawingFilePath);

            Assert.Contains(
                "applied to 2 records",
                response.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTestEnvironment(
                environment);
        }
    }

    [Fact]
    public async Task UploadFiles_SavesHandSketchAndOutsourceDrawingForMatchingMaterials()
    {
        var environment =
            CreateTestEnvironment();

        try
        {
            await using var context =
                CreateContext();

            var selectedMaterial =
                new Material
                {
                    MaterialNo = "MAT-150"
                };

            var matchingMaterial =
                new Material
                {
                    MaterialNo = " mat-150 "
                };

            context.Materials.AddRange(
                selectedMaterial,
                matchingMaterial);

            await context.SaveChangesAsync();

            using var memoryCache =
                new MemoryCache(
                    new MemoryCacheOptions());

            var privateDocumentStorage =
                new PrivateDocumentStorageService(
                    environment,
                    Options.Create(
                        new PrivateDocumentStorageOptions()));

            var controller =
                new MaterialsApiController(
                    context,
                    environment,
                    new PendingInvoiceMatcher(context),
                    new ExternalStockService(
                        new TestHttpClientFactory(),
                        memoryCache,
                        NullLogger<ExternalStockService>.Instance),
                    privateDocumentStorage,
                    new PrivateDocumentReferenceService());

            var handSketchContent =
                Encoding.UTF8.GetBytes(
                    "hand sketch image");

            var outsourceDrawingContent =
                Encoding.UTF8.GetBytes(
                    "ISO-10303-21 outsourced step drawing");

            await using var handSketchStream =
                new MemoryStream(
                    handSketchContent);

            await using var outsourceDrawingStream =
                new MemoryStream(
                    outsourceDrawingContent);

            var handSketchFile =
                new FormFile(
                    handSketchStream,
                    0,
                    handSketchContent.Length,
                    "handSketchFile",
                    "shop-sketch.jpg");

            var outsourceDrawingFile =
                new FormFile(
                    outsourceDrawingStream,
                    0,
                    outsourceDrawingContent.Length,
                    "outsourceDrawingFile",
                    "vendor-part.step");

            var result =
                await controller.UploadFiles(
                    selectedMaterial.Id,
                    null,
                    null,
                    handSketchFile,
                    outsourceDrawingFile);

            var ok =
                Assert.IsType<OkObjectResult>(
                    result.Result);

            var response =
                Assert.IsType<MaterialSaveResponseDto>(
                    ok.Value);

            context.ChangeTracker.Clear();

            var savedMaterials =
                await context.Materials
                    .OrderBy(material =>
                        material.Id)
                    .ToListAsync();

            Assert.All(
                savedMaterials,
                material =>
                {
                    Assert.StartsWith(
                        "/api/private-documents/hand-sketches/",
                        material.HandSketchFilePath);

                    Assert.StartsWith(
                        "/api/private-documents/outsource-drawings/",
                        material.OutsourceDrawingFilePath);
                });

            Assert.Equal(
                savedMaterials[0].HandSketchFilePath,
                savedMaterials[1].HandSketchFilePath);

            Assert.Equal(
                savedMaterials[0].OutsourceDrawingFilePath,
                savedMaterials[1].OutsourceDrawingFilePath);

            Assert.True(response.Success);
        }
        finally
        {
            DeleteTestEnvironment(
                environment);
        }
    }

    [Fact]
    public async Task DeleteDrawing_RemovesDrawingFromEveryMatchingMaterialNumber()
    {
        var environment =
            CreateTestEnvironment();

        try
        {
            await using var context =
                CreateContext();

            var selectedMaterial =
                new Material
                {
                    MaterialNo = "MAT-300",
                    PDFFilePath =
                        "/api/private-documents/pdfs/material.pdf"
                };

            var matchingMaterial =
                new Material
                {
                    MaterialNo = " mat-300 "
                };

            var unrelatedMaterial =
                new Material
                {
                    MaterialNo = "MAT-400",
                    DrawingFilePath =
                        "/api/private-documents/drawings/unrelated.pdf"
                };

            context.Materials.AddRange(
                selectedMaterial,
                matchingMaterial,
                unrelatedMaterial);

            await context.SaveChangesAsync();

            using var memoryCache =
                new MemoryCache(
                    new MemoryCacheOptions());

            var privateDocumentStorage =
                new PrivateDocumentStorageService(
                    environment,
                    Options.Create(
                        new PrivateDocumentStorageOptions()));

            var documentReferences =
                new PrivateDocumentReferenceService();

            var controller =
                new MaterialsApiController(
                    context,
                    environment,
                    new PendingInvoiceMatcher(context),
                    new ExternalStockService(
                        new TestHttpClientFactory(),
                        memoryCache,
                        NullLogger<ExternalStockService>.Instance),
                    privateDocumentStorage,
                    documentReferences);

            var drawingContent =
                Encoding.UTF8.GetBytes(
                    "drawing to delete");

            await using var drawingStream =
                new MemoryStream(
                    drawingContent);

            var drawingFile =
                new FormFile(
                    drawingStream,
                    0,
                    drawingContent.Length,
                    "drawingFile",
                    "delete-me.pdf");

            await controller.UploadFiles(
                selectedMaterial.Id,
                null,
                drawingFile);

            var sharedDrawingPath =
                selectedMaterial.DrawingFilePath;

            Assert.True(
                documentReferences.TryNormalizeRelativePath(
                    sharedDrawingPath,
                    "drawings",
                    out var sharedDrawingRelativePath));

            Assert.True(
                privateDocumentStorage.Exists(
                    sharedDrawingRelativePath));

            var result =
                await controller.DeleteDrawing(
                    selectedMaterial.Id);

            var ok =
                Assert.IsType<OkObjectResult>(
                    result.Result);

            var response =
                Assert.IsType<MaterialSaveResponseDto>(
                    ok.Value);

            context.ChangeTracker.Clear();

            var savedMaterials =
                await context.Materials
                    .OrderBy(material =>
                        material.Id)
                    .ToListAsync();

            Assert.Null(
                savedMaterials[0].DrawingFilePath);

            Assert.Null(
                savedMaterials[1].DrawingFilePath);

            Assert.Null(
                savedMaterials[0].DrawingUpdatedDate);

            Assert.Null(
                savedMaterials[1].DrawingUpdatedDate);

            Assert.Equal(
                "/api/private-documents/pdfs/material.pdf",
                savedMaterials[0].PDFFilePath);

            Assert.Equal(
                "/api/private-documents/drawings/unrelated.pdf",
                savedMaterials[2].DrawingFilePath);

            Assert.False(
                privateDocumentStorage.Exists(
                    sharedDrawingRelativePath));

            Assert.Contains(
                "deleted successfully from 2 records",
                response.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTestEnvironment(
                environment);
        }
    }

    private static ApplicationDbContext
        CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<
                ApplicationDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid()
                        .ToString("N"))
                .Options;

        return new ApplicationDbContext(
            options);
    }

    private static TestWebHostEnvironment
        CreateTestEnvironment()
    {
        var contentRoot =
            Path.Combine(
                Path.GetTempPath(),
                "POTracker.UnitTests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            contentRoot);

        return new TestWebHostEnvironment
        {
            ApplicationName =
                "POTracker.UnitTests",

            EnvironmentName =
                "Development",

            ContentRootPath =
                contentRoot,

            WebRootPath =
                Path.Combine(
                    contentRoot,
                    "wwwroot")
        };
    }

    private static void DeleteTestEnvironment(
        TestWebHostEnvironment environment)
    {
        if (Directory.Exists(
                environment.ContentRootPath))
        {
            Directory.Delete(
                environment.ContentRootPath,
                recursive: true);
        }
    }

    private sealed class TestHttpClientFactory
        : IHttpClientFactory
    {
        public HttpClient CreateClient(
            string name)
        {
            return new HttpClient(
                new EmptyStockResponseHandler())
            {
                BaseAddress =
                    new Uri(
                        "http://localhost/")
            };
        }
    }

    private sealed class EmptyStockResponseHandler
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.OK)
                {
                    Content =
                        new StringContent(
                            "[]",
                            Encoding.UTF8,
                            "application/json")
                });
        }
    }

    private sealed class TestWebHostEnvironment
        : IWebHostEnvironment
    {
        public string ApplicationName
        {
            get;
            set;
        } = string.Empty;

        public IFileProvider WebRootFileProvider
        {
            get;
            set;
        } = new NullFileProvider();

        public string WebRootPath
        {
            get;
            set;
        } = string.Empty;

        public string EnvironmentName
        {
            get;
            set;
        } = string.Empty;

        public string ContentRootPath
        {
            get;
            set;
        } = string.Empty;

        public IFileProvider ContentRootFileProvider
        {
            get;
            set;
        } = new NullFileProvider();
    }
}
