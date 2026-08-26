using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/local-stocks")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class LocalStocksApiController :
        ControllerBase
    {
        private const int DefaultPageSize = 25;

        private static readonly int[] SupportedPageSizes =
        {
            10,
            25,
            50,
            100
        };

        private static readonly HashSet<string>
            LocalStockExcelFilterColumns =
                new(
                    new[]
                    {
                        "material",
                        "grade",
                        "location",
                        "available",
                        "minimum",
                        "status",
                        "updated"
                    },
                    StringComparer.OrdinalIgnoreCase);

        private readonly ApplicationDbContext _context;

        public LocalStocksApiController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // LIST
        // ============================================================

        [HttpGet]
        [ProducesResponseType(
            typeof(LocalStockListResponseDto),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<LocalStockListResponseDto>>
            Index(
                string? search,
                string? material,
                string? rmGrade,
                string? location,
                decimal? minAvailableQty,
                decimal? maxAvailableQty,
                decimal? minMinimumQty,
                decimal? maxMinimumQty,
                string? status,
                DateTime? updatedFrom,
                DateTime? updatedTo,
                string? sortColumn,
                string? sortDirection,
                string? excelFilters,
                int page = 1,
                int pageSize = DefaultPageSize,
                CancellationToken cancellationToken = default)
        {
            page =
                Math.Max(
                    1,
                    page);

            pageSize =
                SupportedPageSizes.Contains(pageSize)
                    ? pageSize
                    : DefaultPageSize;

            var filteredQuery =
                ApplyListFilters(
                    _context.Stocks
                        .AsNoTracking(),
                    search,
                    material,
                    rmGrade,
                    location,
                    minAvailableQty,
                    maxAvailableQty,
                    minMinimumQty,
                    maxMinimumQty,
                    status,
                    updatedFrom,
                    updatedTo);

            var allStocks =
                await filteredQuery
                    .ToListAsync(
                        cancellationToken);

            var excelSelections =
                ParseLocalStockExcelFilters(
                    excelFilters);

            var filteredStocks =
                allStocks
                    .Where(stock =>
                        MatchesLocalStockExcelFilters(
                            stock,
                            excelSelections))
                    .ToList();

            var totalRecords =
                filteredStocks.Count;

            var totalPages =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        totalRecords /
                        (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var today =
                DateTime.Today;

            var tomorrow =
                today.AddDays(1);

            var summary =
                new LocalStockSummaryDto
                {
                    TotalItems =
                        totalRecords,

                    LowStockItems =
                        filteredStocks.Count(stock =>
                            stock.AvailableQty <=
                            stock.MinimumQty),

                    HealthyItems =
                        filteredStocks.Count(stock =>
                            stock.AvailableQty >
                            stock.MinimumQty),

                    UpdatedToday =
                        filteredStocks.Count(stock =>
                            stock.UpdatedDate >= today &&
                            stock.UpdatedDate < tomorrow)
                };

            var items =
                ApplyLocalStockSort(
                        filteredStocks,
                        sortColumn,
                        sortDirection)
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(pageSize)
                    .Select(
                        Map)
                    .ToList();

            return Ok(
                new LocalStockListResponseDto
                {
                    Items =
                        items,
                    Summary =
                        summary,
                    Page =
                        page,
                    PageSize =
                        pageSize,
                    TotalRecords =
                        totalRecords,
                    TotalPages =
                        totalPages
                });
        }

        // ============================================================
        // EXCEL FILTER VALUES
        // ============================================================

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
                string? rmGrade,
                string? location,
                decimal? minAvailableQty,
                decimal? maxAvailableQty,
                decimal? minMinimumQty,
                decimal? maxMinimumQty,
                string? status,
                DateTime? updatedFrom,
                DateTime? updatedTo,
                string? excelFilters,
                CancellationToken cancellationToken = default)
        {
            var normalizedColumn =
                NormalizeExcelColumn(
                    column);

            if (
                !LocalStockExcelFilterColumns
                    .Contains(
                        normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Unsupported Local Stock Excel filter column."
                    });
            }

            var stocks =
                await ApplyListFilters(
                        _context.Stocks
                            .AsNoTracking(),
                        search,
                        material,
                        rmGrade,
                        location,
                        minAvailableQty,
                        maxAvailableQty,
                        minMinimumQty,
                        maxMinimumQty,
                        status,
                        updatedFrom,
                        updatedTo)
                    .ToListAsync(
                        cancellationToken);

            var selections =
                ParseLocalStockExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var values =
                stocks
                    .Where(stock =>
                        MatchesLocalStockExcelFilters(
                            stock,
                            selections))
                    .Select(stock =>
                        GetLocalStockExcelCellValue(
                            stock,
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

        // ============================================================
        // CREATE
        // ============================================================

        [HttpPost]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(LocalStockSaveResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            StatusCodes.Status400BadRequest)]
        public async Task<
            ActionResult<LocalStockSaveResponseDto>>
            Create(
                [FromBody]
                LocalStockUpsertRequestDto? request,
                CancellationToken cancellationToken = default)
        {
            if (
                request == null ||
                string.IsNullOrWhiteSpace(
                    request.MaterialNo)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Material number is required."
                    });
            }

            var stock =
                new Stock
                {
                    MaterialNo =
                        request.MaterialNo.Trim(),

                    MaterialName =
                        NormalizeOptional(
                            request.MaterialName),

                    RMGrade =
                        NormalizeOptional(
                            request.RMGrade),

                    Location =
                        NormalizeOptional(
                            request.Location),

                    AvailableQty =
                        request.AvailableQty,

                    MinimumQty =
                        request.MinimumQty,

                    UpdatedDate =
                        DateTime.Now
                };

            _context.Stocks.Add(
                stock);

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                new LocalStockSaveResponseDto
                {
                    Success = true,
                    Message =
                        "Local stock item created successfully.",
                    Stock =
                        Map(stock)
                });
        }

        // ============================================================
        // UPDATE
        // ============================================================

        [HttpPut("{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(LocalStockSaveResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<LocalStockSaveResponseDto>>
            Update(
                int id,
                [FromBody]
                LocalStockUpsertRequestDto? request,
                CancellationToken cancellationToken = default)
        {
            if (
                request == null ||
                string.IsNullOrWhiteSpace(
                    request.MaterialNo)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Material number is required."
                    });
            }

            var stock =
                await _context.Stocks
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id == id,
                        cancellationToken);

            if (stock == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Local stock item {id} was not found."
                    });
            }

            stock.MaterialNo =
                request.MaterialNo.Trim();

            stock.MaterialName =
                NormalizeOptional(
                    request.MaterialName);

            stock.RMGrade =
                NormalizeOptional(
                    request.RMGrade);

            stock.Location =
                NormalizeOptional(
                    request.Location);

            stock.AvailableQty =
                request.AvailableQty;

            stock.MinimumQty =
                request.MinimumQty;

            stock.UpdatedDate =
                DateTime.Now;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                new LocalStockSaveResponseDto
                {
                    Success = true,
                    Message =
                        "Local stock item updated successfully.",
                    Stock =
                        Map(stock)
                });
        }

        // ============================================================
        // DELETE
        // ============================================================

        [HttpDelete("{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(LocalStockDeleteResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<LocalStockDeleteResponseDto>>
            Delete(
                int id,
                CancellationToken cancellationToken = default)
        {
            var stock =
                await _context.Stocks
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id == id,
                        cancellationToken);

            if (stock == null)
            {
                return NotFound(
                    new
                    {
                        error =
                            $"Local stock item {id} was not found."
                    });
            }

            _context.Stocks.Remove(
                stock);

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                new LocalStockDeleteResponseDto
                {
                    Success = true,
                    Message =
                        "Local stock item deleted successfully.",
                    StockId =
                        id
                });
        }

        private static IQueryable<Stock>
            ApplyListFilters(
                IQueryable<Stock> query,
                string? search,
                string? material,
                string? rmGrade,
                string? location,
                decimal? minAvailableQty,
                decimal? maxAvailableQty,
                decimal? minMinimumQty,
                decimal? maxMinimumQty,
                string? status,
                DateTime? updatedFrom,
                DateTime? updatedTo)
        {
            var filteredQuery =
                ApplySearch(
                    query,
                    search);

            var normalizedMaterial =
                NormalizeOptional(
                    material);

            if (normalizedMaterial != null)
            {
                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.MaterialNo.Contains(
                            normalizedMaterial) ||
                        (
                            stock.MaterialName != null &&
                            stock.MaterialName.Contains(
                                normalizedMaterial)
                        ));
            }

            var normalizedGrade =
                NormalizeOptional(
                    rmGrade);

            if (normalizedGrade != null)
            {
                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.RMGrade != null &&
                        stock.RMGrade.Contains(
                            normalizedGrade));
            }

            var normalizedLocation =
                NormalizeOptional(
                    location);

            if (normalizedLocation != null)
            {
                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.Location != null &&
                        stock.Location.Contains(
                            normalizedLocation));
            }

            if (minAvailableQty.HasValue)
            {
                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.AvailableQty >=
                        minAvailableQty.Value);
            }

            if (maxAvailableQty.HasValue)
            {
                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.AvailableQty <=
                        maxAvailableQty.Value);
            }

            if (minMinimumQty.HasValue)
            {
                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.MinimumQty >=
                        minMinimumQty.Value);
            }

            if (maxMinimumQty.HasValue)
            {
                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.MinimumQty <=
                        maxMinimumQty.Value);
            }

            var normalizedStatus =
                NormalizeOptional(
                    status)?
                    .ToLowerInvariant();

            if (normalizedStatus == "low")
            {
                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.AvailableQty <=
                        stock.MinimumQty);
            }
            else if (
                normalizedStatus ==
                "healthy"
            )
            {
                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.AvailableQty >
                        stock.MinimumQty);
            }

            if (updatedFrom.HasValue)
            {
                var updatedStart =
                    updatedFrom.Value.Date;

                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.UpdatedDate >=
                        updatedStart);
            }

            if (updatedTo.HasValue)
            {
                var updatedEndExclusive =
                    updatedTo.Value.Date.AddDays(1);

                filteredQuery =
                    filteredQuery.Where(stock =>
                        stock.UpdatedDate <
                        updatedEndExclusive);
            }

            return filteredQuery;
        }

        private static Dictionary<
            string,
            string[]>
            ParseLocalStockExcelFilters(
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
                        LocalStockExcelFilterColumns
                            .Contains(
                                NormalizeExcelColumn(
                                    pair.Key)))
                    .ToDictionary(
                        pair =>
                            NormalizeExcelColumn(
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
            MatchesLocalStockExcelFilters(
                Stock stock,
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
                    GetLocalStockExcelCellValue(
                        stock,
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
            GetLocalStockExcelCellValue(
                Stock stock,
                string column)
        {
            var normalized =
                NormalizeExcelColumn(
                    column);

            return normalized switch
            {
                "material" =>
                    string.Join(
                        " | ",
                        new[]
                        {
                            stock.MaterialNo,
                            stock.MaterialName
                        }
                        .Where(value =>
                            !string.IsNullOrWhiteSpace(
                                value))),

                "grade" =>
                    stock.RMGrade?.Trim() ??
                    string.Empty,

                "location" =>
                    stock.Location?.Trim() ??
                    string.Empty,

                "available" =>
                    stock.AvailableQty.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture),

                "minimum" =>
                    stock.MinimumQty.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture),

                "status" =>
                    stock.AvailableQty <=
                    stock.MinimumQty
                        ? "Low Stock"
                        : "Healthy",

                "updated" =>
                    stock.UpdatedDate.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture),

                _ =>
                    string.Empty
            };
        }

        private static IEnumerable<Stock>
            ApplyLocalStockSort(
                IEnumerable<Stock> stocks,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                NormalizeExcelColumn(
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
                return stocks
                    .OrderBy(stock =>
                        stock.AvailableQty <=
                        stock.MinimumQty
                            ? 0
                            : 1)
                    .ThenBy(stock =>
                        stock.MaterialNo)
                    .ThenBy(stock =>
                        stock.Id);
            }

            Func<Stock, object?> selector =
                column switch
                {
                    "material" =>
                        stock =>
                            stock.MaterialNo,

                    "grade" =>
                        stock =>
                            stock.RMGrade,

                    "location" =>
                        stock =>
                            stock.Location,

                    "available" =>
                        stock =>
                            stock.AvailableQty,

                    "minimum" =>
                        stock =>
                            stock.MinimumQty,

                    "status" =>
                        stock =>
                            stock.AvailableQty <=
                            stock.MinimumQty
                                ? "Low Stock"
                                : "Healthy",

                    "updated" =>
                        stock =>
                            stock.UpdatedDate,

                    _ =>
                        stock =>
                            stock.MaterialNo
                };

            return descending
                ? stocks
                    .OrderByDescending(
                        selector)
                    .ThenByDescending(stock =>
                        stock.Id)
                : stocks
                    .OrderBy(
                        selector)
                    .ThenBy(stock =>
                        stock.Id);
        }

        private static string
            NormalizeExcelColumn(
                string? column)
        {
            return column?
                .Trim()
                .ToLowerInvariant() ??
                string.Empty;
        }

        private static IQueryable<Stock>
            ApplySearch(
                IQueryable<Stock> query,
                string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return query;
            }

            var value =
                search.Trim();

            return query.Where(stock =>
                stock.MaterialNo.Contains(value) ||
                (
                    stock.MaterialName != null &&
                    stock.MaterialName.Contains(value)
                ) ||
                (
                    stock.RMGrade != null &&
                    stock.RMGrade.Contains(value)
                ) ||
                (
                    stock.Location != null &&
                    stock.Location.Contains(value)
                ));
        }

        private static string?
            NormalizeOptional(
                string? value)
        {
            var normalized =
                value?.Trim();

            return string.IsNullOrWhiteSpace(
                    normalized)
                ? null
                : normalized;
        }

        private static LocalStockListItemDto
            Map(
                Stock stock)
        {
            var isLowStock =
                stock.AvailableQty <=
                stock.MinimumQty;

            return new LocalStockListItemDto
            {
                Id =
                    stock.Id,
                MaterialNo =
                    stock.MaterialNo,
                MaterialName =
                    stock.MaterialName ??
                    string.Empty,
                RMGrade =
                    stock.RMGrade ??
                    string.Empty,
                Location =
                    stock.Location ??
                    string.Empty,
                AvailableQty =
                    stock.AvailableQty,
                MinimumQty =
                    stock.MinimumQty,
                IsLowStock =
                    isLowStock,
                Status =
                    isLowStock
                        ? "Low Stock"
                        : "Healthy",
                UpdatedDate =
                    stock.UpdatedDate
            };
        }
    }
}