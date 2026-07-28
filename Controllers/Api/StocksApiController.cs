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

            IEnumerable<ExternalStockRecordDto>
                filteredRecords =
                    stockLoad.Items;

            if (!string.IsNullOrWhiteSpace(
                    search))
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

            var records =
                filteredRecords
                    .OrderBy(record =>
                        record.IsLowStock
                            ? 0
                            : 1)
                    .ThenBy(record =>
                        record.MaterialNo)
                    .ThenByDescending(record =>
                        record.LastUpdated)
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
                    .Select(record =>
                        new StockListItemDto
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
                                record.Status ??
                                (
                                    record.IsLowStock
                                        ? "Low Stock"
                                        : "Available"
                                ),

                            LastUpdated =
                                record.LastUpdated
                        })
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
