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

            if (!string.IsNullOrWhiteSpace(
                    material))
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

            if (!string.IsNullOrWhiteSpace(
                    description))
            {
                var value =
                    description.Trim();

                filteredRecords =
                    filteredRecords.Where(record =>
                        Contains(
                            record.Description,
                            value));
            }

            if (!string.IsNullOrWhiteSpace(
                    supplier))
            {
                var value =
                    supplier.Trim();

                filteredRecords =
                    filteredRecords.Where(record =>
                        Contains(
                            record.SupplierName,
                            value));
            }

            if (!string.IsNullOrWhiteSpace(
                    location))
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
                    updatedTo.Value.Date.AddDays(1);

                filteredRecords =
                    filteredRecords.Where(record =>
                        record.LastUpdated.HasValue &&
                        record.LastUpdated.Value <
                        endExclusive);
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
