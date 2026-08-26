using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using POTracker.Contracts.Api;

namespace POTracker.Controllers.Api
{
    public sealed partial class AlertsApiController
    {
        private static readonly HashSet<string>
            AlertExcelFilterColumns =
                new(
                    new[]
                    {
                        "material",
                        "description",
                        "prioritydate",
                        "deliverydate",
                        "process",
                        "dispatch",
                        "stock"
                    },
                    StringComparer.OrdinalIgnoreCase);

        [HttpGet("excel-filter-values")]
        [ProducesResponseType(
            typeof(
                IReadOnlyList<
                    ExcelFilterValueDto>),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<
                IReadOnlyList<
                    ExcelFilterValueDto>>>
            GetExcelFilterValues(
                string column,
                string? category,
                string? search,
                string? material,
                string? description,
                DateTime? priorityDateFrom,
                DateTime? priorityDateTo,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                string? processStatus,
                string? dispatchStatus,
                decimal? minAllocatedStock,
                decimal? maxAllocatedStock,
                decimal? minLiveStock,
                decimal? maxLiveStock,
                string? stockStatus,
                string? excelFilters,
                CancellationToken cancellationToken =
                    default)
        {
            var normalizedColumn =
                NormalizeAlertExcelColumn(
                    column);

            if (
                !AlertExcelFilterColumns.Contains(
                    normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Unsupported Alerts Excel filter column."
                    });
            }

            var normalizedCategory =
                NormalizeCategory(
                    category);

            var selections =
                ParseAlertExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var remainingFilters =
                selections.Count == 0
                    ? null
                    : JsonSerializer.Serialize(
                        selections);

            async Task<AlertsPagedResponseDto?>
                LoadPageAsync(
                    int pageNumber)
            {
                var result =
                    await GetAlerts(
                        category:
                            normalizedCategory,

                        search:
                            search,

                        material:
                            material,

                        description:
                            description,

                        priorityDateFrom:
                            priorityDateFrom,

                        priorityDateTo:
                            priorityDateTo,

                        deliveryDateFrom:
                            deliveryDateFrom,

                        deliveryDateTo:
                            deliveryDateTo,

                        processStatus:
                            processStatus,

                        dispatchStatus:
                            dispatchStatus,

                        minAllocatedStock:
                            minAllocatedStock,

                        maxAllocatedStock:
                            maxAllocatedStock,

                        minLiveStock:
                            minLiveStock,

                        maxLiveStock:
                            maxLiveStock,

                        stockStatus:
                            stockStatus,

                        excelFilters:
                            remainingFilters,

                        sortColumn:
                            null,

                        sortDirection:
                            null,

                        page:
                            pageNumber,

                        pageSize:
                            100,

                        cancellationToken:
                            cancellationToken);

                return UnwrapAlertPage(
                    result);
            }

            var firstPage =
                await LoadPageAsync(
                    1);

            if (firstPage == null)
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Alerts filter values could not be loaded."
                    });
            }

            var allItems =
                new List<AlertMaterialDto>(
                    firstPage.Items);

            for (
                var pageNumber = 2;
                pageNumber <=
                    firstPage.TotalPages;
                pageNumber++)
            {
                var nextPage =
                    await LoadPageAsync(
                        pageNumber);

                if (nextPage == null)
                {
                    return BadRequest(
                        new
                        {
                            error =
                                "Alerts filter values could not be fully loaded."
                        });
                }

                allItems.AddRange(
                    nextPage.Items);
            }

            var values =
                allItems
                    .Select(item =>
                        GetAlertExcelCellValue(
                            item,
                            normalizedCategory,
                            normalizedColumn))
                    .GroupBy(
                        value =>
                            value,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                        new ExcelFilterValueDto
                        {
                            Value =
                                group.Key,

                            Label =
                                string.IsNullOrEmpty(
                                    group.Key)
                                    ? "(Blank)"
                                    : group.Key,

                            Count =
                                group.Count()
                        })
                    .OrderBy(
                        item =>
                            item.Label,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            return Ok(values);
        }

