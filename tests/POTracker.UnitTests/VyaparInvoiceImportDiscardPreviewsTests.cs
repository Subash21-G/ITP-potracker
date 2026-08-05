using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using POTracker.Controllers;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;
using POTracker.ViewModels;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace POTracker.UnitTests;

public sealed class VyaparInvoiceImportDiscardPreviewsTests
{
    private const string TemporaryPreviewSessionKey =
        "POTracker.InvoicePreviewTemporaryPaths";

    [Fact]
    public async Task
        DiscardPreviewsApi_RejectsMoreThanTwoHundredPaths()
    {
        await using var scope =
            CreateScope();

        var request =
            new InvoicePreviewDiscardRequest
            {
                UploadedFilePaths =
                    Enumerable.Range(
                            1,
                            201)
                        .Select(index =>
                            (string?)
                            (
                                "/api/private-documents/invoices/" +
                                $"preview-{index}.pdf"
                            ))
                        .ToList()
            };

        var result =
            await scope.Controller
                .DiscardPreviewsApi(
                    request);

        Assert.IsType<BadRequestObjectResult>(
            result);
    }

    [Fact]
    public async Task
        DiscardPreviewsApi_RejectsInvalidNonInvoicePath()
    {
        await using var scope =
            CreateScope();

        var relativePath =
            await scope.SavePrivateAsync(
                "po",
                "order.pdf",
                "purchase-order");

        RegisterPaths(
            scope.Session,
            relativePath);

        var result =
            await scope.Controller
                .DiscardPreviewsApi(
                    CreateRequest(
                        "/api/private-documents/po/order.pdf"));

        AssertCounts(
            result,
            submitted: 1,
            uniqueRegistered: 0,
            deleted: 0,
            protectedCount: 0,
            missing: 0,
            invalid: 1,
            unregistered: 0);

        Assert.True(
            scope.Storage.Exists(
                relativePath));
    }

    [Fact]
    public async Task
        DiscardPreviewsApi_RejectsUnregisteredPathWithoutDeletingFiles()
    {
        await using var scope =
            CreateScope();

        var relativePath =
            await scope.SavePrivateAsync(
                "invoices",
                "unregistered.pdf",
                "private-unregistered");

        var publicPath =
            await scope.SavePublicInvoiceAsync(
                "unregistered.pdf",
                "public-unregistered");

        var result =
            await scope.Controller
                .DiscardPreviewsApi(
                    CreateRequest(
                        "/api/private-documents/invoices/" +
                        "unregistered.pdf"));

        AssertCounts(
            result,
            submitted: 1,
            uniqueRegistered: 0,
            deleted: 0,
            protectedCount: 0,
            missing: 0,
            invalid: 0,
            unregistered: 1);

        Assert.True(
            scope.Storage.Exists(
                relativePath));

        Assert.True(
            File.Exists(
                publicPath));
    }

    [Fact]
    public async Task
        DiscardPreviewsApi_ProtectsDatabaseReferencedFiles()
    {
        await using var scope =
            CreateScope();

        var relativePath =
            await scope.SavePrivateAsync(
                "invoices",
                "protected.pdf",
                "private-protected");

        var publicPath =
            await scope.SavePublicInvoiceAsync(
                "protected.pdf",
                "public-protected");

        RegisterPaths(
            scope.Session,
            relativePath);

        scope.Context.Invoices.Add(
            new Invoice
            {
                InvoiceNumber =
                    "INV-PROTECTED",

                UploadedFileName =
                    "protected.pdf",

                UploadedFilePath =
                    "/api/private-documents/invoices/" +
                    "protected.pdf"
            });

        await scope.Context.SaveChangesAsync();

        var result =
            await scope.Controller
                .DiscardPreviewsApi(
                    CreateRequest(
                        "/api/private-documents/invoices/" +
                        "protected.pdf"));

        AssertCounts(
            result,
            submitted: 1,
            uniqueRegistered: 1,
            deleted: 0,
            protectedCount: 1,
            missing: 0,
            invalid: 0,
            unregistered: 0);

        Assert.True(
            scope.Storage.Exists(
                relativePath));

        Assert.True(
            File.Exists(
                publicPath));

        Assert.False(
            scope.Session.TryGetValue(
                TemporaryPreviewSessionKey,
                out _));
    }

