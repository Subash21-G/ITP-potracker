using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    public sealed partial class InvoicesApiController
    {
        private const string
            DefaultPendingInvoiceReason =
                "No matching material was found.";

        private static readonly HashSet<string>
            PendingInvoiceExcelColumns =
                new(
                    new[]
                    {
                        "invoice",
                        "invoicedate",
                        "po",
                        "lineitem",
                        "material",
                        "quantity",
                        "priceunit",
                        "gst",
                        "amount",
                        "reason",
                        "imported"
                    },
                    StringComparer.OrdinalIgnoreCase);

        [HttpGet("pending/excel")]
        public async Task<
            ActionResult<
                List<
                    PendingInvoiceItemApiDto>>>
            PendingExcel(
                string? search,
                int take = 500,
                string? sortColumn = null,
                string? sortDirection = null,
                string? excelFilters = null,
                CancellationToken cancellationToken =
                    default)
        {
            take =
                Math.Clamp(
                    take,
                    10,
                    1000);

            var query =
                ApplyPendingInvoiceSearch(
                    _context
                        .PendingInvoiceItems
                        .AsNoTracking()
                        .Where(
                            item =>
                                item.Status ==
                                "Pending"),
                    search);

            query =
                ApplyPendingInvoiceExcelFilters(
                    query,
                    excelFilters);

            query =
                ApplyPendingInvoiceSort(
                    query,
                    sortColumn,
                    sortDirection);

            var entities =
                await query
                    .Take(
                        take)
                    .ToListAsync(
                        cancellationToken);

            return Ok(
                entities
                    .Select(
                        MapPendingInvoiceItem)
                    .ToList());
        }

        [HttpGet(
            "pending/excel-filter-values")]
        [ProducesResponseType(
            typeof(
                IReadOnlyList<
                    ExcelFilterValueDto>),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<
                IReadOnlyList<
                    ExcelFilterValueDto>>>
            PendingExcelFilterValues(
                string column,
                string? search,
                string? excelFilters,
                CancellationToken cancellationToken =
                    default)
        {
            var normalizedColumn =
                NormalizePendingInvoiceColumn(
                    column);

            if (
                !PendingInvoiceExcelColumns
                    .Contains(
                        normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        message =
                            "Unknown pending invoice Excel-filter column."
                    });
            }

            var selections =
                ParsePendingInvoiceExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var remainingFilters =
                selections.Count == 0
                    ? null
                    : JsonSerializer.Serialize(
                        selections);

            var query =
                ApplyPendingInvoiceSearch(
                    _context
                        .PendingInvoiceItems
                        .AsNoTracking()
                        .Where(
                            item =>
                                item.Status ==
                                "Pending"),
                    search);

            query =
                ApplyPendingInvoiceExcelFilters(
                    query,
                    remainingFilters);

            var entities =
                await query
                    .ToListAsync(
                        cancellationToken);

            var values =
                entities
                    .Select(
                        item =>
                            new
                            {
                                Value =
                                    GetPendingInvoiceExcelValue(
                                        item,
                                        normalizedColumn),

                                Label =
                                    GetPendingInvoiceExcelLabel(
                                        item,
                                        normalizedColumn)
                            })
                    .GroupBy(
                        item =>
                            item.Value,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(
                        group =>
                            new ExcelFilterValueDto
                            {
                                Value =
                                    group.Key,

                                Label =
                                    group
                                        .Select(
                                            item =>
                                                item.Label)
                                        .FirstOrDefault()
                                    ?? string.Empty,

                                Count =
                                    group.Count()
                            })
                    .OrderBy(
                        item =>
                            item.Label,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            return Ok(
                values);
        }

        private static IQueryable<
                PendingInvoiceItem>
            ApplyPendingInvoiceSearch(
                IQueryable<
                    PendingInvoiceItem> query,
                string? search)
        {
            if (
                string.IsNullOrWhiteSpace(
                    search)
            )
            {
                return query;
            }

            var searchValue =
                search.Trim();

            return query.Where(
                item =>
                    item.InvoiceNumber
                        .Contains(
                            searchValue) ||
                    (
                        item.PONumber != null &&
                        item.PONumber
                            .Contains(
                                searchValue)
                    ) ||
                    (
                        item.MaterialNo != null &&
                        item.MaterialNo
                            .Contains(
                                searchValue)
                    ) ||
                    (
                        item.LineItem != null &&
                        item.LineItem
                            .Contains(
                                searchValue)
                    ) ||
                    (
                        item.Description != null &&
                        item.Description
                            .Contains(
                                searchValue)
                    ));
        }

        private static IQueryable<
                PendingInvoiceItem>
            ApplyPendingInvoiceExcelFilters(
                IQueryable<
                    PendingInvoiceItem> query,
                string? excelFilters)
        {
            var selections =
                ParsePendingInvoiceExcelFilters(
                    excelFilters);

            foreach (
                var pair in selections)
            {
                var selected =
                    pair.Value ??
                    Array.Empty<string>();

                if (
                    selected.Length ==
                    0
                )
                {
                    return query.Where(
                        _ => false);
                }

                switch (pair.Key)
                {
                    case "invoice":
                        query =
                            query.Where(
                                item =>
                                    selected.Contains(
                                        item.InvoiceNumber ??
                                        string.Empty));
                        break;

                    case "invoicedate":
                    {
                        var dates =
                            ParsePendingDates(
                                selected);

                        if (
                            dates.Length !=
                            selected.Length
                        )
                        {
                            return query.Where(
                                _ => false);
                        }

                        query =
                            query.Where(
                                item =>
                                    item.InvoiceDate.HasValue &&
                                    dates.Contains(
                                        item.InvoiceDate
                                            .Value.Date));
                        break;
                    }

                    case "po":
                        query =
                            query.Where(
                                item =>
                                    selected.Contains(
                                        item.PONumber ??
                                        string.Empty));
                        break;

                    case "lineitem":
                        query =
                            query.Where(
                                item =>
                                    selected.Contains(
                                        item.LineItem ??
                                        string.Empty));
                        break;

                    case "material":
                        query =
                            query.Where(
                                item =>
                                    selected.Contains(
                                        item.MaterialNo ??
                                        string.Empty));
                        break;

                    case "quantity":
                    {
                        var values =
                            ParsePendingDecimals(
                                selected);

                        if (
                            values.Length !=
                            selected.Length
                        )
                        {
                            return query.Where(
                                _ => false);
                        }

                        query =
                            query.Where(
                                item =>
                                    values.Contains(
                                        item.InvoiceQty));
                        break;
                    }

                    case "priceunit":
                    {
                        var values =
                            ParsePendingDecimals(
                                selected);

                        if (
                            values.Length !=
                            selected.Length
                        )
                        {
                            return query.Where(
                                _ => false);
                        }

                        query =
                            query.Where(
                                item =>
                                    values.Contains(
                                        item.PriceUnit));
                        break;
                    }

                    case "gst":
                    {
                        var values =
                            ParsePendingDecimals(
                                selected);

                        if (
                            values.Length !=
                            selected.Length
                        )
                        {
                            return query.Where(
                                _ => false);
                        }

                        query =
                            query.Where(
                                item =>
                                    values.Contains(
                                        item.GstAmount));
                        break;
                    }

                    case "amount":
                    {
                        var values =
                            ParsePendingDecimals(
                                selected);

                        if (
                            values.Length !=
                            selected.Length
                        )
                        {
                            return query.Where(
                                _ => false);
                        }

                        query =
                            query.Where(
                                item =>
                                    values.Contains(
                                        item.Amount));
                        break;
                    }

                    case "reason":
                        query =
                            query.Where(
                                item =>
                                    selected.Contains(
                                        item.ErrorMessage ??
                                        DefaultPendingInvoiceReason));
                        break;

                    case "imported":
                    {
                        var dates =
                            ParsePendingDates(
                                selected);

                        if (
                            dates.Length !=
                            selected.Length
                        )
                        {
                            return query.Where(
                                _ => false);
                        }

                        query =
                            query.Where(
                                item =>
                                    dates.Contains(
                                        item.CreatedDate.Date));
                        break;
                    }
                }
            }

            return query;
        }

        private static IQueryable<
                PendingInvoiceItem>
            ApplyPendingInvoiceSort(
                IQueryable<
                    PendingInvoiceItem> query,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                NormalizePendingInvoiceColumn(
                    sortColumn);

            var descending =
                string.Equals(
                    sortDirection?.Trim(),
                    "desc",
                    StringComparison.OrdinalIgnoreCase);

            if (
                !PendingInvoiceExcelColumns
                    .Contains(
                        column)
            )
            {
                return query
                    .OrderByDescending(
                        item =>
                            item.CreatedDate)
                    .ThenByDescending(
                        item =>
                            item.Id);
            }

            return column switch
            {
                "invoice" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.InvoiceNumber)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "invoice" =>
                    query
                        .OrderBy(
                            item =>
                                item.InvoiceNumber)
                        .ThenBy(
                            item =>
                                item.Id),

                "invoicedate" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.InvoiceDate)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "invoicedate" =>
                    query
                        .OrderBy(
                            item =>
                                item.InvoiceDate)
                        .ThenBy(
                            item =>
                                item.Id),

                "po" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.PONumber)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "po" =>
                    query
                        .OrderBy(
                            item =>
                                item.PONumber)
                        .ThenBy(
                            item =>
                                item.Id),

                "lineitem" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.LineItem)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "lineitem" =>
                    query
                        .OrderBy(
                            item =>
                                item.LineItem)
                        .ThenBy(
                            item =>
                                item.Id),

                "material" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.MaterialNo)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "material" =>
                    query
                        .OrderBy(
                            item =>
                                item.MaterialNo)
                        .ThenBy(
                            item =>
                                item.Id),

                "quantity" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.InvoiceQty)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "quantity" =>
                    query
                        .OrderBy(
                            item =>
                                item.InvoiceQty)
                        .ThenBy(
                            item =>
                                item.Id),

                "priceunit" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.PriceUnit)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "priceunit" =>
                    query
                        .OrderBy(
                            item =>
                                item.PriceUnit)
                        .ThenBy(
                            item =>
                                item.Id),

                "gst" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.GstAmount)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "gst" =>
                    query
                        .OrderBy(
                            item =>
                                item.GstAmount)
                        .ThenBy(
                            item =>
                                item.Id),

                "amount" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.Amount)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "amount" =>
                    query
                        .OrderBy(
                            item =>
                                item.Amount)
                        .ThenBy(
                            item =>
                                item.Id),

                "reason" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.ErrorMessage)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "reason" =>
                    query
                        .OrderBy(
                            item =>
                                item.ErrorMessage)
                        .ThenBy(
                            item =>
                                item.Id),

                "imported" when descending =>
                    query
                        .OrderByDescending(
                            item =>
                                item.CreatedDate)
                        .ThenByDescending(
                            item =>
                                item.Id),

                "imported" =>
                    query
                        .OrderBy(
                            item =>
                                item.CreatedDate)
                        .ThenBy(
                            item =>
                                item.Id),

                _ =>
                    query
                        .OrderByDescending(
                            item =>
                                item.CreatedDate)
                        .ThenByDescending(
                            item =>
                                item.Id)
            };
        }

        private static Dictionary<
                string,
                string[]>
            ParsePendingInvoiceExcelFilters(
                string? raw)
        {
            var result =
                new Dictionary<
                    string,
                    string[]>(
                    StringComparer.OrdinalIgnoreCase);

            if (
                string.IsNullOrWhiteSpace(
                    raw)
            )
            {
                return result;
            }

            try
            {
                var parsed =
                    JsonSerializer.Deserialize<
                        Dictionary<
                            string,
                            string[]>>(
                        raw);

                if (
                    parsed ==
                    null
                )
                {
                    return result;
                }

                foreach (
                    var pair in parsed)
                {
                    var column =
                        NormalizePendingInvoiceColumn(
                            pair.Key);

                    if (
                        !PendingInvoiceExcelColumns
                            .Contains(
                                column)
                    )
                    {
                        continue;
                    }

                    result[column] =
                        pair.Value ??
                        Array.Empty<string>();
                }
            }
            catch (JsonException)
            {
                // Invalid JSON behaves as no Excel filter.
            }

            return result;
        }

        private static DateTime[]
            ParsePendingDates(
                IEnumerable<string> values)
        {
            return values
                .Select(
                    value =>
                        DateTime.TryParseExact(
                            value,
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var date)
                            ? date.Date
                            : (DateTime?)null)
                .Where(
                    value =>
                        value.HasValue)
                .Select(
                    value =>
                        value!.Value)
                .ToArray();
        }

        private static decimal[]
            ParsePendingDecimals(
                IEnumerable<string> values)
        {
            return values
                .Select(
                    value =>
                        decimal.TryParse(
                            value,
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out var number)
                            ? number
                            : (decimal?)null)
                .Where(
                    value =>
                        value.HasValue)
                .Select(
                    value =>
                        value!.Value)
                .ToArray();
        }

        private static string
            GetPendingInvoiceExcelValue(
                PendingInvoiceItem item,
                string column)
        {
            return column switch
            {
                "invoice" =>
                    item.InvoiceNumber ??
                    string.Empty,

                "invoicedate" =>
                    item.InvoiceDate
                        ?.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)
                    ?? string.Empty,

                "po" =>
                    item.PONumber ??
                    string.Empty,

                "lineitem" =>
                    item.LineItem ??
                    string.Empty,

                "material" =>
                    item.MaterialNo ??
                    string.Empty,

                "quantity" =>
                    item.InvoiceQty
                        .ToString(
                            "0.##",
                            CultureInfo.InvariantCulture),

                "priceunit" =>
                    item.PriceUnit
                        .ToString(
                            "0.##",
                            CultureInfo.InvariantCulture),

                "gst" =>
                    item.GstAmount
                        .ToString(
                            "0.##",
                            CultureInfo.InvariantCulture),

                "amount" =>
                    item.Amount
                        .ToString(
                            "0.##",
                            CultureInfo.InvariantCulture),

                "reason" =>
                    string.IsNullOrWhiteSpace(
                        item.ErrorMessage)
                        ? DefaultPendingInvoiceReason
                        : item.ErrorMessage,

                "imported" =>
                    item.CreatedDate
                        .ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture),

                _ =>
                    string.Empty
            };
        }

        private static string
            GetPendingInvoiceExcelLabel(
                PendingInvoiceItem item,
                string column)
        {
            var value =
                GetPendingInvoiceExcelValue(
                    item,
                    column);

            if (
                string.IsNullOrEmpty(
                    value)
            )
            {
                return "(Blank)";
            }

            if (
                column is
                    "invoicedate" or
                    "imported" &&
                DateTime.TryParseExact(
                    value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date)
            )
            {
                return date.ToString(
                    "dd MMM yyyy",
                    CultureInfo.InvariantCulture);
            }

            return value;
        }

        private static string
            NormalizePendingInvoiceColumn(
                string? column)
        {
            return column?
                .Trim()
                .ToLowerInvariant() ??
                string.Empty;
        }
    }
}
