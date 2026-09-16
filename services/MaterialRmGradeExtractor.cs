using System.Text.RegularExpressions;

namespace POTracker.Services
{
    public static partial class MaterialRmGradeExtractor
    {
        private const int MaximumGradeLength = 200;

        public static string? Extract(
            string? shortDescription,
            string? briefDescription)
        {
            foreach (var description in new[]
            {
                briefDescription,
                shortDescription
            })
            {
                var grade =
                    ExtractFromDescription(
                        description);

                if (grade != null)
                {
                    return grade;
                }
            }

            return null;
        }

        private static string? ExtractFromDescription(
            string? description)
        {
            if (string.IsNullOrWhiteSpace(
                    description))
            {
                return null;
            }

            var normalizedDescription =
                WhitespaceRegex()
                    .Replace(
                        description,
                        " ")
                    .Trim();

            var explicitGrade =
                ExplicitGradeRegex()
                    .Match(
                        normalizedDescription);

            if (explicitGrade.Success)
            {
                var value =
                    CleanCandidate(
                        explicitGrade
                            .Groups["value"]
                            .Value);

                if (value != null)
                {
                    return value;
                }
            }

            var materialCandidates =
                MaterialClauseRegex()
                    .Matches(
                        normalizedDescription)
                    .Select(match => new
                    {
                        Value =
                            CleanCandidate(
                                match
                                    .Groups["value"]
                                    .Value),

                        Score =
                            ScoreMaterialLabel(
                                match
                                    .Groups["label"]
                                    .Value),

                        match.Index
                    })
                    .Where(candidate =>
                        candidate.Value != null &&
                        candidate.Score >= 0)
                    .OrderByDescending(candidate =>
                        candidate.Score)
                    .ThenBy(candidate =>
                        candidate.Index)
                    .FirstOrDefault();

            if (materialCandidates?.Value != null)
            {
                return materialCandidates.Value;
            }

            var fallback =
                GradeFallbackRegex()
                    .Match(
                        normalizedDescription);

            return
                fallback.Success
                    ? CleanCandidate(
                        fallback
                            .Groups["value"]
                            .Value)
                    : null;
        }

        private static int ScoreMaterialLabel(
            string label)
        {
            var normalizedLabel =
                WhitespaceRegex()
                    .Replace(
                        label,
                        " ")
                    .Trim()
                    .ToUpperInvariant();

            if (
                normalizedLabel.Contains(
                    "RISK MATERIAL",
                    StringComparison.Ordinal) ||
                normalizedLabel.Contains(
                    "CERTIFICATE",
                    StringComparison.Ordinal))
            {
                return -1;
            }

            if (
                normalizedLabel == "MATERIAL" ||
                normalizedLabel.EndsWith(
                    " RAW MATERIAL",
                    StringComparison.Ordinal))
            {
                return 1000;
            }

            if (
                normalizedLabel.Contains(
                    "BODY MATERIAL",
                    StringComparison.Ordinal))
            {
                return 900;
            }

            if (
                normalizedLabel.Contains(
                    "BASE MATERIAL",
                    StringComparison.Ordinal) ||
                normalizedLabel.Contains(
                    "COVER MATERIAL",
                    StringComparison.Ordinal) ||
                normalizedLabel.Contains(
                    "FASTENER",
                    StringComparison.Ordinal))
            {
                return 100;
            }

            return 700;
        }

        private static string? CleanCandidate(
            string value)
        {
            var normalizedValue =
                WhitespaceRegex()
                    .Replace(
                        value,
                        " ")
                    .Trim(
                        ' ',
                        ':',
                        '-',
                        ',',
                        '.');

            var documentNoise =
                DocumentNoiseRegex()
                    .Match(
                        normalizedValue);

            if (documentNoise.Success)
            {
                normalizedValue =
                    normalizedValue[..documentNoise.Index]
                        .Trim(
                            ' ',
                            ':',
                            '-',
                            ',',
                            '.');
            }

            if (
                string.IsNullOrWhiteSpace(
                    normalizedValue) ||
                normalizedValue.Equals(
                    "WITHOUT",
                    StringComparison.OrdinalIgnoreCase) ||
                normalizedValue.Equals(
                    "NOT APPLICABLE",
                    StringComparison.OrdinalIgnoreCase) ||
                normalizedValue.Equals(
                    "NOT SPECIFIED",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            normalizedValue =
                GradePrefixSpacingRegex()
                    .Replace(
                        normalizedValue,
                        "$1 ")
                    .ToUpperInvariant();

            if (normalizedValue.Length >
                MaximumGradeLength)
            {
                return null;
            }

            return normalizedValue;
        }

        [GeneratedRegex(
            @"\s+",
            RegexOptions.CultureInvariant)]
        private static partial Regex WhitespaceRegex();

        [GeneratedRegex(
            @"\b(?:RM|RAW\s+MATERIAL)\s+GRADE\s*[:=\-]\s*(?<value>[^;|]+)",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex ExplicitGradeRegex();

        [GeneratedRegex(
            @"(?<label>(?:[A-Z0-9()/_.\-]+\s+){0,5}MATERIAL)\s*:\s*(?<value>[^;]+)",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex MaterialClauseRegex();

        [GeneratedRegex(
            @"(?<value>(?:STAINLESS(?:\s+STEEL)?\s+)?AISI\s*\d{3,4}[A-Z]?|(?:CARBON\s+STEEL\s+)?(?:SAE|ABNT)\s*\d{3,4}(?:/\d{2,4})?|MEDIUM\s+CARBON\s+STEEL\s*>?\s*\d+\s*<?=\s*\d+|CAST\s+IRON\s+FC\s*-?\s*\d+|ALUMINI?UM\s+\d{4}(?:-T\d+)?|POLYPROPYLENE\s*/\s*POLYAMIDE(?:\s+6[.,]6)?|POLYAMIDE\s+6[.,]6|HIGH\s+DENSITY\s+POLYETHYLENE|LOW\s+DENSITY\s+POLYETHYLENE|BLACK\s+MALLEABLE\s+IRON|MALLEABLE\s+CAST\s+IRON|HYDRAULIC\s+CARDBOARD(?:\s+NA\s+\d+)?|CARBON\s+STEEL|POLYPROPYLENE|POLYCARBONATE|POLIACETAL|COPPER|BRASS|BRONZE|PHENO|ZAMAK)",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex GradeFallbackRegex();

        [GeneratedRegex(
            @"\b(AISI|SAE|ABNT|SS)\s*(?=\d)",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex GradePrefixSpacingRegex();

        [GeneratedRegex(
            @"\s+(?:PAGE|ORDER\s+N(?:O|º|°)?|DATE\s+OF\s+(?:ORDER|EMISSION)|QUOTATION|ZIP\s+CODE|SALES\s+REFERENCE|GST\s+NUMBER|PAN\s+NUMBER)\s*:",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)]
        private static partial Regex DocumentNoiseRegex();
    }
}