    [Fact]
    public async Task
        DiscardPreviewsApi_DeletesRegisteredUnreferencedPrivateAndPublicFiles()
    {
        await using var scope =
            CreateScope();

        var relativePath =
            await scope.SavePrivateAsync(
                "invoices",
                "temporary.pdf",
                "private-temporary");

        var publicPath =
            await scope.SavePublicInvoiceAsync(
                "temporary.pdf",
                "public-temporary");

        RegisterPaths(
            scope.Session,
            relativePath);

        var result =
            await scope.Controller
                .DiscardPreviewsApi(
                    CreateRequest(
                        "/api/private-documents/invoices/" +
                        "temporary.pdf"));

        AssertCounts(
            result,
            submitted: 1,
            uniqueRegistered: 1,
            deleted: 1,
            protectedCount: 0,
            missing: 0,
            invalid: 0,
            unregistered: 0);

        Assert.False(
            scope.Storage.Exists(
                relativePath));

        Assert.False(
            File.Exists(
                publicPath));

        Assert.False(
            scope.Session.TryGetValue(
                TemporaryPreviewSessionKey,
                out _));
    }

    [Fact]
    public async Task
        DiscardPreviewsApi_CountsRegisteredMissingFileWithoutThrowing()
    {
        await using var scope =
            CreateScope();

        const string relativePath =
            "invoices/missing.pdf";

        RegisterPaths(
            scope.Session,
            relativePath);

        var result =
            await scope.Controller
                .DiscardPreviewsApi(
                    CreateRequest(
                        "/api/private-documents/invoices/" +
                        "missing.pdf"));

        AssertCounts(
            result,
            submitted: 1,
            uniqueRegistered: 1,
            deleted: 0,
            protectedCount: 0,
            missing: 1,
            invalid: 0,
            unregistered: 0);

        Assert.False(
            scope.Storage.Exists(
                relativePath));

        Assert.False(
            scope.Session.TryGetValue(
                TemporaryPreviewSessionKey,
                out _));
    }

    private static InvoicePreviewDiscardRequest
        CreateRequest(
            params string?[] paths)
    {
        return new InvoicePreviewDiscardRequest
        {
            UploadedFilePaths =
                paths.ToList()
        };
    }

    private static void RegisterPaths(
        ISession session,
        params string[] relativePaths)
    {
        session.SetString(
            TemporaryPreviewSessionKey,
            JsonSerializer.Serialize(
                relativePaths));
    }

    private static void AssertCounts(
        IActionResult result,
        int submitted,
        int uniqueRegistered,
        int deleted,
        int protectedCount,
        int missing,
        int invalid,
        int unregistered)
    {
        var okResult =
            Assert.IsType<OkObjectResult>(
                result);

        Assert.NotNull(
            okResult.Value);

        var value =
            okResult.Value!;

        Assert.Equal(
            submitted,
            ReadInt(
                value,
                "submittedPathCount"));

        Assert.Equal(
            uniqueRegistered,
            ReadInt(
                value,
                "uniqueRegisteredPathCount"));

        Assert.Equal(
            deleted,
            ReadInt(
                value,
                "deletedPreviewCount"));

        Assert.Equal(
            protectedCount,
            ReadInt(
                value,
                "protectedPreviewCount"));

        Assert.Equal(
            missing,
            ReadInt(
                value,
                "missingPreviewCount"));

        Assert.Equal(
            invalid,
            ReadInt(
                value,
                "invalidPathCount"));

        Assert.Equal(
            unregistered,
            ReadInt(
                value,
                "unregisteredPathCount"));
    }

    private static int ReadInt(
        object value,
        string propertyName)
    {
        var property =
            value.GetType()
                .GetProperty(
                    propertyName);

        Assert.NotNull(
            property);

        return Assert.IsType<int>(
            property!.GetValue(
                value));
    }

    private static TestScope CreateScope()
    {
        return new TestScope();
    }

