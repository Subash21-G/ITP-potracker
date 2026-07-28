using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace POTracker.Services
{
    public sealed class ExternalStockRecordDto
    {
        public int Id { get; set; }

        public string? MaterialCode { get; set; }

        public string? MaterialNo { get; set; }

        public string? Description { get; set; }

        public string? SupplierName { get; set; }

        public string? Location { get; set; }

        public decimal CurrentStock { get; set; }

        public decimal MinimumStock { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalValue { get; set; }

        public bool IsLowStock { get; set; }

        public string? Status { get; set; }

        public DateTime? LastUpdated { get; set; }
    }

    public sealed class ExternalStockLoadResult
    {
        public IReadOnlyList<ExternalStockRecordDto>
            Items
        { get; init; } =
            Array.Empty<ExternalStockRecordDto>();

        public bool IsOnline { get; init; }

        public string? ErrorMessage { get; init; }
    }

    public sealed class ExternalStockSnapshot
    {
        public decimal LiveStock { get; init; }

        public decimal MinimumStock { get; init; }

        public decimal Shortage { get; init; }

        public string Status { get; init; } =
            "Not Found";

        public int RecordCount { get; init; }

        public string Description { get; init; } =
            string.Empty;

        public string Suppliers { get; init; } =
            string.Empty;

        public string Locations { get; init; } =
            string.Empty;

        public DateTime? LastUpdated { get; init; }
    }

    public sealed class ExternalStockService
    {
        private const string CacheKey =
            "ExternalStockMaintenance.All";

        private static readonly TimeSpan CacheDuration =
            TimeSpan.FromSeconds(30);

        private readonly IHttpClientFactory
            _httpClientFactory;

        private readonly IMemoryCache
            _memoryCache;

        private readonly ILogger<ExternalStockService>
            _logger;

        public ExternalStockService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache,
            ILogger<ExternalStockService> logger)
        {
            _httpClientFactory =
                httpClientFactory;

            _memoryCache =
                memoryCache;

            _logger =
                logger;
        }

        public async Task<ExternalStockLoadResult>
            GetAllAsync(
                bool forceRefresh = false,
                CancellationToken cancellationToken =
                    default)
        {
            if (!forceRefresh &&
                TryGetCached(
                    out var cachedRecords))
            {
                return new ExternalStockLoadResult
                {
                    Items =
                        cachedRecords,

                    IsOnline =
                        true
                };
            }

            try
            {
                var client =
                    _httpClientFactory
                        .CreateClient("StockApi");

                if (client.BaseAddress == null)
                {
                    throw new InvalidOperationException(
                        "StockApi:BaseUrl is not configured.");
                }

                var records =
                    await client.GetFromJsonAsync<
                        List<ExternalStockRecordDto>>(
                            "api/stock/all",
                            cancellationToken) ??
                    new List<ExternalStockRecordDto>();

                var cleanedRecords =
                    records
                        .Where(record =>
                            !string.IsNullOrWhiteSpace(
                                record.MaterialNo) ||
                            !string.IsNullOrWhiteSpace(
                                record.MaterialCode))
                        .ToList();

                _memoryCache.Set(
                    CacheKey,
                    cleanedRecords,
                    CacheDuration);

                return new ExternalStockLoadResult
                {
                    Items =
                        cleanedRecords,

                    IsOnline =
                        true
                };
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Stock Maintenance API could not be loaded.");

                if (TryGetCached(
                        out var staleRecords))
                {
                    return new ExternalStockLoadResult
                    {
                        Items =
                            staleRecords,

                        IsOnline =
                            false,

                        ErrorMessage =
                            "Showing recently cached Stock Maintenance data."
                    };
                }

                return new ExternalStockLoadResult
                {
                    Items =
                        Array.Empty<
                            ExternalStockRecordDto>(),

                    IsOnline =
                        false,

                    ErrorMessage =
                        "Stock Maintenance is currently unavailable."
                };
            }
        }

        public static ExternalStockSnapshot
            BuildSnapshot(
                IEnumerable<ExternalStockRecordDto>
                    records,
                string? materialNo,
                decimal requiredQuantity)
        {
            var normalizedMaterialNo =
                Normalize(materialNo);

            if (string.IsNullOrEmpty(
                    normalizedMaterialNo))
            {
                return new ExternalStockSnapshot
                {
                    Shortage =
                        Math.Max(
                            0,
                            requiredQuantity)
                };
            }

            var matches =
                records
                    .Where(record =>
                        Normalize(
                            record.MaterialNo) ==
                            normalizedMaterialNo ||
                        Normalize(
                            record.MaterialCode) ==
                            normalizedMaterialNo)
                    .ToList();

            if (matches.Count == 0)
            {
                return new ExternalStockSnapshot
                {
                    Shortage =
                        Math.Max(
                            0,
                            requiredQuantity)
                };
            }

            var liveStock =
                matches.Sum(record =>
                    Math.Max(
                        0,
                        record.CurrentStock));

            var minimumStock =
                matches.Sum(record =>
                    Math.Max(
                        0,
                        record.MinimumStock));

            var shortage =
                Math.Max(
                    0,
                    requiredQuantity -
                    liveStock);

            string status;

            if (liveStock <= 0)
            {
                status =
                    "Out of Stock";
            }
            else if (shortage > 0)
            {
                status =
                    "Shortage";
            }
            else
            {
                status =
                    "Available";
            }

            return new ExternalStockSnapshot
            {
                LiveStock =
                    liveStock,

                MinimumStock =
                    minimumStock,

                Shortage =
                    shortage,

                Status =
                    status,

                RecordCount =
                    matches.Count,

                Description =
                    matches
                        .Select(record =>
                            record.Description?.Trim())
                        .FirstOrDefault(value =>
                            !string.IsNullOrWhiteSpace(
                                value)) ??
                    string.Empty,

                Suppliers =
                    JoinDistinct(
                        matches.Select(record =>
                            record.SupplierName)),

                Locations =
                    JoinDistinct(
                        matches.Select(record =>
                            record.Location)),

                LastUpdated =
                    matches.Max(record =>
                        record.LastUpdated)
            };
        }

        public static string Normalize(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return string.Empty;
            }

            return new string(
                value
                    .Where(
                        char.IsLetterOrDigit)
                    .ToArray())
                .ToUpperInvariant();
        }

        private bool TryGetCached(
            out IReadOnlyList<
                ExternalStockRecordDto> records)
        {
            if (_memoryCache.TryGetValue(
                    CacheKey,
                    out List<
                        ExternalStockRecordDto>?
                        cachedRecords) &&
                cachedRecords != null)
            {
                records =
                    cachedRecords;

                return true;
            }

            records =
                Array.Empty<
                    ExternalStockRecordDto>();

            return false;
        }

        private static string JoinDistinct(
            IEnumerable<string?> values)
        {
            return string.Join(
                ", ",
                values
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                    .Select(value =>
                        value!.Trim())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase));
        }
    }
}
