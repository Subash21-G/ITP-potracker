using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using POTracker.Services;
using System.Security.Cryptography;
using System.Text;

namespace POTracker.UnitTests;

public sealed class PrivateDocumentStorageServiceTests
{
    [Fact]
    public async Task SaveAsync_StoresFileOutsideWebRootAndCalculatesHash()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var service = CreateService(temporaryRoot);

            var content =
                Encoding.UTF8.GetBytes(
                    "POTracker private document test");

            await using var source =
                new MemoryStream(content);

            var stored =
                await service.SaveAsync(
                    source,
                    "po",
                    "Purchase Order 1001.PDF");

            var expectedHash =
                Convert.ToHexString(
                    SHA256.HashData(content));

            var fullPath =
                service.ResolveDocumentPath(
                    stored.RelativePath);

            Assert.True(
                File.Exists(fullPath));

            Assert.Equal(
                expectedHash,
                stored.Sha256);

            Assert.Equal(
                content.Length,
                stored.Size);

            Assert.EndsWith(
                ".pdf",
                stored.StoredFileName,
                StringComparison.OrdinalIgnoreCase);

            Assert.Equal(
                $"po/{stored.StoredFileName}",
                stored.RelativePath);

            Assert.False(
                fullPath.Contains(
                    "wwwroot",
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot);
        }
    }

    [Fact]
    public async Task OpenRead_ReturnsSavedDocumentContent()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var service = CreateService(temporaryRoot);

            var expectedContent =
                Encoding.UTF8.GetBytes(
                    "Invoice file content");

            await using var source =
                new MemoryStream(
                    expectedContent);

            var stored =
                await service.SaveAsync(
                    source,
                    "invoices",
                    "INV-1001.xlsx");

            await using var storedStream =
                service.OpenRead(
                    stored.RelativePath);

            await using var result =
                new MemoryStream();

            await storedStream.CopyToAsync(
                result);

            Assert.Equal(
                expectedContent,
                result.ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot);
        }
    }

    [Fact]
    public async Task DeleteIfExists_RemovesStoredDocument()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var service = CreateService(temporaryRoot);

            await using var source =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(
                        "Drawing content"));

            var stored =
                await service.SaveAsync(
                    source,
                    "drawings",
                    "drawing.pdf");

            Assert.True(
                service.Exists(
                    stored.RelativePath));

            service.DeleteIfExists(
                stored.RelativePath);

            Assert.False(
                service.Exists(
                    stored.RelativePath));
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot);
        }
    }

    [Fact]
    public void ResolveDocumentPath_BlocksPathTraversal()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var service = CreateService(temporaryRoot);

            Assert.Throws<
                InvalidOperationException>(
                () =>
                    service.ResolveDocumentPath(
                        "../outside.pdf"));
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot);
        }
    }

    [Fact]
    public async Task SaveAsync_BlocksUnsafeCategory()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var service = CreateService(temporaryRoot);

            await using var source =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(
                        "Unsafe category test"));

            await Assert.ThrowsAsync<
                ArgumentException>(
                () =>
                    service.SaveAsync(
                        source,
                        "../po",
                        "test.pdf"));
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot);
        }
    }

    private static PrivateDocumentStorageService
        CreateService(
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