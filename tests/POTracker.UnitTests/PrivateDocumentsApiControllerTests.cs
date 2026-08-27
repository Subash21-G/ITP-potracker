using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using POTracker.Controllers.Api;
using POTracker.Services;
using System.Text;

namespace POTracker.UnitTests;

public sealed class PrivateDocumentsApiControllerTests
{
    [Fact]
    public async Task Download_ReturnsStoredDocument()
    {
        var temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            var storage =
                CreateStorage(
                    temporaryRoot);

            await using var source =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(
                        "Purchase order PDF test"));

            var stored =
                await storage.SaveAsync(
                    source,
                    "po",
                    "PO-1001.pdf");

            var controller =
                CreateController(storage);

            var result =
                controller.Download(
                    "po",
                    stored.StoredFileName);

            var fileResult =
                Assert.IsType<FileStreamResult>(
                    result);

            Assert.Equal(
                "application/pdf",
                fileResult.ContentType);

            Assert.Equal(
                stored.StoredFileName,
                fileResult.FileDownloadName);

            Assert.True(
                fileResult.EnableRangeProcessing);

            Assert.Equal(
                "nosniff",
                controller.Response
                    .Headers
                    .XContentTypeOptions
                    .ToString());

            await fileResult.FileStream.DisposeAsync();
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot);
        }
    }

    [Theory]
    [InlineData("drawing.pdf", "application/pdf")]
    [InlineData("drawing.png", "image/png")]
    public async Task Download_ReturnsInlinePreviewWhenRequested(
        string fileName,
        string expectedContentType)
    {
        var temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            var storage =
                CreateStorage(
                    temporaryRoot);

            await using var source =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(
                        "%PDF-1.7 drawing preview"));

            var stored =
                await storage.SaveAsync(
                    source,
                    "drawings",
                    fileName);

            var controller =
                CreateController(storage);

            var result =
                controller.Download(
                    "drawings",
                    stored.StoredFileName,
                    inline: true);

            var fileResult =
                Assert.IsType<FileStreamResult>(
                    result);

            Assert.Equal(
                expectedContentType,
                fileResult.ContentType);

            Assert.True(
                string.IsNullOrEmpty(
                    fileResult.FileDownloadName));

            Assert.True(
                fileResult.EnableRangeProcessing);

            await fileResult.FileStream.DisposeAsync();
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot);
        }
    }

    [Fact]
    public void Download_ReturnsNotFoundForMissingDocument()
    {
        var temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            var storage =
                CreateStorage(
                    temporaryRoot);

            var controller =
                CreateController(storage);

            var result =
                controller.Download(
                    "po",
                    "missing.pdf");

            Assert.IsType<NotFoundObjectResult>(
                result);
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot);
        }
    }

    [Theory]
    [InlineData("..", "document.pdf")]
    [InlineData("../po", "document.pdf")]
    [InlineData("po", "../document.pdf")]
    [InlineData("po", "..")]
    public void Download_BlocksUnsafePathSegments(
        string category,
        string storedFileName)
    {
        var temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            var storage =
                CreateStorage(
                    temporaryRoot);

            var controller =
                CreateController(storage);

            var result =
                controller.Download(
                    category,
                    storedFileName);

            Assert.IsType<BadRequestObjectResult>(
                result);
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot);
        }
    }

    private static PrivateDocumentsApiController
        CreateController(
            PrivateDocumentStorageService storage)
    {
        return new PrivateDocumentsApiController(
            storage)
        {
            ControllerContext =
                new ControllerContext
                {
                    HttpContext =
                        new DefaultHttpContext()
                }
        };
    }

    private static PrivateDocumentStorageService
        CreateStorage(
            string temporaryRoot)
    {
        var environment =
            new TestWebHostEnvironment
            {
                ContentRootPath =
                    temporaryRoot,

                WebRootPath =
                    Path.Combine(
                        temporaryRoot,
                        "wwwroot")
            };

        var options =
            Options.Create(
                new PrivateDocumentStorageOptions
                {
                    RootPath =
                        Path.Combine(
                            "App_Data",
                            "private-uploads")
                });

        return new PrivateDocumentStorageService(
            environment,
            options);
    }

    private static string CreateTemporaryRoot()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "POTrackerTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);

        return path;
    }

    private static void DeleteTemporaryRoot(
        string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(
                path,
                recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment :
        IWebHostEnvironment
    {
        public string ApplicationName { get; set; } =
            "POTracker.UnitTests";

        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();

        public string WebRootPath { get; set; } =
            string.Empty;

        public string EnvironmentName { get; set; } =
            "Testing";

        public string ContentRootPath { get; set; } =
            string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
