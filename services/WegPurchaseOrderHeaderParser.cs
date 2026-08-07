using System.Text.RegularExpressions;

namespace POTracker.Services
{
    /// <summary>
    /// Extracts and normalizes the purchaser company printed in the
    /// WEG Purchase Order header. The purchaser company is deliberately
    /// kept separate from the buyer/contact person and the vendor.
    /// </summary>
    public static class WegPurchaseOrderHeaderParser
    {
        public const string CanonicalWegPurchaserName =
            "WEG Industries (India) PVT LTD";

        private static readonly Regex WegCompanyPattern =
            new(
                @"\bWEG\s+Industries\s*\(\s*India\s*\)\s*" +
                @"(?:PVT\.?\s+LTD\.?|PRIVATE\s+LIMITED)\b",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant |
                RegexOptions.Compiled);

        private static readonly Regex CompanySuffixPattern =
            new(
                @"\b(?:PVT\.?\s+LTD\.?|PRIVATE\s+LIMITED|" +
                @"LIMITED|LTD\.?|LLP|LLC|INC\.?)\b",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant |
                RegexOptions.Compiled);

        private static readonly Regex EmailPattern =
            new(
                @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant |
                RegexOptions.Compiled);

        public static string ExtractPurchaserName(
            string? extractedPdfText)
        {
            if (string.IsNullOrWhiteSpace(
                    extractedPdfText))
            {
                return string.Empty;
            }

            var normalizedText =
                NormalizeLineEndings(
                    extractedPdfText);

            var wegMatch =
                WegCompanyPattern.Match(
                    normalizedText);

            if (wegMatch.Success)
            {
                return CanonicalWegPurchaserName;
            }

            var lines =
                normalizedText
                    .Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(CleanLine)
                    .Where(line =>
                        !string.IsNullOrWhiteSpace(line))
                    .ToList();

            // The purchaser company is normally printed shortly after
            // the buyer's email/phone and before the purchaser address.
            for (var index = 0;
                 index < lines.Count;
                 index++)
            {
                if (!EmailPattern.IsMatch(
                        lines[index]))
                {
                    continue;
                }

                var maximumCandidateIndex =
                    Math.Min(
                        lines.Count - 1,
                        index + 5);

                for (var candidateIndex = index + 1;
                     candidateIndex <= maximumCandidateIndex;
                     candidateIndex++)
                {
                    var candidate =
                        lines[candidateIndex];

                    if (IsPurchaserCompanyCandidate(
                            candidate))
                    {
                        return NormalizePurchaserName(
                            candidate);
                    }
                }
            }

            // Fallback for PO layouts where the email and company are
            // arranged in separate PDF columns and line order changes.
            var headerLimit =
                lines.FindIndex(line =>
                    line.StartsWith(
                        "Item Material",
                        StringComparison.OrdinalIgnoreCase));

            if (headerLimit < 0)
            {
                headerLimit =
                    Math.Min(
                        lines.Count,
                        80);
            }

            for (var index = 0;
                 index < headerLimit;
                 index++)
            {
                var candidate =
                    lines[index];

                if (IsPurchaserCompanyCandidate(
                        candidate))
                {
                    return NormalizePurchaserName(
                        candidate);
                }
            }

            return string.Empty;
        }

        public static string NormalizePurchaserName(
            string? purchaserName)
        {
            if (string.IsNullOrWhiteSpace(
                    purchaserName))
            {
                return string.Empty;
            }

            var cleanedName =
                CleanLine(
                    purchaserName);

            if (WegCompanyPattern.IsMatch(
                    cleanedName))
            {
                return CanonicalWegPurchaserName;
            }

            return cleanedName.Trim(
                ' ',
                ',',
                ';',
                ':',
                '.');
        }

        public static string CreateComparisonKey(
            string? purchaserName)
        {
            var normalizedName =
                NormalizePurchaserName(
                    purchaserName);

            if (string.IsNullOrWhiteSpace(
                    normalizedName))
            {
                return string.Empty;
            }

            return new string(
                normalizedName
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray());
        }

        private static bool IsPurchaserCompanyCandidate(
            string candidate)
        {
            if (string.IsNullOrWhiteSpace(
                    candidate) ||
                candidate.Length < 5 ||
                candidate.Length > 160)
            {
                return false;
            }

            if (
                candidate.Contains(
                    "INDHRA TURNING POINT",
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(
                    "Vendor:",
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(
                    "GST Number",
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(
                    "PAN Number",
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(
                    "Payment conditions",
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(
                    "Order N",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return CompanySuffixPattern.IsMatch(
                candidate);
        }

        private static string NormalizeLineEndings(
            string value)
        {
            return value
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Replace(
                    '\r',
                    '\n');
        }

        private static string CleanLine(
            string value)
        {
            return Regex.Replace(
                    value,
                    @"\s+",
                    " ")
                .Trim();
        }
    }
}