        private static AlertsPagedResponseDto?
            UnwrapAlertPage(
                ActionResult<
                    AlertsPagedResponseDto> result)
        {
            if (result.Value != null)
            {
                return result.Value;
            }

            return result.Result is
                    OkObjectResult ok &&
                ok.Value is
                    AlertsPagedResponseDto response
                ? response
                : null;
        }

        private static List<AlertMaterialDto>
            ApplyAlertExcelFilters(
                List<AlertMaterialDto> items,
                string category,
                string? excelFilters)
        {
            var selections =
                ParseAlertExcelFilters(
                    excelFilters);

            if (selections.Count == 0)
            {
                return items;
            }

            return items
                .Where(item =>
                    MatchesAlertExcelFilters(
                        item,
                        category,
                        selections))
                .ToList();
        }

        private static Dictionary<
            string,
            string[]>
            ParseAlertExcelFilters(
                string? excelFilters)
        {
            if (
                string.IsNullOrWhiteSpace(
                    excelFilters)
            )
            {
                return new Dictionary<
                    string,
                    string[]>(
                    StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var parsed =
                    JsonSerializer.Deserialize<
                        Dictionary<
                            string,
                            string[]>>(
                        excelFilters)
                    ?? new Dictionary<
                        string,
                        string[]>();

                return parsed
                    .Where(pair =>
                        AlertExcelFilterColumns.Contains(
                            NormalizeAlertExcelColumn(
                                pair.Key)))
                    .ToDictionary(
                        pair =>
                            NormalizeAlertExcelColumn(
                                pair.Key),

                        pair =>
                            pair.Value ??
                            Array.Empty<string>(),

                        StringComparer.OrdinalIgnoreCase);
            }
            catch (
                JsonException)
            {
                return new Dictionary<
                    string,
                    string[]>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool
            MatchesAlertExcelFilters(
                AlertMaterialDto item,
                string category,
                IReadOnlyDictionary<
                    string,
                    string[]> selections)
        {
            foreach (
                var pair in selections)
            {
                var selectedValues =
                    pair.Value ??
                    Array.Empty<string>();

                if (
                    selectedValues.Length ==
                    0
                )
                {
                    return false;
                }

                var cellValue =
                    GetAlertExcelCellValue(
                        item,
                        category,
                        pair.Key);

                if (
                    !selectedValues.Any(
                        selected =>
                            string.Equals(
                                selected,
                                cellValue,
                                StringComparison.OrdinalIgnoreCase))
                )
                {
                    return false;
                }
            }

            return true;
        }

        private static string
            GetAlertExcelCellValue(
                AlertMaterialDto item,
                string category,
                string column)
        {
            return NormalizeAlertExcelColumn(
                    column)
                switch
                {
                    "material" =>
                        string.Join(
                            " | ",
                            new[]
                            {
                                item.MaterialNo,
                                item.LineItem
                            }
                            .Where(value =>
                                !string.IsNullOrWhiteSpace(
                                    value))),

                    "description" =>
                        item.Description ??
                        string.Empty,

                    "prioritydate" =>
                        GetPriorityDate(
                                item,
                                category)
                            ?.ToString(
                                "yyyy-MM-dd",
                                CultureInfo.InvariantCulture)
                        ?? string.Empty,

                    "deliverydate" =>
                        item.DeliveryDate
                            ?.ToString(
                                "yyyy-MM-dd",
                                CultureInfo.InvariantCulture)
                        ?? string.Empty,

                    "process" =>
                        item.ProcessStatus ??
                        string.Empty,

                    "dispatch" =>
                        item.DispatchStatus ??
                        string.Empty,

                    "stock" =>
                        string.Concat(
                            item.AllocatedStockQuantity
                                .ToString(
                                    "0.##",
                                    CultureInfo.InvariantCulture),
                            " / ",
                            item.LiveStockQuantity
                                .ToString(
                                    "0.##",
                                    CultureInfo.InvariantCulture)),

                    _ =>
                        string.Empty
                };
        }

        private static string
            NormalizeAlertExcelColumn(
                string? column)
        {
            return column?
                .Trim()
                .ToLowerInvariant() ??
                string.Empty;
        }
    }
}
