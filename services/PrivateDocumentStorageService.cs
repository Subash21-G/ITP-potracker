using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace POTracker.Services;

/// <summary>
/// Stores business documents outside wwwroot so they cannot be
/// downloaded directly through ASP.NET Core static-file middleware.
/// </summary>
public sealed class PrivateDocumentStorageService
{
    private readonly string _rootPath;

    public PrivateDocumentStorageService(
        IWebHostEnvironment environment,
        IOptions<PrivateDocumentStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        var configuredPath =
            options.Value.RootPath?.Trim();

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath =
                Path.Combine(
                    "App_Data",
                    "private-uploads");
        }

        _rootPath =
            Path.IsPathRooted(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : Path.GetFullPath(
                    Path.Combine(
                        environment.ContentRootPath,
                        configuredPath));

        Directory.CreateDirectory(_rootPath);
    }

    public string RootPath => _rootPath;

    public async Task<StoredPrivateDocument> SaveAsync(
        Stream source,
        string category,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
        {
            throw new ArgumentException(
                "The supplied document stream is not readable.",
                nameof(source));
        }

        var safeCategory =
            NormalizeSinglePathSegment(
                category,
                nameof(category));

        var safeOriginalFileName =
            NormalizeFileName(originalFileName);

        var extension =
            Path.GetExtension(
                safeOriginalFileName);

        if (extension.Length > 20)
        {
            throw new InvalidOperationException(
                "The document extension is too long.");
        }

        var categoryFolder =
            ResolveCategoryFolder(
                safeCategory);

        Directory.CreateDirectory(
            categoryFolder);

        var storedFileName =
            $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_" +
            $"{Guid.NewGuid():N}" +
            extension.ToLowerInvariant();

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    categoryFolder,
                    storedFileName));

        EnsureInsideStorageRoot(fullPath);

        try
        {
            await using (
                var destination =
                    new FileStream(
                        fullPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        options:
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan))
            {
                await source.CopyToAsync(
                    destination,
                    cancellationToken);

                await destination.FlushAsync(
                    cancellationToken);
            }

            string sha256;

            await using (
                var hashStream =
                    new FileStream(
                        fullPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 81920,
                        options:
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan))
            {
                var hash =
                    await SHA256.HashDataAsync(
                        hashStream,
                        cancellationToken);

                sha256 =
                    Convert.ToHexString(hash);
            }

            var fileInfo =
                new FileInfo(fullPath);

            var relativePath =
                Path.GetRelativePath(
                        _rootPath,
                        fullPath)
                    .Replace(
                        Path.DirectorySeparatorChar,
                        '/');

            return new StoredPrivateDocument(
                OriginalFileName:
                    safeOriginalFileName,
                StoredFileName:
                    storedFileName,
                RelativePath:
                    relativePath,
                Size:
                    fileInfo.Length,
                Sha256:
                    sha256);
        }
        catch
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            throw;
        }
    }

    /// <summary>
    /// Saves a document using a caller-provided stored filename.
    /// This is intended for safely mirroring an already generated
    /// public upload filename into private storage.
    /// </summary>
    public async Task<StoredPrivateDocument>
        SaveWithStoredFileNameAsync(
            Stream source,
            string category,
            string storedFileName,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
        {
            throw new ArgumentException(
                "The supplied document stream is not readable.",
                nameof(source));
        }

        var safeCategory =
            NormalizeSinglePathSegment(
                category,
                nameof(category));

        var requestedStoredFileName =
            storedFileName?.Trim();

        if (string.IsNullOrWhiteSpace(
                requestedStoredFileName))
        {
            throw new ArgumentException(
                "A stored document filename is required.",
                nameof(storedFileName));
        }

        var safeStoredFileName =
            NormalizeFileName(
                requestedStoredFileName);

        if (!string.Equals(
                requestedStoredFileName,
                safeStoredFileName,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The stored document filename must be " +
                "one safe filename without a folder path.",
                nameof(storedFileName));
        }

        var extension =
            Path.GetExtension(
                safeStoredFileName);

        if (extension.Length > 20)
        {
            throw new InvalidOperationException(
                "The document extension is too long.");
        }

        var categoryFolder =
            ResolveCategoryFolder(
                safeCategory);

        Directory.CreateDirectory(
            categoryFolder);

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    categoryFolder,
                    safeStoredFileName));

        EnsureInsideStorageRoot(
            fullPath);

        var createdFile =
            false;

        try
        {
            await using (
                var destination =
                    new FileStream(
                        fullPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        options:
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan))
            {
                createdFile =
                    true;

                await source.CopyToAsync(
                    destination,
                    cancellationToken);

                await destination.FlushAsync(
                    cancellationToken);
            }

            string sha256;

            await using (
                var hashStream =
                    new FileStream(
                        fullPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 81920,
                        options:
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan))
            {
                var hash =
                    await SHA256.HashDataAsync(
                        hashStream,
                        cancellationToken);

                sha256 =
                    Convert.ToHexString(
                        hash);
            }

            var fileInfo =
                new FileInfo(
                    fullPath);

            var relativePath =
                Path.GetRelativePath(
                        _rootPath,
                        fullPath)
                    .Replace(
                        Path.DirectorySeparatorChar,
                        '/');

            return new StoredPrivateDocument(
                OriginalFileName:
                    safeStoredFileName,
                StoredFileName:
                    safeStoredFileName,
                RelativePath:
                    relativePath,
                Size:
                    fileInfo.Length,
                Sha256:
                    sha256);
        }
        catch
        {
            if (
                createdFile &&
                File.Exists(fullPath)
            )
            {
                File.Delete(
                    fullPath);
            }

            throw;
        }
    }

    public Stream OpenRead(
        string relativePath)
    {
        var fullPath =
            ResolveDocumentPath(
                relativePath);

        return new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            options:
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);
    }

    public bool Exists(
        string relativePath)
    {
        var fullPath =
            ResolveDocumentPath(
                relativePath);

        return File.Exists(fullPath);
    }

    public void DeleteIfExists(
        string relativePath)
    {
        var fullPath =
            ResolveDocumentPath(
                relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public string ResolveDocumentPath(
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException(
                "A document path is required.",
                nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException(
                "Absolute document paths are not allowed.");
        }

        var normalizedRelativePath =
            relativePath.Replace(
                '/',
                Path.DirectorySeparatorChar);

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    _rootPath,
                    normalizedRelativePath));

        EnsureInsideStorageRoot(fullPath);

        return fullPath;
    }

    private string ResolveCategoryFolder(
        string category)
    {
        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    _rootPath,
                    category));

        EnsureInsideStorageRoot(fullPath);

        return fullPath;
    }

    private void EnsureInsideStorageRoot(
        string candidatePath)
    {
        var rootWithSeparator =
            _rootPath.EndsWith(
                Path.DirectorySeparatorChar.ToString(),
                StringComparison.Ordinal)
                ? _rootPath
                : _rootPath +
                  Path.DirectorySeparatorChar;

        var isRoot =
            candidatePath.Equals(
                _rootPath,
                StringComparison.OrdinalIgnoreCase);

        var isInsideRoot =
            candidatePath.StartsWith(
                rootWithSeparator,
                StringComparison.OrdinalIgnoreCase);

        if (!isRoot && !isInsideRoot)
        {
            throw new InvalidOperationException(
                "The requested document path is outside " +
                "the configured private-storage root.");
        }
    }

    private static string NormalizeSinglePathSegment(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A document category is required.",
                parameterName);
        }

        var trimmed =
            value.Trim();

        if (trimmed is "." or ".." ||
            trimmed.Contains('/') ||
            trimmed.Contains('\\'))
        {
            throw new ArgumentException(
                "The document category must be " +
                "a single safe folder name.",
                parameterName);
        }

        foreach (
            var invalidCharacter
            in Path.GetInvalidFileNameChars())
        {
            if (trimmed.Contains(invalidCharacter))
            {
                throw new ArgumentException(
                    "The document category contains " +
                    "an invalid character.",
                    parameterName);
            }
        }

        return trimmed;
    }

    private static string NormalizeFileName(
        string originalFileName)
    {
        var fileName =
            Path.GetFileName(
                originalFileName?.Trim());

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "document";
        }

        foreach (
            var invalidCharacter
            in Path.GetInvalidFileNameChars())
        {
            fileName =
                fileName.Replace(
                    invalidCharacter,
                    '_');
        }

        return fileName;
    }
}

public sealed record StoredPrivateDocument(
    string OriginalFileName,
    string StoredFileName,
    string RelativePath,
    long Size,
    string Sha256);
