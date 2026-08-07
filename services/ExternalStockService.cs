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
        private const string FreshCacheKey =
            "ExternalStockMaintenance.Fresh";

        private const string StaleCacheKey =
            "ExternalStockMaintenance.Stale";

        private const string FailureCooldownKey =
            "ExternalStockMaintenance.FailureCooldown";

        private static readonly TimeSpan FreshCacheDuration =
            TimeSpan.FromSeconds(30);

        private static readonly TimeSpan StaleCacheDuration =
            TimeSpan.FromMinutes(10);

        private static readonly TimeSpan FailureCooldown =
            TimeSpan.FromSeconds(20);

        private static readonly TimeSpan RequestTimeout =
            TimeSpan.FromMilliseconds(650);

        private static readonly SemaphoreSlim RefreshGate =
            new(1, 1);

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
            if (
                !forceRefresh &&
                TryGetFresh(
                    out var freshRecords))
            {
                return new ExternalStockLoadResult
                {
                    Items =
                        freshRecords,

                    IsOnline =
                        true
                };
            }

            if (
                !forceRefresh &&
                IsFailureCoolingDown())
            {
                return CreateUnavailableResult(
                    "Stock Maintenance is temporarily offline. " +
                    "The main PO Tracker data remains available.");
            }

            var lockTaken =
                await RefreshGate.WaitAsync(
                    0,
                    cancellationToken);

            if (!lockTaken)
            {
                return CreateUnavailableResult(
                    "Stock refresh is already running. " +
                    "Showing PO Tracker data without waiting.");
            }

            try
            {
                if (
                    !forceRefresh &&
                    TryGetFresh(
                        out freshRecords))
                {
                    return new ExternalStockLoadResult
                    {
                        Items =
                            freshRecords,

                        IsOnline =
                            true
                    };
                }

                if (
                    !forceRefresh &&
                    IsFailureCoolingDown())
                {
                    return CreateUnavailableResult(
                        "Stock Maintenance is temporarily offline. " +
                        "The main PO Tracker data remains available.");
                }

                var client =
                    _httpClientFactory
                        .CreateClient("StockApi");

                if (client.BaseAddress == null)
                {
                    throw new InvalidOperationException(
                        "StockApi:BaseUrl is not configured.");
                }

                using var timeoutSource =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(
                            cancellationToken);

                timeoutSource.CancelAfter(
                    RequestTimeout);

                var records =
                    await client.GetFromJsonAsync<
                        List<ExternalStockRecordDto>>(
                            "api/stock/all",
                            timeoutSource.Token) ??
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
                    FreshCacheKey,
                    cleanedRecords,
                    FreshCacheDuration);

                _memoryCache.Set(
                    StaleCacheKey,
                    cleanedRecords,
                    StaleCacheDuration);

                _memoryCache.Remove(
                    FailureCooldownKey);

                return new ExternalStockLoadResult
                {
                    Items =
                        cleanedRecords,

                    IsOnline =
                        true
                };
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _memoryCache.Set(
                    FailureCooldownKey,
                    true,
                    FailureCooldown);

                _logger.LogWarning(
                    exception,
                    "Stock Maintenance API did not respond within {TimeoutMilliseconds} ms. " +
                    "PO Tracker continued without blocking.",
                    RequestTimeout.TotalMilliseconds);

                return CreateUnavailableResult(
                    "Stock Maintenance is currently unavailable. " +
                    "PO Tracker continued without waiting.");
            }
            finally
            {
                RefreshGate.Release();
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

        private ExternalStockLoadResult
            CreateUnavailableResult(
                string message)
        {
            if (TryGetStale(
                    out var staleRecords))
            {
                return new ExternalStockLoadResult
                {
                    Items =
                        staleRecords,

                    IsOnline =
                        false,

                    ErrorMessage =
                        "Showing recently cached Stock Maintenance data. " +
                        message
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
                    message
            };
        }

        private bool IsFailureCoolingDown()
        {
            return _memoryCache.TryGetValue(
                FailureCooldownKey,
                out bool _);
        }

        private bool TryGetFresh(
            out IReadOnlyList<
                ExternalStockRecordDto> records)
        {
            return TryGetRecords(
                FreshCacheKey,
                out records);
        }

        private bool TryGetStale(
            out IReadOnlyList<
                ExternalStockRecordDto> records)
        {
            return TryGetRecords(
                StaleCacheKey,
                out records);
        }

        private bool TryGetRecords(
            string cacheKey,
            out IReadOnlyList<
                ExternalStockRecordDto> records)
        {
            if (_memoryCache.TryGetValue(
                    cacheKey,
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