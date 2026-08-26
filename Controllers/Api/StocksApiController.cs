using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using POTracker.Contracts.Api;
using POTracker.Services;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/stocks")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class StocksApiController :
        ControllerBase
    {
        private static readonly HashSet<string>
            ExcelFilterColumns =
                new(
                    new[]
                    {
                        "material",
                        "description",
                        "supplier",
                        "location",
                        "current",
                        "minimum",
                        "unitprice",
                        "totalvalue",
                        "status",
                        "updated"
                    },
                    StringComparer.OrdinalIgnoreCase);

        private readonly ExternalStockService
            _externalStockService;

        public StocksApiController(
            ExternalStockService
                externalStockService)
        {
            _externalStockService =
                externalStockService;
        }

        [HttpGet]
        public async Task<
            ActionResult<StockListResponseDto>>
            Index(
                string? search,
                string? material,
                string? description,
                string? supplier,
                string? location,
                decimal? minCurrentStock,
                decimal? maxCurrentStock,
                decimal? minMinimumStock,
                decimal? maxMinimumStock,
                decimal? minUnitPrice,
                decimal? maxUnitPrice,
                decimal? minTotalValue,
                decimal? maxTotalValue,
                string? status,
                DateTime? updatedFrom,
                DateTime? updatedTo,
                string? sortColumn,
                string? sortDirection,
                string? excelFilters,
                int page = 1,
                int pageSize = 25,
                bool refresh = false,
                CancellationToken cancellationToken =
                    default)
        {
            page =
                Math.Max(
                    1,
                    page);

            pageSize =
                Math.Clamp(
                    pageSize,
                    10,
                    100);

            var stockLoad =
                await _externalStockService
                    .GetAllAsync(
                        refresh,
                        cancellationToken);

            var filteredRecords =
                ApplyLegacyFilters(
                    stockLoad.Items,
                    search,
                    material,
                    description,
                    supplier,
                    location,
                    minCurrentStock,
                    maxCurrentStock,
                    minMinimumStock,
                    maxMinimumStock,
                    minUnitPrice,
                    maxUnitPrice,
                    minTotalValue,
                    maxTotalValue,
                    status,
                    updatedFrom,
                    updatedTo);

            var excelSelections =
                ParseExcelFilters(
                    excelFilters);

            var records =
                ApplyStockSort(
                        filteredRecords.Where(
                            record =>
                                MatchesExcelFilters(
                                    record,
                                    excelSelections)),
                        sortColumn,
                        sortDirection)
                    .ToList();

            var totalRecords =
                records.Count;

            var totalPages =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        totalRecords /
                        (double)pageSize));

            if (page > totalPages)
            {
                page =
                    totalPages;
            }

            var today =
                DateTime.Today;

            var tomorrow =
                today.AddDays(1);

            var items =
                records
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(pageSize)
                    .Select(
                        MapListItem)
                    .ToList();

            return Ok(
                new StockListResponseDto
                {
                    Page =
                        page,

                    PageSize =
                        pageSize,

                    TotalRecords =
                        totalRecords,

                    TotalPages =
                        totalPages,

                    SourceOnline =
                        stockLoad.IsOnline,

                    Message =
                        stockLoad.ErrorMessage ??
                        "Live Stock Maintenance data loaded successfully.",

                    RefreshedAt =
                        DateTime.Now,

                    Summary =
                        new StockSummaryDto
                        {
                            TotalRecords =
                                totalRecords,

                            UniqueMaterials =
                                records
                                    .Select(record =>
                                        ExternalStockService
                                            .Normalize(
                                                record.MaterialNo ??
                                                record.MaterialCode))
                                    .Where(value =>
                                        !string.IsNullOrEmpty(
                                            value))
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .Count(),

                            LowStockRecords =
                                records.Count(record =>
                                    record.IsLowStock),

                            AvailableRecords =
                                records.Count(record =>
                                    !record.IsLowStock),

                            UpdatedToday =
                                records.Count(record =>
                                    record.LastUpdated >=
                                        today &&
                                    record.LastUpdated <
                                        tomorrow),

                            TotalCurrentStock =
                                records.Sum(record =>
                                    record.CurrentStock),

                            TotalInventoryValue =
                                records.Sum(record =>
                                    record.TotalValue)
                        },

                    Items =
                        items
                });
        }

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
                string? search,
                string? material,
                string? description,
                string? supplier,
                string? location,
                decimal? minCurrentStock,
                decimal? maxCurrentStock,
                decimal? minMinimumStock,
                decimal? maxMinimumStock,
                decimal? minUnitPrice,
                decimal? maxUnitPrice,
                decimal? minTotalValue,
                decimal? maxTotalValue,
                string? status,
                DateTime? updatedFrom,
                DateTime? updatedTo,
                string? excelFilters,
                CancellationToken cancellationToken =
                    default)
        {
            var normalizedColumn =
                NormalizeColumn(
                    column);

            if (
                !ExcelFilterColumns.Contains(
                    normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Unsupported Stock Excel filter column."
                    });
            }

            var stockLoad =
                await _externalStockService
                    .GetAllAsync(
                        false,
                        cancellationToken);

            var filteredRecords =
                ApplyLegacyFilters(
                    stockLoad.Items,
                    search,
                    material,
                    description,
                    supplier,
                    location,
                    minCurrentStock,
                    maxCurrentStock,
                    minMinimumStock,
                    maxMinimumStock,
                    minUnitPrice,
                    maxUnitPrice,
                    minTotalValue,
                    maxTotalValue,
                    status,
                    updatedFrom,
                    updatedTo);

            var selections =
                ParseExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var values =
                filteredRecords
                    .Where(record =>
                        MatchesExcelFilters(
                            record,
                            selections))
                    .Select(record =>
                        GetExcelCellValue(
                            record,
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

        private static IEnumerable<
            ExternalStockRecordDto>
            ApplyLegacyFilters(
                IEnumerable<
                    ExternalStockRecordDto> source,
                string? search,
                string? material,
                string? description,
                string? supplier,
                string? location,
                decimal? minCurrentStock,
                decimal? maxCurrentStock,
                decimal? minMinimumStock,
                decimal? maxMinimumStock,
                decimal? minUnitPrice,
                decimal? maxUnitPrice,
                decimal? minTotalValue,
                decimal? maxTotalValue,
                string? status,
                DateTime? updatedFrom,
                DateTime? updatedTo)
        {
            var filteredRecords =
                source;

            if (
                !string.IsNullOrWhiteSpace(
                    search)
            )
            {
                var searchValue =
                    search.Trim();

                filteredRecords =
                    filteredRecords.Where(record =>
                        Contains(
                            record.MaterialCode,
                            searchValue) ||
                        Contains(
                            record.MaterialNo,
                            searchValue) ||
                        Contains(
                            record.Description,
                            searchValue) ||
                        Contains(
                            record.SupplierName,
                            searchValue) ||
                        Contains(
                            record.Location,
                            searchValue));
            }

            if (
                !string.IsNullOrWhiteSpace(
                    material)
            )
            {
                var value =
                    material.Trim();

                filteredRecords =
                    filteredRecords.Where(record =>
                        Contains(
                            record.MaterialNo,
                            value) ||
                        Contains(
                            record.MaterialCode,
                            value));
            }

            if (
                !string.IsNullOrWhiteSpace(
                    description)
            )
            {
                var value =
                    description.Trim();

                filteredRecords =
                    filteredRecords.Where(record =>
                        Contains(
                            record.Description,
                            value));
            }

            if (
                !string.IsNullOrWhiteSpace(
                    supplier)
            )
            {
                var value =
                    supplier.Trim();

                filteredRecords =
                    filteredRecords.Where(record =>
                        Contains(
                            record.SupplierName,
                            value));
            }

            if (
                !string.IsNullOrWhiteSpace(
                    location)
            )
            {
                var value =
                    location.Trim();

                filteredRecords =
                    filteredRecords.Where(record =>
                        Contains(
                            record.Location,
                            value));
            }

            if (minCurrentStock.HasValue)
            {
                filteredRecords =
                    filteredRecords.Where(record =>
                        record.CurrentStock >=
                            minCurrentStock.Value);
            }

            if (maxCurrentStock.HasValue)
            {
                filteredRecords =
                    filteredRecords.Where(record =>
                        record.CurrentStock <=
                            maxCurrentStock.Value);
            }

            if (minMinimumStock.HasValue)
            {
                filteredRecords =
                    filteredRecords.Where(record =>
                        record.MinimumStock >=
                            minMinimumStock.Value);
            }

            if (maxMinimumStock.HasValue)
            {
                filteredRecords =
                    filteredRecords.Where(record =>
                        record.MinimumStock <=
                            maxMinimumStock.Value);
            }

            if (minUnitPrice.HasValue)
            {
                filteredRecords =
                    filteredRecords.Where(record =>
                        record.UnitPrice >=
                            minUnitPrice.Value);
            }

            if (maxUnitPrice.HasValue)
            {
                filteredRecords =
                    filteredRecords.Where(record =>
                        record.UnitPrice <=
                            maxUnitPrice.Value);
            }

            if (minTotalValue.HasValue)
            {
                filteredRecords =
                    filteredRecords.Where(record =>
                        record.TotalValue >=
                            minTotalValue.Value);
            }

            if (maxTotalValue.HasValue)
            {
                filteredRecords =
                    filteredRecords.Where(record =>
                        record.TotalValue <=
                            maxTotalValue.Value);
            }

            var normalizedStatus =
                status?
                    .Trim()
                    .ToLowerInvariant();

            if (normalizedStatus == "low")
            {
                filteredRecords =
                    filteredRecords.Where(record =>
                        record.IsLowStock);
            }
            else if (
                normalizedStatus ==
                "available"
            )
            {
                filteredRecords =
                    filteredRecords.Where(record =>
                        !record.IsLowStock);
            }

            if (updatedFrom.HasValue)
            {
                var start =
                    updatedFrom.Value.Date;

                filteredRecords =
                    filteredRecords.Where(record =>
                        record.LastUpdated.HasValue &&
                        record.LastUpdated.Value >=
                            start);
            }

            if (updatedTo.HasValue)
            {
                var endExclusive =
                    updatedTo.Value.Date.AddDays(
                        1);

                filteredRecords =
                    filteredRecords.Where(record =>
                        record.LastUpdated.HasValue &&
                        record.LastUpdated.Value <
                            endExclusive);
            }

            return filteredRecords;
        }

        private static Dictionary<
            string,
            string[]>
            ParseExcelFilters(
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
                    System.Text.Json.JsonSerializer
                        .Deserialize<
                            Dictionary<
                                string,
                                string[]>>(
                            excelFilters)
                    ?? new Dictionary<
                        string,
                        string[]>();

                return parsed
                    .Where(pair =>
                        ExcelFilterColumns.Contains(
                            NormalizeColumn(
                                pair.Key)))
                    .ToDictionary(
                        pair =>
                            NormalizeColumn(
                                pair.Key),
                        pair =>
                            pair.Value ??
                            Array.Empty<string>(),
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (
                System.Text.Json.JsonException)
            {
                return new Dictionary<
                    string,
                    string[]>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool
            MatchesExcelFilters(
                ExternalStockRecordDto record,
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
                    GetExcelCellValue(
                        record,
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
            GetExcelCellValue(
                ExternalStockRecordDto record,
                string column)
        {
            var normalized =
                NormalizeColumn(
                    column);

            return normalized switch
            {
                "material" =>
                    string.Join(
                        " | ",
                        new[]
                        {
                            record.MaterialNo,
                            record.MaterialCode
                        }
                        .Where(value =>
                            !string.IsNullOrWhiteSpace(
                                value))),

                "description" =>
                    record.Description?.Trim() ??
                    string.Empty,

                "supplier" =>
                    record.SupplierName?.Trim() ??
                    string.Empty,

                "location" =>
                    record.Location?.Trim() ??
                    string.Empty,

                "current" =>
                    record.CurrentStock.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture),

                "minimum" =>
                    record.MinimumStock.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture),

                "unitprice" =>
                    record.UnitPrice.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture),

                "totalvalue" =>
                    record.TotalValue.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture),

                "status" =>
                    GetStockStatus(
                        record),

                "updated" =>
                    record.LastUpdated
                        ?.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)
                    ?? string.Empty,

                _ =>
                    string.Empty
            };
        }

        private static IEnumerable<
            ExternalStockRecordDto>
            ApplyStockSort(
                IEnumerable<
                    ExternalStockRecordDto> records,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                NormalizeColumn(
                    sortColumn);

            var descending =
                string.Equals(
                    sortDirection?.Trim(),
                    "desc",
                    StringComparison.OrdinalIgnoreCase);

            if (
                string.IsNullOrEmpty(
                    column)
            )
            {
                return records
                    .OrderBy(record =>
                        record.IsLowStock
                            ? 0
                            : 1)
                    .ThenBy(record =>
                        record.MaterialNo)
                    .ThenByDescending(record =>
                        record.LastUpdated);
            }

            return (
                column,
                descending
            ) switch
            {
                ("material", true) =>
                    records
                        .OrderByDescending(record =>
                            record.MaterialNo ??
                            record.MaterialCode)
                        .ThenByDescending(record =>
                            record.Id),

                ("material", false) =>
                    records
                        .OrderBy(record =>
                            record.MaterialNo ??
                            record.MaterialCode)
                        .ThenBy(record =>
                            record.Id),

                ("description", true) =>
                    records
                        .OrderByDescending(record =>
                            record.Description)
                        .ThenByDescending(record =>
                            record.Id),

                ("description", false) =>
                    records
                        .OrderBy(record =>
                            record.Description)
                        .ThenBy(record =>
                            record.Id),

                ("supplier", true) =>
                    records
                        .OrderByDescending(record =>
                            record.SupplierName)
                        .ThenByDescending(record =>
                            record.Id),

                ("supplier", false) =>
                    records
                        .OrderBy(record =>
                            record.SupplierName)
                        .ThenBy(record =>
                            record.Id),

                ("location", true) =>
                    records
                        .OrderByDescending(record =>
                            record.Location)
                        .ThenByDescending(record =>
                            record.Id),

                ("location", false) =>
                    records
                        .OrderBy(record =>
                            record.Location)
                        .ThenBy(record =>
                            record.Id),

                ("current", true) =>
                    records
                        .OrderByDescending(record =>
                            record.CurrentStock)
                        .ThenByDescending(record =>
                            record.Id),

                ("current", false) =>
                    records
                        .OrderBy(record =>
                            record.CurrentStock)
                        .ThenBy(record =>
                            record.Id),

                ("minimum", true) =>
                    records
                        .OrderByDescending(record =>
                            record.MinimumStock)
                        .ThenByDescending(record =>
                            record.Id),

                ("minimum", false) =>
                    records
                        .OrderBy(record =>
                            record.MinimumStock)
                        .ThenBy(record =>
                            record.Id),

                ("unitprice", true) =>
                    records
                        .OrderByDescending(record =>
                            record.UnitPrice)
                        .ThenByDescending(record =>
                            record.Id),

                ("unitprice", false) =>
                    records
                        .OrderBy(record =>
                            record.UnitPrice)
                        .ThenBy(record =>
                            record.Id),

                ("totalvalue", true) =>
                    records
                        .OrderByDescending(record =>
                            record.TotalValue)
                        .ThenByDescending(record =>
                            record.Id),

                ("totalvalue", false) =>
                    records
                        .OrderBy(record =>
                            record.TotalValue)
                        .ThenBy(record =>
                            record.Id),

                ("status", true) =>
                    records
                        .OrderByDescending(
                            GetStockStatus)
                        .ThenByDescending(record =>
                            record.Id),

                ("status", false) =>
                    records
                        .OrderBy(
                            GetStockStatus)
                        .ThenBy(record =>
                            record.Id),

                ("updated", true) =>
                    records
                        .OrderByDescending(record =>
                            record.LastUpdated)
                        .ThenByDescending(record =>
                            record.Id),

                ("updated", false) =>
                    records
                        .OrderBy(record =>
                            record.LastUpdated)
                        .ThenBy(record =>
                            record.Id),

                _ =>
                    records
                        .OrderBy(record =>
                            record.IsLowStock
                                ? 0
                                : 1)
                        .ThenBy(record =>
                            record.MaterialNo)
                        .ThenByDescending(record =>
                            record.LastUpdated)
            };
        }

        private static string
            GetStockStatus(
                ExternalStockRecordDto record)
        {
            return record.Status?.Trim() ??
                (
                    record.IsLowStock
                        ? "Low Stock"
                        : "Available"
                );
        }

        private static StockListItemDto
            MapListItem(
                ExternalStockRecordDto record)
        {
            return new StockListItemDto
            {
                Id =
                    record.Id,

                MaterialCode =
                    record.MaterialCode ??
                    string.Empty,

                MaterialNo =
                    record.MaterialNo ??
                    string.Empty,

                Description =
                    record.Description ??
                    string.Empty,

                SupplierName =
                    record.SupplierName?.Trim() ??
                    string.Empty,

                Location =
                    record.Location ??
                    string.Empty,

                CurrentStock =
                    record.CurrentStock,

                MinimumStock =
                    record.MinimumStock,

                UnitPrice =
                    record.UnitPrice,

                TotalValue =
                    record.TotalValue,

                IsLowStock =
                    record.IsLowStock,

                Status =
                    GetStockStatus(
                        record),

                LastUpdated =
                    record.LastUpdated
            };
        }

        private static string NormalizeColumn(
            string? column)
        {
            return column?
                .Trim()
                .ToLowerInvariant() ??
                string.Empty;
        }

        private static bool Contains(
            string? source,
            string searchValue)
        {
            return !string.IsNullOrWhiteSpace(
                       source) &&
                   source.Contains(
                       searchValue,
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
