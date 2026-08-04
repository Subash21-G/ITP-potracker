namespace POTracker.Services;

/// <summary>
/// Converts legacy public upload paths, private relative paths and
/// secure download URLs into one safe private-document reference.
/// </summary>
public sealed class PrivateDocumentReferenceService
{
    private const string DownloadRoutePrefix =
        "/api/private-documents/";

    public string? NormalizeRelativePath(
        string? storedValue,
        string defaultCategory)
    {
        var safeDefaultCategory =
            NormalizeSinglePathSegment(
                defaultCategory);

        if (safeDefaultCategory == null)
        {
            throw new ArgumentException(
                "The default document category is invalid.",
                nameof(defaultCategory));
        }

        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return null;
        }

        try
        {
            var value =
                storedValue
                    .Trim()
                    .Trim('"');

            if (
                Uri.TryCreate(
                    value,
                    UriKind.Absolute,
                    out var absoluteUri) &&
                (
                    absoluteUri.Scheme.Equals(
                        Uri.UriSchemeHttp,
                        StringComparison.OrdinalIgnoreCase) ||
                    absoluteUri.Scheme.Equals(
                        Uri.UriSchemeHttps,
                        StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                value =
                    absoluteUri.AbsolutePath;
            }

            value =
                Uri.UnescapeDataString(value);

            value =
                value.Replace('\\', '/');

            var queryOrFragmentIndex =
                value.IndexOfAny(
                    new[] { '?', '#' });

            if (queryOrFragmentIndex >= 0)
            {
                value =
                    value[..queryOrFragmentIndex];
            }

            value =
                StripKnownPrefix(value);

            value =
                value.Trim('/');

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!value.Contains('/'))
            {
                value =
                    $"{safeDefaultCategory}/{value}";
            }

            var segments =
                value.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);

            if (segments.Length != 2)
            {
                return null;
            }

            var category =
                NormalizeSinglePathSegment(
                    segments[0]);

            var fileName =
                NormalizeSinglePathSegment(
                    segments[1]);

            if (
                category == null ||
                fileName == null
            )
            {
                return null;
            }

            return $"{category}/{fileName}";
        }
        catch (
            UriFormatException)
        {
            return null;
        }
        catch (
            ArgumentException)
        {
            return null;
        }
    }

    public string? ToDownloadUrl(
        string? storedValue,
        string defaultCategory)
    {
        var relativePath =
            NormalizeRelativePath(
                storedValue,
                defaultCategory);

        if (relativePath == null)
        {
            return null;
        }

        var segments =
            relativePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 2)
        {
            return null;
        }

        return
            DownloadRoutePrefix +
            Uri.EscapeDataString(segments[0]) +
            "/" +
            Uri.EscapeDataString(segments[1]);
    }

    public bool TryNormalizeRelativePath(
        string? storedValue,
        string defaultCategory,
        out string relativePath)
    {
        var normalized =
            NormalizeRelativePath(
                storedValue,
                defaultCategory);

        if (normalized == null)
        {
            relativePath =
                string.Empty;

            return false;
        }

        relativePath =
            normalized;

        return true;
    }

    private static string StripKnownPrefix(
        string value)
    {
        var embeddedPrefixes =
            new[]
            {
                "/api/private-documents/",
                "/App_Data/private-uploads/",
                "/app_data/private-uploads/",
                "/private-uploads/",
                "/uploads/"
            };

        foreach (
            var prefix
            in embeddedPrefixes)
        {
            var index =
                value.IndexOf(
                    prefix,
                    StringComparison.OrdinalIgnoreCase);

            if (index >= 0)
            {
                return value[
                    (index + prefix.Length)..];
            }
        }

        var startingPrefixes =
            new[]
            {
                "api/private-documents/",
                "App_Data/private-uploads/",
                "private-uploads/",
                "wwwroot/uploads/",
                "uploads/"
            };

        foreach (
            var prefix
            in startingPrefixes)
        {
            if (
                value.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                return value[prefix.Length..];
            }
        }

        return value;
    }

    private static string? NormalizeSinglePathSegment(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed =
            value.Trim();

        if (
            trimmed is "." or ".." ||
            trimmed.Contains('/') ||
            trimmed.Contains('\\')
        )
        {
            return null;
        }

        foreach (
            var invalidCharacter
            in Path.GetInvalidFileNameChars())
        {
            if (trimmed.Contains(invalidCharacter))
            {
                return null;
            }
        }

        return trimmed;
    }
}