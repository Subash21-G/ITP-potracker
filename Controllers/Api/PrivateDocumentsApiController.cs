using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using POTracker.Services;

namespace POTracker.Controllers.Api;

[ApiController]
[Route("api/private-documents")]
[ResponseCache(
    Duration = 0,
    Location = ResponseCacheLocation.None,
    NoStore = true)]
public sealed class PrivateDocumentsApiController :
    ControllerBase
{
    private static readonly FileExtensionContentTypeProvider
        ContentTypeProvider = new();

    private readonly PrivateDocumentStorageService
        _storage;

    public PrivateDocumentsApiController(
        PrivateDocumentStorageService storage)
    {
        _storage =
            storage ??
            throw new ArgumentNullException(
                nameof(storage));
    }

    [HttpGet("{category}/{storedFileName}")]
    [ProducesResponseType(
        typeof(FileStreamResult),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public IActionResult Download(
        string category,
        string storedFileName)
    {
        category =
            category?.Trim() ??
            string.Empty;

        storedFileName =
            storedFileName?.Trim() ??
            string.Empty;

        if (
            !IsSafeSinglePathSegment(category) ||
            !IsSafeSinglePathSegment(storedFileName)
        )
        {
            return BadRequest(
                new
                {
                    error =
                        "The requested document path is invalid."
                });
        }

        var relativePath =
            $"{category}/{storedFileName}";

        try
        {
            if (!_storage.Exists(relativePath))
            {
                return NotFound(
                    new
                    {
                        error =
                            "The requested document was not found."
                    });
            }

            if (
                !ContentTypeProvider.TryGetContentType(
                    storedFileName,
                    out var contentType)
            )
            {
                contentType =
                    "application/octet-stream";
            }

            Response.Headers.CacheControl =
                "no-store, no-cache, must-revalidate";

            Response.Headers.Pragma =
                "no-cache";

            Response.Headers.Expires =
                "0";

            Response.Headers.XContentTypeOptions =
                "nosniff";

            var stream =
                _storage.OpenRead(relativePath);

            return File(
                stream,
                contentType,
                Path.GetFileName(storedFileName),
                enableRangeProcessing: true);
        }
        catch (
            ArgumentException)
        {
            return BadRequest(
                new
                {
                    error =
                        "The requested document path is invalid."
                });
        }
        catch (
            InvalidOperationException)
        {
            return BadRequest(
                new
                {
                    error =
                        "The requested document path is invalid."
                });
        }
    }

    private static bool IsSafeSinglePathSegment(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (
            value is "." or ".." ||
            value.Contains('/') ||
            value.Contains('\\')
        )
        {
            return false;
        }

        foreach (
            var invalidCharacter
            in Path.GetInvalidFileNameChars())
        {
            if (value.Contains(invalidCharacter))
            {
                return false;
            }
        }

        return true;
    }
}