    private sealed class TestScope :
        IAsyncDisposable
    {
        public TestScope()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "POTracker.UnitTests",
                    Guid.NewGuid()
                        .ToString("N"));

            Directory.CreateDirectory(
                RootPath);

            Environment =
                new TestWebHostEnvironment
                {
                    ContentRootPath =
                        RootPath,

                    WebRootPath =
                        Path.Combine(
                            RootPath,
                            "wwwroot")
                };

            Directory.CreateDirectory(
                Environment.WebRootPath);

            Storage =
                new PrivateDocumentStorageService(
                    Environment,
                    Options.Create(
                        new PrivateDocumentStorageOptions
                        {
                            RootPath =
                                Path.Combine(
                                    "App_Data",
                                    "private-uploads")
                        }));

            var databaseOptions =
                new DbContextOptionsBuilder<
                        ApplicationDbContext>()
                    .UseInMemoryDatabase(
                        "InvoicePreviewCleanup-" +
                        Guid.NewGuid()
                            .ToString("N"))
                    .Options;

            Context =
                new ApplicationDbContext(
                    databaseOptions);

            Session =
                new TestSession();

            var httpContext =
                new DefaultHttpContext();

            httpContext.Session =
                Session;

            Controller =
                new VyaparInvoiceImportController(
                    Context,
                    Environment,
                    Storage,
                    new PrivateDocumentReferenceService())
                {
                    ControllerContext =
                        new ControllerContext
                        {
                            HttpContext =
                                httpContext
                        }
                };
        }

        public string RootPath
        {
            get;
        }

        public TestWebHostEnvironment Environment
        {
            get;
        }

        public PrivateDocumentStorageService Storage
        {
            get;
        }

        public ApplicationDbContext Context
        {
            get;
        }

        public TestSession Session
        {
            get;
        }

        public VyaparInvoiceImportController Controller
        {
            get;
        }

        public async Task<string> SavePrivateAsync(
            string category,
            string storedFileName,
            string content)
        {
            await using var source =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(
                        content));

            var stored =
                await Storage
                    .SaveWithStoredFileNameAsync(
                        source,
                        category,
                        storedFileName);

            return stored.RelativePath;
        }

        public async Task<string> SavePublicInvoiceAsync(
            string storedFileName,
            string content)
        {
            var publicInvoiceDirectory =
                Path.Combine(
                    Environment.WebRootPath,
                    "uploads",
                    "invoices");

            Directory.CreateDirectory(
                publicInvoiceDirectory);

            var fullPath =
                Path.Combine(
                    publicInvoiceDirectory,
                    storedFileName);

            await File.WriteAllTextAsync(
                fullPath,
                content);

            return fullPath;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();

            if (
                Directory.Exists(
                    RootPath)
            )
            {
                Directory.Delete(
                    RootPath,
                    recursive: true);
            }
        }
    }

    private sealed class TestSession :
        ISession
    {
        private readonly Dictionary<string, byte[]>
            _values =
                new(
                    StringComparer.Ordinal);

        public bool IsAvailable =>
            true;

        public string Id
        {
            get;
        } = Guid.NewGuid()
            .ToString("N");

        public IEnumerable<string> Keys =>
            _values.Keys;

        public void Clear()
        {
            _values.Clear();
        }

        public Task CommitAsync(
            CancellationToken cancellationToken =
                default)
        {
            return Task.CompletedTask;
        }

        public Task LoadAsync(
            CancellationToken cancellationToken =
                default)
        {
            return Task.CompletedTask;
        }

        public void Remove(
            string key)
        {
            _values.Remove(
                key);
        }

        public void Set(
            string key,
            byte[] value)
        {
            _values[key] =
                value.ToArray();
        }

        public bool TryGetValue(
            string key,
            [NotNullWhen(true)]
            out byte[]? value)
        {
            if (
                _values.TryGetValue(
                    key,
                    out var storedValue)
            )
            {
                value =
                    storedValue.ToArray();

                return true;
            }

            value =
                null;

            return false;
        }
    }

    private sealed class TestWebHostEnvironment :
        IWebHostEnvironment
    {
        public string ApplicationName
        {
            get;
            set;
        } = "POTracker.UnitTests";

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
        } = "Testing";

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
