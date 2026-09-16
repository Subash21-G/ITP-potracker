using System.Text.RegularExpressions;

namespace POTracker.Services
{
    /// <summary>
    /// Extracts optional, item-level manufacturing details from WEG PO
    /// descriptions. Every method returns null when the PDF does not state a
    /// usable value so callers can keep the corresponding database field blank.
    /// </summary>
    public static partial class WegMaterialDetailExtractor
    {
        private const int MaximumValueLength = 200;

        public static string? ExtractDrawingVersion(
            string? drawing)
        {
            var normalized = Normalize(drawing);

            if (normalized == null)
            {
                return null;
            }

            var explicitVersion =
                ExplicitDrawingVersionRegex()
                    .Match(normalized);

            if (explicitVersion.Success)
            {
                return CleanValue(
                    explicitVersion.Groups["value"].Value);
            }

            // WEG drawing references commonly end in a separate revision
            // token, for example: "SWD 10007585436 000 01".
            var trailingVersion =
                TrailingDrawingVersionRegex()
                    .Match(normalized);

            return trailingVersion.Success
                ? CleanValue(
                    trailingVersion.Groups["value"].Value)
                : null;
        }

        public static string? ExtractSize(
            string? shortDescription,
            string? fullDescription)
        {
            foreach (var description in new[]
            {
                shortDescription,
                fullDescription
            })
            {
                var normalized = Normalize(description);

                if (normalized == null)
                {
                    continue;
                }

                var explicitSize =
                    ExplicitSizeRegex()
                        .Match(normalized);

                if (explicitSize.Success)
                {
                    var value = ExtractDimension(
                        explicitSize.Groups["value"].Value) ??
                        CleanValue(
                            explicitSize.Groups["value"].Value);

                    if (value != null)
                    {
                        return value;
                    }
                }

                var dimension =
                    ExtractDimension(normalized);

                if (dimension != null)
                {
                    return dimension;
                }
            }

            return null;
        }

        public static string? ExtractProcess(
            string? shortDescription,
            string? fullDescription)
        {
            return ExtractLabeledValue(
                ProcessRegex(),
                shortDescription,
                fullDescription);
        }

        public static string? ExtractSource(
            string? shortDescription,
            string? fullDescription)
        {
            return ExtractLabeledValue(
                SourceRegex(),
                shortDescription,
                fullDescription);
        }

        private static string? ExtractLabeledValue(
            Regex pattern,
            string? shortDescription,
            string? fullDescription)
        {
            foreach (var description in new[]
            {
                fullDescription,
                shortDescription
            })
            {
                var normalized = Normalize(description);

                if (normalized == null)
                {
                    continue;
                }

                var match = pattern.Match(normalized);

                if (!match.Success)
                {
                    continue;
                }

                var value = CleanValue(
                    match.Groups["value"].Value);

                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        private static string? ExtractDimension(
            string value)
        {
            var match = DimensionRegex().Match(value);

            if (!match.Success)
            {
                return null;
            }

            var dimension =
                WhitespaceRegex()
                    .Replace(
                        match.Groups["value"].Value,
                        " ")
                    .Trim()
                    .ToUpperInvariant();

            dimension =
                DimensionSeparatorRegex()
                    .Replace(
                        dimension,
                        "X");

            return CleanValue(dimension);
        }

        private static string? Normalize(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return WhitespaceRegex()
                .Replace(
                    value,
                    " ")
                .Trim();
        }

        private static string? CleanValue(
            string value)
        {
            var normalized =
                WhitespaceRegex()
                    .Replace(
                        value,
                        " ")
                    .Trim(
                        ' ',
                        ':',
                        '=',
                        '-',
                        ',',
                        '.',
                        ';',
                        '|')
                    .ToUpperInvariant();

            if (
                string.IsNullOrWhiteSpace(normalized) ||
                normalized.Length > MaximumValueLength ||
                MissingValueRegex().IsMatch(normalized)
            )
            {
                return null;
            }

            return normalized;
        }

        [GeneratedRegex(
            @"\s+",
            RegexOptions.CultureInvariant)]
        private static partial Regex WhitespaceRegex();

        [GeneratedRegex(
            @"\s*[Xx\u00D7]\s*",
            RegexOptions.CultureInvariant)]
        private static partial Regex DimensionSeparatorRegex();

        [GeneratedRegex(
            @"\b(?:REV(?:ISION)?|VER(?:SION)?)\s*[:=\-]?\s*(?<value>[A-Z0-9._\-/]{1,20})\b",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex ExplicitDrawingVersionRegex();

        [GeneratedRegex(
            @"\s+(?<value>(?:\d{1,3}|[A-Z]\d{0,2}))\s*$",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex TrailingDrawingVersionRegex();

        [GeneratedRegex(
            @"\b(?:(?:RM|RAW\s+MATERIAL|MATERIAL)\s+)?(?:SIZE|DIMENSIONS?)\s*(?::|=|\s-\s)\s*(?<value>[^;|]+)",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex ExplicitSizeRegex();

        [GeneratedRegex(
            @"(?<![A-Z0-9])(?<value>(?:[\u00D8\u2300]\s*)?(?:M\s*)?\d+(?:[.,]\d+)?(?:\s*(?:MM|CM|IN(?:CH(?:ES)?)?|M))?(?:\s*[Xx\u00D7]\s*(?:[\u00D8\u2300]\s*)?(?:M\s*)?\d+(?:[.,]\d+)?(?:\s*(?:MM|CM|IN(?:CH(?:ES)?)?|M))?){1,3})(?![A-Z0-9])",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex DimensionRegex();

        [GeneratedRegex(
            @"\b(?:(?:RM|RAW\s+MATERIAL|MATERIAL)\s+)?PROCESS\s*(?::|=|\s-\s)\s*(?<value>[^;|]+)",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex ProcessRegex();

        [GeneratedRegex(
            @"\b(?:(?:RM|RAW\s+MATERIAL|MATERIAL)\s+)?SOURCE\s*(?::|=|\s-\s)\s*(?<value>[^;|]+)",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex SourceRegex();

        [GeneratedRegex(
            @"^(?:N/?A|NA|NIL|NONE|NOT\s+(?:AVAILABLE|APPLICABLE|SPECIFIED)|WITHOUT)$",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex MissingValueRegex();
    }
}
