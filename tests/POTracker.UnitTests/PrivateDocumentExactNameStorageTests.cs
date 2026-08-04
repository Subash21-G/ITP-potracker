using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using POTracker.Services;
using System.Text;

namespace POTracker.UnitTests;

public sealed class PrivateDocumentExactNameStorageTests
{
    [Fact]
    public async Task SaveWithStoredFileNameAsync_SavesExactNameAndContent()
    {
        var testEnvironment =
            CreateTestEnvironment();

        try
        {
            var service =
                CreateService(
                    testEnvironment);

            var content =
                Encoding.UTF8.GetBytes(
                    "invoice-content");

            using var source =
                new MemoryStream(
                    content);

            var stored =
                await service.SaveWithStoredFileNameAsync(
                    source,
                    "invoices",
                    "abc123_invoice.pdf");

            Assert.Equal(
                "abc123_invoice.pdf",
                stored.StoredFileName);

            Assert.Equal(
                "invoices/abc123_invoice.pdf",
                stored.RelativePath);

            Assert.Equal(
                content.Length,
                stored.Size);

            Assert.True(
                service.Exists(
                    stored.RelativePath));

            var savedContent =
                await File.ReadAllBytesAsync(
                    service.ResolveDocumentPath(
                        stored.RelativePath));

            Assert.Equal(
                content,
                savedContent);
        }
        finally
        {
            DeleteTestEnvironment(
                testEnvironment);
        }
    }

    [Fact]
    public async Task SaveWithStoredFileNameAsync_RejectsNestedFilename()
    {
        var testEnvironment =
            CreateTestEnvironment();

        try
        {
            var service =
                CreateService(
                    testEnvironment);

            using var source =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(
                        "unsafe"));

            await Assert.ThrowsAsync<ArgumentException>(
                () =>
                    service.SaveWithStoredFileNameAsync(
                        source,
                        "invoices",
                        "../unsafe.pdf"));
        }
        finally
        {
            DeleteTestEnvironment(
                testEnvironment);
        }
    }

    [Fact]
    public async Task SaveWithStoredFileNameAsync_DoesNotOverwriteOrDeleteExistingFile()
    {
        var testEnvironment =
            CreateTestEnvironment();

        try
        {
            var service =
                CreateService(
                    testEnvironment);

            var relativePath =
                "invoices/existing.pdf";

            var existingPath =
                service.ResolveDocumentPath(
                    relativePath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    existingPath)!);

            await File.WriteAllTextAsync(
                existingPath,
                "original-content");

            using var source =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(
                        "replacement-content"));

            await Assert.ThrowsAsync<IOException>(
                () =>
                    service.SaveWithStoredFileNameAsync(
                        source,
                        "invoices",
                        "existing.pdf"));

            var remainingContent =
                await File.ReadAllTextAsync(
                    existingPath);

            Assert.Equal(
                "original-content",
                remainingContent);
        }
        finally
        {
            DeleteTestEnvironment(
                testEnvironment);
        }
    }

    private static PrivateDocumentStorageService
        CreateService(
            TestWebHostEnvironment environment)
    {
        return new PrivateDocumentStorageService(
            environment,
            Options.Create(
                new PrivateDocumentStorageOptions()));
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

            ContentRootFileProvider =
                new PhysicalFileProvider(
                    contentRoot),

            WebRootPath =
                Path.Combine(
                    contentRoot,
                    "wwwroot"),

            WebRootFileProvider =
                new NullFileProvider()
        };
    }

    private static void DeleteTestEnvironment(
        TestWebHostEnvironment environment)
    {
        if (
            environment.ContentRootFileProvider is
            System.IDisposable disposableFileProvider)
        {
            disposableFileProvider.Dispose();
        }

        if (
            Directory.Exists(
                environment.ContentRootPath)
        )
        {
            Directory.Delete(
                environment.ContentRootPath,
                recursive: true);
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