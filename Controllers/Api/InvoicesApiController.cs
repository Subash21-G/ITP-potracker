using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;
using POTracker.Services;
using System.Globalization;
using System.Text.RegularExpressions;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/invoices")]
    public sealed partial class InvoicesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PendingInvoiceMatcher _pendingInvoiceMatcher;

        private readonly PrivateDocumentReferenceService _documentReferences;

        private static readonly HashSet<string>
            InvoiceExcelFilterColumns =
                new(
                    new[]
                    {
                        "invoice",
                        "invoicedate",
                        "customer",
                        "items",
                        "quantity",
                        "gst",
                        "amount",
                        "uploadedfile",
                        "imported"
                    },
                    StringComparer.OrdinalIgnoreCase);

        public InvoicesApiController(
            ApplicationDbContext context,
            PendingInvoiceMatcher pendingInvoiceMatcher,
            PrivateDocumentReferenceService
                documentReferences)
        {
            _context = context;
            _pendingInvoiceMatcher = pendingInvoiceMatcher;
            _documentReferences =
                documentReferences;
        }

        // ============================================================
        // INVOICE LIST
        // ============================================================

        [HttpGet]
        public async Task<ActionResult<InvoiceListResponseDto>> Index(
            string? search,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? invoiceNumber,
            DateTime? columnInvoiceDateFrom,
            DateTime? columnInvoiceDateTo,
            string? customerName,
            int? minItemCount,
            int? maxItemCount,
            decimal? minTotalQuantity,
            decimal? maxTotalQuantity,
            decimal? minTotalGstAmount,
            decimal? maxTotalGstAmount,
            decimal? minTotalAmount,
            decimal? maxTotalAmount,
            string? uploadedFileName,
            DateTime? importedDateFrom,
            DateTime? importedDateTo,
            string? excelFilters,

            string? sortColumn,
            string? sortDirection,

            int page = 1,
            int pageSize = 25)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 200);

            var query =
                ApplyInvoiceFilters(
                    _context.Invoices
                        .AsNoTracking()
                        .AsQueryable(),
                    search,
                    dateFrom,
                    dateTo,
                    invoiceNumber,
                    columnInvoiceDateFrom,
                    columnInvoiceDateTo,
                    customerName,
                    minItemCount,
                    maxItemCount,
                    minTotalQuantity,
                    maxTotalQuantity,
                    minTotalGstAmount,
                    maxTotalGstAmount,
                    minTotalAmount,
                    maxTotalAmount,
                    uploadedFileName,
                    importedDateFrom,
                    importedDateTo);

            var excelFilterSelections =
                ParseInvoiceExcelFilters(
                    excelFilters);

            List<InvoiceListItemDto>?
                excelFilteredRows =
                    null;

            int totalCount;

            if (
                excelFilterSelections.Count ==
                0
            )
            {
                totalCount =
                    await query.CountAsync();
            }
            else
            {
                excelFilteredRows =
                    (
                        await ProjectInvoiceRows(
                                query)
                            .ToListAsync()
                    )
                    .Where(row =>
                        MatchesInvoiceExcelFilters(
                            row,
                            excelFilterSelections))
                    .ToList();

                totalCount =
                    excelFilteredRows.Count;
            }

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    totalCount / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            List<InvoiceListItemDto>
                items;

            if (excelFilteredRows is null)
            {
                items =
                    await ProjectInvoiceRows(
                            ApplyInvoiceSort(
                                query,
                                sortColumn,
                                sortDirection))
                        .Skip(
                            (page - 1) *
                            pageSize)
                        .Take(
                            pageSize)
                        .ToListAsync();
            }
            else
            {
                items =
                    ApplyInvoiceSortInMemory(
                            excelFilteredRows,
                            sortColumn,
                            sortDirection)
                        .Skip(
                            (page - 1) *
                            pageSize)
                        .Take(
                            pageSize)
                        .ToList();
            }

            var summary = new InvoiceListSummaryDto
            {
                TotalInvoices = await _context.Invoices
                    .AsNoTracking()
                    .CountAsync(),

                TotalItems = await _context.InvoiceItems
                    .AsNoTracking()
                    .CountAsync(),

                TotalQuantity = await _context.InvoiceItems
                    .AsNoTracking()
                    .SumAsync(item =>
                        (decimal?)item.InvoiceQty) ?? 0m,

                TotalGstAmount = await _context.InvoiceItems
                    .AsNoTracking()
                    .SumAsync(item =>
                        (decimal?)item.GstAmount) ?? 0m,

                TotalInvoiceAmount = await _context.InvoiceItems
                    .AsNoTracking()
                    .SumAsync(item =>
                        (decimal?)item.Amount) ?? 0m,

                PendingItemCount = await _context.PendingInvoiceItems
                    .AsNoTracking()
                    .CountAsync(item =>
                        item.Status == "Pending")
            };

            return Ok(new InvoiceListResponseDto
            {
                Items = items,
                Summary = summary,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
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
                DateTime? dateFrom,
                DateTime? dateTo,
                string? invoiceNumber,
                DateTime? columnInvoiceDateFrom,
                DateTime? columnInvoiceDateTo,
                string? customerName,
                int? minItemCount,
                int? maxItemCount,
                decimal? minTotalQuantity,
                decimal? maxTotalQuantity,
                decimal? minTotalGstAmount,
                decimal? maxTotalGstAmount,
                decimal? minTotalAmount,
                decimal? maxTotalAmount,
                string? uploadedFileName,
                DateTime? importedDateFrom,
                DateTime? importedDateTo,
                string? excelFilters)
        {
            var normalizedColumn =
                column?
                    .Trim()
                    .ToLowerInvariant() ??
                string.Empty;

            if (
                !InvoiceExcelFilterColumns
                    .Contains(
                        normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Unsupported Invoice Excel filter column."
                    });
            }

            var query =
                ApplyInvoiceFilters(
                    _context.Invoices
                        .AsNoTracking()
                        .AsQueryable(),
                    search,
                    dateFrom,
                    dateTo,
                    invoiceNumber,
                    columnInvoiceDateFrom,
                    columnInvoiceDateTo,
                    customerName,
                    minItemCount,
                    maxItemCount,
                    minTotalQuantity,
                    maxTotalQuantity,
                    minTotalGstAmount,
                    maxTotalGstAmount,
                    minTotalAmount,
                    maxTotalAmount,
                    uploadedFileName,
                    importedDateFrom,
                    importedDateTo);

            var rows =
                await ProjectInvoiceRows(
                        query)
                    .ToListAsync();

            var selections =
                ParseInvoiceExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var values =
                rows
                    .Where(row =>
                        MatchesInvoiceExcelFilters(
                            row,
                            selections))
                    .Select(row =>
                        GetInvoiceExcelCellValue(
                            row,
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

        private IQueryable<Invoice>
            ApplyInvoiceFilters(
                IQueryable<Invoice> query,
                string? search,
                DateTime? dateFrom,
                DateTime? dateTo,
                string? invoiceNumber,
                DateTime? columnInvoiceDateFrom,
                DateTime? columnInvoiceDateTo,
                string? customerName,
                int? minItemCount,
                int? maxItemCount,
                decimal? minTotalQuantity,
                decimal? maxTotalQuantity,
                decimal? minTotalGstAmount,
                decimal? maxTotalGstAmount,
                decimal? minTotalAmount,
                decimal? maxTotalAmount,
                string? uploadedFileName,
                DateTime? importedDateFrom,
                DateTime? importedDateTo)
        {
            if (
                !string.IsNullOrWhiteSpace(
                    search)
            )
            {
                var searchValue =
                    search.Trim();

                query =
                    query.Where(
                        invoice =>
                            invoice.InvoiceNumber.Contains(
                                searchValue) ||
                            invoice.Items.Any(item =>
                                (item.PONumber != null &&
                                 item.PONumber.Contains(searchValue)) ||
                                (item.MaterialNo != null &&
                                 item.MaterialNo.Contains(searchValue)) ||
                                (item.Material != null &&
                                 item.Material.MaterialNo != null &&
                                 item.Material.MaterialNo.Contains(searchValue))) ||
                            _context.PendingInvoiceItems.Any(pending =>
                                pending.InvoiceId == invoice.Id &&
                                ((pending.PONumber != null &&
                                  pending.PONumber.Contains(searchValue)) ||
                                 (pending.MaterialNo != null &&
                                  pending.MaterialNo.Contains(searchValue)))) ||
                            (
                                invoice.CustomerName != null &&
                                invoice.CustomerName.Contains(
                                    searchValue)
                            ) ||
                            (
                                invoice.UploadedFileName != null &&
                                invoice.UploadedFileName.Contains(
                                    searchValue)
                            ));
            }

            if (dateFrom.HasValue)
            {
                var fromDate =
                    dateFrom.Value.Date;

                query =
                    query.Where(
                        invoice =>
                            invoice.InvoiceDate.HasValue &&
                            invoice.InvoiceDate.Value >=
                                fromDate);
            }

            if (dateTo.HasValue)
            {
                var nextDate =
                    dateTo.Value.Date.AddDays(
                        1);

                query =
                    query.Where(
                        invoice =>
                            invoice.InvoiceDate.HasValue &&
                            invoice.InvoiceDate.Value <
                                nextDate);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    invoiceNumber)
            )
            {
                var value =
                    invoiceNumber.Trim();

                query =
                    query.Where(
                        invoice =>
                            invoice.InvoiceNumber.Contains(
                                value));
            }

            if (
                columnInvoiceDateFrom
                    .HasValue
            )
            {
                var fromDate =
                    columnInvoiceDateFrom
                        .Value
                        .Date;

                query =
                    query.Where(
                        invoice =>
                            invoice.InvoiceDate.HasValue &&
                            invoice.InvoiceDate.Value >=
                                fromDate);
            }

            if (
                columnInvoiceDateTo
                    .HasValue
            )
            {
                var nextDate =
                    columnInvoiceDateTo
                        .Value
                        .Date
                        .AddDays(
                            1);

                query =
                    query.Where(
                        invoice =>
                            invoice.InvoiceDate.HasValue &&
                            invoice.InvoiceDate.Value <
                                nextDate);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    customerName)
            )
            {
                var value =
                    customerName.Trim();

                query =
                    query.Where(
                        invoice =>
                            invoice.CustomerName != null &&
                            invoice.CustomerName.Contains(
                                value));
            }

            if (minItemCount.HasValue)
            {
                var value =
                    Math.Max(
                        0,
                        minItemCount.Value);

                query =
                    query.Where(
                        invoice =>
                            invoice.Items.Count >=
                                value);
            }

            if (maxItemCount.HasValue)
            {
                var value =
                    Math.Max(
                        0,
                        maxItemCount.Value);

                query =
                    query.Where(
                        invoice =>
                            invoice.Items.Count <=
                                value);
            }

            if (
                minTotalQuantity.HasValue
            )
            {
                var value =
                    Math.Max(
                        0m,
                        minTotalQuantity.Value);

                query =
                    query.Where(
                        invoice =>
                            (
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.InvoiceQty) ??
                                0m
                            ) >= value);
            }

            if (
                maxTotalQuantity.HasValue
            )
            {
                var value =
                    Math.Max(
                        0m,
                        maxTotalQuantity.Value);

                query =
                    query.Where(
                        invoice =>
                            (
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.InvoiceQty) ??
                                0m
                            ) <= value);
            }

            if (
                minTotalGstAmount.HasValue
            )
            {
                var value =
                    Math.Max(
                        0m,
                        minTotalGstAmount.Value);

                query =
                    query.Where(
                        invoice =>
                            (
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.GstAmount) ??
                                0m
                            ) >= value);
            }

            if (
                maxTotalGstAmount.HasValue
            )
            {
                var value =
                    Math.Max(
                        0m,
                        maxTotalGstAmount.Value);

                query =
                    query.Where(
                        invoice =>
                            (
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.GstAmount) ??
                                0m
                            ) <= value);
            }

            if (minTotalAmount.HasValue)
            {
                var value =
                    Math.Max(
                        0m,
                        minTotalAmount.Value);

                query =
                    query.Where(
                        invoice =>
                            (
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.Amount) ??
                                0m
                            ) >= value);
            }

            if (maxTotalAmount.HasValue)
            {
                var value =
                    Math.Max(
                        0m,
                        maxTotalAmount.Value);

                query =
                    query.Where(
                        invoice =>
                            (
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.Amount) ??
                                0m
                            ) <= value);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    uploadedFileName)
            )
            {
                var value =
                    uploadedFileName.Trim();

                query =
                    query.Where(
                        invoice =>
                            invoice.UploadedFileName != null &&
                            invoice.UploadedFileName.Contains(
                                value));
            }

            if (importedDateFrom.HasValue)
            {
                var fromDate =
                    importedDateFrom
                        .Value
                        .Date;

                query =
                    query.Where(
                        invoice =>
                            invoice.CreatedDate >=
                                fromDate);
            }

            if (importedDateTo.HasValue)
            {
                var nextDate =
                    importedDateTo
                        .Value
                        .Date
                        .AddDays(
                            1);

                query =
                    query.Where(
                        invoice =>
                            invoice.CreatedDate <
                                nextDate);
            }

            return query;
        }

        private static IQueryable<
            InvoiceListItemDto>
            ProjectInvoiceRows(
                IQueryable<Invoice> query)
        {
            return query.Select(
                invoice =>
                    new InvoiceListItemDto
                    {
                        Id =
                            invoice.Id,

                        InvoiceNumber =
                            invoice.InvoiceNumber,

                        InvoiceDate =
                            invoice.InvoiceDate,

                        CustomerName =
                            invoice.CustomerName ??
                            string.Empty,

                        UploadedFileName =
                            invoice.UploadedFileName ??
                            string.Empty,

                        CreatedDate =
                            invoice.CreatedDate,

                        ItemCount =
                            invoice.Items.Count,

                        TotalQuantity =
                            invoice.Items.Sum(
                                item =>
                                    (decimal?)
                                    item.InvoiceQty) ??
                            0m,

                        TotalGstAmount =
                            invoice.Items.Sum(
                                item =>
                                    (decimal?)
                                    item.GstAmount) ??
                            0m,

                        TotalAmount =
                            invoice.Items.Sum(
                                item =>
                                    (decimal?)
                                    item.Amount) ??
                            0m
                    });
        }

        private static Dictionary<
            string,
            string[]>
            ParseInvoiceExcelFilters(
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
                        InvoiceExcelFilterColumns
                            .Contains(
                                pair.Key))
                    .ToDictionary(
                        pair =>
                            pair.Key
                                .Trim()
                                .ToLowerInvariant(),
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
            MatchesInvoiceExcelFilters(
                InvoiceListItemDto row,
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
                    GetInvoiceExcelCellValue(
                        row,
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
            GetInvoiceExcelCellValue(
                InvoiceListItemDto row,
                string column)
        {
            var normalized =
                column
                    .Trim()
                    .ToLowerInvariant();

            return normalized switch
            {
                "invoice" =>
                    row.InvoiceNumber,

                "invoicedate" =>
                    row.InvoiceDate
                        ?.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)
                    ?? string.Empty,

                "customer" =>
                    row.CustomerName,

                "items" =>
                    row.ItemCount
                        .ToString(
                            CultureInfo.InvariantCulture),

                "quantity" =>
                    row.TotalQuantity
                        .ToString(
                            "0.###",
                            CultureInfo.InvariantCulture),

                "gst" =>
                    row.TotalGstAmount
                        .ToString(
                            "0.##",
                            CultureInfo.InvariantCulture),

                "amount" =>
                    row.TotalAmount
                        .ToString(
                            "0.##",
                            CultureInfo.InvariantCulture),

                "uploadedfile" =>
                    row.UploadedFileName,

                "imported" =>
                    row.CreatedDate
                        .ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture),

                _ =>
                    string.Empty
            };
        }

        private static IOrderedEnumerable<
            InvoiceListItemDto>
            ApplyInvoiceSortInMemory(
                IEnumerable<
                    InvoiceListItemDto> rows,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                sortColumn?
                    .Trim()
                    .ToLowerInvariant() ??
                string.Empty;

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
                return rows
                    .OrderByDescending(
                        row =>
                            row.InvoiceDate)
                    .ThenByDescending(
                        row =>
                            row.CreatedDate)
                    .ThenByDescending(
                        row =>
                            row.Id);
            }

            Func<
                InvoiceListItemDto,
                object?> selector =
                column switch
                {
                    "invoice" =>
                        row =>
                            row.InvoiceNumber,

                    "invoicedate" =>
                        row =>
                            row.InvoiceDate,

                    "customer" =>
                        row =>
                            row.CustomerName,

                    "items" =>
                        row =>
                            row.ItemCount,

                    "quantity" =>
                        row =>
                            row.TotalQuantity,

                    "gst" =>
                        row =>
                            row.TotalGstAmount,

                    "amount" =>
                        row =>
                            row.TotalAmount,

                    "uploadedfile" =>
                        row =>
                            row.UploadedFileName,

                    "imported" =>
                        row =>
                            row.CreatedDate,

                    _ =>
                        row =>
                            row.InvoiceDate
                };

            return descending
                ? rows
                    .OrderByDescending(
                        selector)
                    .ThenByDescending(
                        row =>
                            row.Id)
                : rows
                    .OrderBy(
                        selector)
                    .ThenBy(
                        row =>
                            row.Id);
        }

        private static IOrderedQueryable<Invoice>
            ApplyInvoiceSort(
                IQueryable<Invoice> query,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                sortColumn?
                    .Trim()
                    .ToLowerInvariant() ??
                string.Empty;

            var descending =
                string.Equals(
                    sortDirection?.Trim(),
                    "desc",
                    StringComparison.OrdinalIgnoreCase);

            return column switch
            {
                "invoice" when descending =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.InvoiceNumber)
                        .ThenByDescending(
                            invoice =>
                                invoice.Id),

                "invoice" =>
                    query
                        .OrderBy(
                            invoice =>
                                invoice.InvoiceNumber)
                        .ThenBy(
                            invoice =>
                                invoice.Id),

                "invoicedate" when descending =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.InvoiceDate.HasValue)
                        .ThenByDescending(
                            invoice =>
                                invoice.InvoiceDate)
                        .ThenByDescending(
                            invoice =>
                                invoice.Id),

                "invoicedate" =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.InvoiceDate.HasValue)
                        .ThenBy(
                            invoice =>
                                invoice.InvoiceDate)
                        .ThenBy(
                            invoice =>
                                invoice.Id),

                "customer" when descending =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.CustomerName ??
                                string.Empty)
                        .ThenByDescending(
                            invoice =>
                                invoice.Id),

                "customer" =>
                    query
                        .OrderBy(
                            invoice =>
                                invoice.CustomerName ??
                                string.Empty)
                        .ThenBy(
                            invoice =>
                                invoice.Id),

                "items" when descending =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.Items.Count)
                        .ThenByDescending(
                            invoice =>
                                invoice.Id),

                "items" =>
                    query
                        .OrderBy(
                            invoice =>
                                invoice.Items.Count)
                        .ThenBy(
                            invoice =>
                                invoice.Id),

                "quantity" when descending =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.InvoiceQty) ??
                                0m)
                        .ThenByDescending(
                            invoice =>
                                invoice.Id),

                "quantity" =>
                    query
                        .OrderBy(
                            invoice =>
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.InvoiceQty) ??
                                0m)
                        .ThenBy(
                            invoice =>
                                invoice.Id),

                "gst" when descending =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.GstAmount) ??
                                0m)
                        .ThenByDescending(
                            invoice =>
                                invoice.Id),

                "gst" =>
                    query
                        .OrderBy(
                            invoice =>
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.GstAmount) ??
                                0m)
                        .ThenBy(
                            invoice =>
                                invoice.Id),

                "amount" when descending =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.Amount) ??
                                0m)
                        .ThenByDescending(
                            invoice =>
                                invoice.Id),

                "amount" =>
                    query
                        .OrderBy(
                            invoice =>
                                invoice.Items.Sum(
                                    item =>
                                        (decimal?)
                                        item.Amount) ??
                                0m)
                        .ThenBy(
                            invoice =>
                                invoice.Id),

                "uploadedfile" when descending =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.UploadedFileName ??
                                string.Empty)
                        .ThenByDescending(
                            invoice =>
                                invoice.Id),

                "uploadedfile" =>
                    query
                        .OrderBy(
                            invoice =>
                                invoice.UploadedFileName ??
                                string.Empty)
                        .ThenBy(
                            invoice =>
                                invoice.Id),

                "imported" when descending =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.CreatedDate)
                        .ThenByDescending(
                            invoice =>
                                invoice.Id),

                "imported" =>
                    query
                        .OrderBy(
                            invoice =>
                                invoice.CreatedDate)
                        .ThenBy(
                            invoice =>
                                invoice.Id),

                _ =>
                    query
                        .OrderByDescending(
                            invoice =>
                                invoice.InvoiceDate)
                        .ThenByDescending(
                            invoice =>
                                invoice.CreatedDate)
                        .ThenByDescending(
                            invoice =>
                                invoice.Id)
            };
        }


        // ============================================================
        // INVOICE DETAILS
        // ============================================================

        [HttpGet("{id:int}")]
        public async Task<ActionResult<InvoiceDetailsApiDto>> Details(
            int id)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    message = "Invalid invoice ID."
                });
            }

            var invoice = await _context.Invoices
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new
                {
                    item.Id,
                    item.InvoiceNumber,
                    item.InvoiceDate,
                    item.CustomerName,
                    item.UploadedFileName,
                    item.UploadedFilePath,
                    item.CreatedDate
                })
                .FirstOrDefaultAsync();

            if (invoice == null)
            {
                return NotFound(new
                {
                    message = "Invoice was not found."
                });
            }

            var invoiceItems = await _context.InvoiceItems
                .AsNoTracking()
                .Where(item => item.InvoiceId == id)
                .OrderBy(item => item.Id)
                .Select(item => new InvoiceItemApiDto
                {
                    Id = item.Id,
                    PONumber = item.PONumber ?? string.Empty,
                    LineItem = item.LineItem ?? string.Empty,
                    MaterialNo = item.MaterialNo ?? string.Empty,
                    POMaterialNo = item.Material != null
                        ? item.Material.MaterialNo ?? string.Empty
                        : string.Empty,
                    DispatchedQty = item.Material != null
                        ? item.Material.DispatchQty
                        : 0m,
                    DispatchStatus = item.Material != null
                        ? item.Material.DispatchStatus ?? string.Empty
                        : string.Empty,
                    Qty = item.InvoiceQty,
                    PriceUnit = item.PriceUnit,
                    GstAmount = item.GstAmount,
                    Amount = item.Amount,
                    PODate = null
                })
                .ToListAsync();

            var materialIdsByInvoiceItemId = await _context.InvoiceItems
                .AsNoTracking()
                .Where(item => item.InvoiceId == id)
                .ToDictionaryAsync(item => item.Id, item => item.MaterialId);

            // Imported rows that could not be safely matched are stored in
            // PendingInvoiceItems, so include them in the invoice's material
            // comparison as well. Otherwise their invoice material numbers
            // appear to be missing from this page.
            var pendingRows = await _context.PendingInvoiceItems
                .AsNoTracking()
                .Where(item =>
                    item.InvoiceId == id && item.Status == "Pending")
                .OrderBy(item => item.Id)
                .Select(item => new
                {
                    item.Id,
                    item.PONumber,
                    item.LineItem,
                    item.MaterialNo,
                    item.InvoiceQty,
                    item.PriceUnit,
                    item.GstAmount,
                    item.Amount
                })
                .ToListAsync();

            if (pendingRows.Count > 0)
            {
                var poNumbers = pendingRows
                    .Select(item => item.PONumber)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct()
                    .ToList();

                var poMaterials = await _context.Materials
                    .AsNoTracking()
                    .Where(material => material.PurchaseOrder != null &&
                        poNumbers.Contains(material.PurchaseOrder.PONumber))
                    .Select(material => new
                    {
                        material.Id,
                        PONumber = material.PurchaseOrder!.PONumber,
                        material.LineItem,
                        material.MaterialNo,
                        material.DispatchQty,
                        material.DispatchStatus
                    })
                    .ToListAsync();

                foreach (var pending in pendingRows)
                {
                    var pendingPoKey = NormalizeKey(pending.PONumber);
                    var pendingMaterialKey = NormalizeKey(pending.MaterialNo);
                    var matchedPoMaterial = poMaterials.FirstOrDefault(material =>
                        NormalizeKey(material.PONumber) == pendingPoKey &&
                        NormalizeKey(material.MaterialNo) == pendingMaterialKey);

                    var pendingItem = new InvoiceItemApiDto
                    {
                        Id = -pending.Id,
                        PONumber = pending.PONumber ?? string.Empty,
                        LineItem = pending.LineItem ?? string.Empty,
                        MaterialNo = pending.MaterialNo ?? string.Empty,
                        POMaterialNo = matchedPoMaterial?.MaterialNo ?? string.Empty,
                        DispatchedQty = matchedPoMaterial?.DispatchQty ?? 0m,
                        DispatchStatus = matchedPoMaterial?.DispatchStatus ?? string.Empty,
                        IsPending = true,
                        Qty = pending.InvoiceQty,
                        PriceUnit = pending.PriceUnit,
                        GstAmount = pending.GstAmount,
                        Amount = pending.Amount
                    };

                    if (matchedPoMaterial != null)
                    {
                        materialIdsByInvoiceItemId[pendingItem.Id] =
                            matchedPoMaterial.Id;
                    }

                    pendingItem.MaterialNumbersMatch =
                        !string.IsNullOrWhiteSpace(pendingItem.MaterialNo) &&
                        NormalizeKey(pendingItem.MaterialNo) ==
                        NormalizeKey(pendingItem.POMaterialNo);
                    invoiceItems.Add(pendingItem);
                }
            }

            var relatedMaterialIds = materialIdsByInvoiceItemId.Values
                .Distinct()
                .ToList();

            if (relatedMaterialIds.Count > 0)
            {
                var dispatchRows = await _context.Dispatches
                    .AsNoTracking()
                    .Where(dispatch => relatedMaterialIds.Contains(dispatch.MaterialId))
                    .OrderBy(dispatch => dispatch.DispatchDate)
                    .ThenBy(dispatch => dispatch.Id)
                    .Select(dispatch => new
                    {
                        dispatch.MaterialId,
                        dispatch.DispatchNo,
                        dispatch.DispatchQty
                    })
                    .ToListAsync();

                var dispatchesByMaterialId = dispatchRows
                    .GroupBy(dispatch => dispatch.MaterialId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(dispatch => new InvoiceDispatchApiDto
                        {
                            DispatchNo = dispatch.DispatchNo,
                            DispatchQty = dispatch.DispatchQty
                        }).ToList());

                foreach (var item in invoiceItems)
                {
                    if (!materialIdsByInvoiceItemId.TryGetValue(item.Id, out var materialId) ||
                        !dispatchesByMaterialId.TryGetValue(materialId, out var dispatches))
                    {
                        continue;
                    }

                    item.Dispatches = dispatches;
                    if (item.DispatchedQty <= 0)
                    {
                        item.DispatchedQty = dispatches.Sum(dispatch => dispatch.DispatchQty);
                    }
                }
            }

            foreach (var item in invoiceItems)
            {
                item.MaterialNumbersMatch =
                    !string.IsNullOrWhiteSpace(item.MaterialNo) &&
                    NormalizeKey(item.MaterialNo) ==
                    NormalizeKey(item.POMaterialNo);
            }

            return Ok(new InvoiceDetailsApiDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.InvoiceDate,
                CustomerName = invoice.CustomerName ?? string.Empty,
                UploadedFileName =
                    invoice.UploadedFileName ?? string.Empty,
                UploadedFilePath =
                    _documentReferences.ToDownloadUrl(
                        invoice.UploadedFilePath,
                        "invoices") ??
                    string.Empty,
                CreatedDate = invoice.CreatedDate,
                TotalQuantity = invoiceItems.Sum(item => item.Qty),
                TotalGstAmount = invoiceItems.Sum(item => item.GstAmount),
                TotalAmount = invoiceItems.Sum(item => item.Amount),
                Items = invoiceItems
            });
        }

        // ============================================================
        // IMPORT HISTORY
        // ============================================================

        [HttpGet("import-history")]
        public async Task<ActionResult<List<InvoiceImportBatchDto>>>
            ImportHistory(
                string? search,
                int take = 200)
        {
            take = Math.Clamp(take, 10, 500);

            var query = _context.VyaparInvoiceImportBatches
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchValue = search.Trim();

                query = query.Where(batch =>
                    batch.FileName != null &&
                    batch.FileName.Contains(searchValue));
            }

            var batches = await query
                .OrderByDescending(batch => batch.ImportDate)
                .Take(take)
                .Select(batch => new InvoiceImportBatchDto
                {
                    Id = batch.Id,
                    FileName = batch.FileName ?? string.Empty,
                    ImportDate = batch.ImportDate,
                    TotalRows =
                        batch.Rows.Count > 0
                            ? batch.Rows.Count
                            : batch.TotalRows,
                    SuccessRows = batch.Rows.Count(row =>
                        row.Status == "Success"),
                    FailedRows = 0,
                    PendingRows = batch.Rows.Count(row =>
                        row.Status == null ||
                        (row.Status != "Success" &&
                         row.Status != "Deleted")),
                    DeletedRows = batch.Rows.Count(row =>
                        row.Status == "Deleted")
                })
                .ToListAsync();

            return Ok(batches);
        }

        // ============================================================
        // PENDING / UNMATCHED INVOICE ITEMS
        // ============================================================

        [HttpGet("pending")]
        public async Task<ActionResult<PagedResult<PendingInvoiceItemApiDto>>>
            Pending(
                string? search,
                int page = 1,
                int pageSize = 25,
                string? sortColumn = null,
                string? sortDirection = null,
                string? excelFilters = null,
                CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = ApplyPendingInvoiceSearch(
                _context.PendingInvoiceItems
                .AsNoTracking()
                .Where(item => item.Status == "Pending"),
                search);

            query = ApplyPendingInvoiceExcelFilters(query, excelFilters);

            var totalRecords = await query.CountAsync(cancellationToken);
            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(totalRecords / (double)pageSize));
            page = Math.Min(page, totalPages);

            var entities = await ApplyPendingInvoiceSort(
                    query,
                    sortColumn,
                    sortDirection)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new PagedResult<PendingInvoiceItemApiDto>
            {
                Items = entities.Select(MapPendingInvoiceItem).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            });
        }


        // ============================================================
        // MANUAL MATCH CANDIDATES
        // Admin and Production use this endpoint from the Angular
        // pending-row review screen.
        // ============================================================

        [HttpGet("pending/{id:int}/candidates")]
        public async Task<ActionResult<
            PendingInvoiceMatchCandidatesResponseDto>>
            PendingCandidates(
                int id,
                string? search,
                int take = 50)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    message = "Invalid pending invoice row ID."
                });
            }

            take = Math.Clamp(take, 10, 200);

            var pending = await _context.PendingInvoiceItems
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.Status == "Pending");

            if (pending == null)
            {
                return NotFound(new
                {
                    message =
                        "The pending invoice row was not found or is already matched."
                });
            }

            var pendingPoKey = NormalizeKey(
                pending.PONumber);

            var pendingMaterialKey = NormalizeKey(
                pending.MaterialNo);

            var pendingLineKey = NormalizeLineItem(
                pending.LineItem);

            var searchValue = search?.Trim() ?? string.Empty;
            var searchKey = NormalizeKey(searchValue);
            var searchLineKey = NormalizeLineItem(searchValue);

            var materials = await _context.Materials
                .AsNoTracking()
                .ToListAsync();

            var purchaseOrderIds = materials
                .Select(material => material.PurchaseOrderId)
                .Distinct()
                .ToList();

            var purchaseOrderNumberMap =
                await _context.PurchaseOrders
                    .AsNoTracking()
                    .Where(purchaseOrder =>
                        purchaseOrderIds.Contains(
                            purchaseOrder.Id))
                    .ToDictionaryAsync(
                        purchaseOrder => purchaseOrder.Id,
                        purchaseOrder =>
                            purchaseOrder.PONumber ?? string.Empty);

            var candidates = materials
                .Select(material =>
                {
                    var poNumber =
                        purchaseOrderNumberMap.TryGetValue(
                            material.PurchaseOrderId,
                            out var storedPONumber)
                            ? storedPONumber.Trim()
                            : string.Empty;

                    var materialNo =
                        material.MaterialNo?.Trim() ??
                        string.Empty;

                    var lineItem =
                        material.LineItem?.Trim() ??
                        string.Empty;

                    var poKey = NormalizeKey(poNumber);
                    var materialKey = NormalizeKey(materialNo);
                    var lineKey = NormalizeLineItem(lineItem);

                    var exactPo =
                        !string.IsNullOrWhiteSpace(pendingPoKey) &&
                        poKey == pendingPoKey;

                    var exactMaterial =
                        !string.IsNullOrWhiteSpace(pendingMaterialKey) &&
                        materialKey == pendingMaterialKey;

                    var exactLine =
                        !string.IsNullOrWhiteSpace(pendingLineKey) &&
                        lineKey == pendingLineKey;

                    var balanceQty =
                        GetAvailableInvoiceBalance(material);

                    var score = 0;

                    if (exactPo)
                    {
                        score += 100;
                    }

                    if (exactMaterial)
                    {
                        score += 80;
                    }

                    if (exactLine)
                    {
                        score += 40;
                    }

                    if (!exactMaterial &&
                        !string.IsNullOrWhiteSpace(pendingMaterialKey) &&
                        (materialKey.Contains(pendingMaterialKey) ||
                         pendingMaterialKey.Contains(materialKey)))
                    {
                        score += 25;
                    }

                    if (!exactPo &&
                        !string.IsNullOrWhiteSpace(pendingPoKey) &&
                        (poKey.Contains(pendingPoKey) ||
                         pendingPoKey.Contains(poKey)))
                    {
                        score += 20;
                    }

                    if (!exactLine &&
                        !string.IsNullOrWhiteSpace(pendingLineKey) &&
                        lineKey == pendingLineKey)
                    {
                        score += 10;
                    }

                    var searchMatches =
                        string.IsNullOrWhiteSpace(searchValue) ||
                        (!string.IsNullOrWhiteSpace(searchKey) &&
                         (poKey.Contains(searchKey) ||
                          materialKey.Contains(searchKey) ||
                          NormalizeKey(material.ShortDescription)
                              .Contains(searchKey))) ||
                        (!string.IsNullOrWhiteSpace(searchLineKey) &&
                         lineKey == searchLineKey);

                    return new
                    {
                        Material = material,
                        PONumber = poNumber,
                        LineItem = lineItem,
                        MaterialNo = materialNo,
                        BalanceQty = balanceQty,
                        Score = score,
                        ExactPo = exactPo,
                        ExactMaterial = exactMaterial,
                        ExactLine = exactLine,
                        SearchMatches = searchMatches
                    };
                })
                .Where(candidate =>
                    candidate.SearchMatches)
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate =>
                    candidate.BalanceQty >= pending.InvoiceQty)
                .ThenByDescending(candidate => candidate.ExactMaterial)
                .ThenByDescending(candidate => candidate.ExactPo)
                .ThenBy(candidate => candidate.PONumber)
                .ThenBy(candidate => candidate.LineItem)
                .Take(take)
                .Select(candidate =>
                    new PendingInvoiceMatchCandidateDto
                    {
                        MaterialId = candidate.Material.Id,
                        PurchaseOrderId =
                            candidate.Material.PurchaseOrderId,
                        PONumber = candidate.PONumber,
                        LineItem = candidate.LineItem,
                        MaterialNo = candidate.MaterialNo,
                        ShortDescription =
                            candidate.Material.ShortDescription ??
                            string.Empty,
                        POQty = candidate.Material.Qty,
                        InvoiceQty =
                            candidate.Material.InvoiceQty,
                        BalanceQty = candidate.BalanceQty,
                        DispatchQty =
                            candidate.Material.DispatchQty,
                        PendingQty =
                            candidate.Material.PendingQty,
                        MatchScore = candidate.Score,
                        IsExactPO = candidate.ExactPo,
                        IsExactMaterial =
                            candidate.ExactMaterial,
                        IsExactLineItem =
                            candidate.ExactLine,
                        HasEnoughBalance =
                            pending.InvoiceQty > 0 &&
                            pending.InvoiceQty <=
                            candidate.BalanceQty
                    })
                .ToList();

            return Ok(
                new PendingInvoiceMatchCandidatesResponseDto
                {
                    PendingItem =
                        MapPendingInvoiceItem(pending),
                    Candidates = candidates
                });
        }

        [HttpDelete("pending/{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<IActionResult> DeletePending(int id, CancellationToken cancellationToken = default)
        {
            var item = await _context.PendingInvoiceItems
                .SingleOrDefaultAsync(row => row.Id == id && row.Status == "Pending", cancellationToken);
            if (item == null) return NotFound();
            _context.PendingInvoiceItems.Remove(item);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        // ============================================================
        // MANUAL MATCH ONE PENDING ROW
        // ============================================================

        [HttpPost("pending/{id:int}/match")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<ActionResult<
            PendingInvoiceManualMatchResponseDto>>
            MatchPendingManually(
                int id,
                [FromBody]
                PendingInvoiceManualMatchRequestDto request)
        {
            if (id <= 0 || request.MaterialId <= 0)
            {
                return BadRequest(
                    new PendingInvoiceManualMatchResponseDto
                    {
                        Success = false,
                        Message =
                            "A valid pending row and material are required.",
                        PendingItemId = id,
                        MaterialId = request.MaterialId
                    });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var pending = await _context.PendingInvoiceItems
                    .FirstOrDefaultAsync(item =>
                        item.Id == id &&
                        item.Status == "Pending");

                if (pending == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        new PendingInvoiceManualMatchResponseDto
                        {
                            Success = false,
                            Message =
                                "The pending row was not found or is already matched.",
                            PendingItemId = id,
                            MaterialId = request.MaterialId
                        });
                }

                var invoiceExists = await _context.Invoices
                    .AnyAsync(invoice =>
                        invoice.Id == pending.InvoiceId);

                if (!invoiceExists)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(
                        new PendingInvoiceManualMatchResponseDto
                        {
                            Success = false,
                            Message =
                                "The invoice linked to this pending row was not found.",
                            PendingItemId = pending.Id,
                            MaterialId = request.MaterialId,
                            InvoiceId = pending.InvoiceId
                        });
                }

                var material = await _context.Materials
                    .Include(item => item.PurchaseOrder)
                    .FirstOrDefaultAsync(item =>
                        item.Id == request.MaterialId);

                if (material == null ||
                    material.PurchaseOrder == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        new PendingInvoiceManualMatchResponseDto
                        {
                            Success = false,
                            Message =
                                "The selected PO material was not found.",
                            PendingItemId = pending.Id,
                            MaterialId = request.MaterialId,
                            InvoiceId = pending.InvoiceId
                        });
                }

                if (pending.InvoiceQty <= 0)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(
                        new PendingInvoiceManualMatchResponseDto
                        {
                            Success = false,
                            Message =
                                "Pending invoice quantity must be greater than zero.",
                            PendingItemId = pending.Id,
                            MaterialId = material.Id,
                            InvoiceId = pending.InvoiceId
                        });
                }

                var actualPoNumber =
                    material.PurchaseOrder.PONumber?.Trim() ??
                    string.Empty;

                var actualMaterialNo =
                    material.MaterialNo?.Trim() ??
                    string.Empty;

                var actualLineItem =
                    material.LineItem?.Trim() ??
                    string.Empty;

                var exactPo =
                    NormalizeKey(pending.PONumber) ==
                    NormalizeKey(actualPoNumber);

                var exactMaterial =
                    NormalizeKey(pending.MaterialNo) ==
                    NormalizeKey(actualMaterialNo);

                var pendingLineKey =
                    NormalizeLineItem(pending.LineItem);

                var materialLineKey =
                    NormalizeLineItem(actualLineItem);

                var exactLine =
                    string.IsNullOrWhiteSpace(pendingLineKey) ||
                    string.IsNullOrWhiteSpace(materialLineKey) ||
                    pendingLineKey == materialLineKey;

                if (!exactPo ||
                    !exactMaterial ||
                    !exactLine)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(
                        new PendingInvoiceManualMatchResponseDto
                        {
                            Success = false,
                            Message =
                                "Direct matching is allowed only when PO number, material number and line item are exact. Create the missing material instead.",
                            PendingItemId = pending.Id,
                            MaterialId = material.Id,
                            InvoiceId = pending.InvoiceId
                        });
                }

                var resolvedLineItem =
                    string.IsNullOrWhiteSpace(actualLineItem)
                        ? pending.LineItem ?? string.Empty
                        : actualLineItem;

                var savedLineItems =
                    await _context.InvoiceItems
                        .Where(item =>
                            item.InvoiceId == pending.InvoiceId &&
                            item.MaterialId == material.Id &&
                            item.InvoiceQty == pending.InvoiceQty)
                        .Select(item => item.LineItem)
                        .ToListAsync();

                var alreadySaved = savedLineItems.Any(savedLine =>
                    NormalizeLineItem(savedLine) ==
                    NormalizeLineItem(resolvedLineItem));

                if (!alreadySaved)
                {
                    var balanceQty =
                        GetAvailableInvoiceBalance(material);

                    if (pending.InvoiceQty > balanceQty)
                    {
                        await transaction.RollbackAsync();

                        return BadRequest(
                            new PendingInvoiceManualMatchResponseDto
                            {
                                Success = false,
                                Message =
                                    $"Invoice qty {pending.InvoiceQty} is greater than the selected material balance qty {balanceQty}.",
                                PendingItemId = pending.Id,
                                MaterialId = material.Id,
                                InvoiceId = pending.InvoiceId
                            });
                    }

                    _context.InvoiceItems.Add(
                        new InvoiceItem
                        {
                            InvoiceId =
                                pending.InvoiceId,
                            MaterialId =
                                material.Id,
                            PONumber =
                                actualPoNumber,
                            LineItem =
                                resolvedLineItem,
                            MaterialNo =
                                actualMaterialNo,
                            Description =
                                pending.Description,
                            HsnSacCode =
                                pending.HsnSacCode,
                            InvoiceQty =
                                pending.InvoiceQty,
                            PriceUnit =
                                pending.PriceUnit,
                            GstAmount =
                                pending.GstAmount,
                            Amount =
                                pending.Amount,
                            CreatedDate =
                                DateTime.Now
                        });

                    material.InvoiceQty +=
                        pending.InvoiceQty;

                    material.BalanceQty =
                        Math.Max(
                            0m,
                            material.Qty -
                            material.InvoiceQty);

                    material.InvoiceStatus =
                        GetInvoiceStatus(
                            material.Qty,
                            material.InvoiceQty,
                            material.BalanceQty);

                    material.DispatchQty +=
                        pending.InvoiceQty;

                    material.PendingQty =
                        Math.Max(
                            0m,
                            material.Qty -
                            material.DispatchQty);

                    material.DispatchDate =
                        pending.InvoiceDate ??
                        DateTime.Today;

                    material.DispatchStatus =
                        GetDispatchStatus(
                            material.Qty,
                            material.DispatchQty,
                            material.PendingQty);
                }

                pending.Status = "Matched";
                pending.MatchedMaterialId = material.Id;
                pending.MatchedDate = DateTime.Now;
                pending.ErrorMessage = null;

                await MarkImportAuditAsManualSuccessAsync(
                    pending,
                    actualPoNumber,
                    actualMaterialNo,
                    resolvedLineItem);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(
                    new PendingInvoiceManualMatchResponseDto
                    {
                        Success = true,
                        Message = alreadySaved
                            ? "The invoice item already existed. The pending row was safely marked as matched without changing quantities again."
                            : $"Pending row matched to PO {actualPoNumber}, material {actualMaterialNo}, line {resolvedLineItem}.",
                        PendingItemId = pending.Id,
                        MaterialId = material.Id,
                        InvoiceId = pending.InvoiceId
                    });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new PendingInvoiceManualMatchResponseDto
                    {
                        Success = false,
                        Message =
                            "Manual matching failed. No partial changes were saved.",
                        PendingItemId = id,
                        MaterialId = request.MaterialId
                    });
            }
        }

        // ============================================================
        // RETRY ALL PENDING INVOICE MATCHES
        // Admin and Production only. Viewer POST requests are blocked
        // by SessionAuthorizationFilter.
        // ============================================================

        [HttpPost("pending/retry")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<ActionResult<PendingInvoiceRetryResponseDto>>
            RetryPending()
        {
            var pendingBefore = await _context.PendingInvoiceItems
                .CountAsync(item => item.Status == "Pending");

            if (pendingBefore == 0)
            {
                return Ok(new PendingInvoiceRetryResponseDto
                {
                    Success = true,
                    Message = "There are no pending invoice rows to retry.",
                    PendingBefore = 0,
                    MatchedRows = 0,
                    RemainingRows = 0
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var purchaseOrderIds = await _context.Materials
                    .AsNoTracking()
                    .Select(material => material.PurchaseOrderId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToListAsync();

                foreach (var purchaseOrderId in purchaseOrderIds)
                {
                    await _pendingInvoiceMatcher
                        .MatchPendingInvoicesForPurchaseOrderAsync(
                            purchaseOrderId);
                }

                var remainingRows = await _context.PendingInvoiceItems
                    .CountAsync(item => item.Status == "Pending");

                var matchedRows = Math.Max(
                    0,
                    pendingBefore - remainingRows);

                var batches = await _context
                    .VyaparInvoiceImportBatches
                    .Include(batch => batch.Rows)
                    .ToListAsync();

                foreach (var batch in batches)
                {
                    batch.SuccessRows = batch.Rows.Count(row =>
                        row.Status == "Success");

                    batch.FailedRows = batch.Rows.Count(row =>
                        row.Status == "Failed");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var message = matchedRows > 0
                    ? $"{matchedRows} pending invoice row(s) were " +
                      $"matched successfully. {remainingRows} row(s) " +
                      "still require PO/material corrections."
                    : "No pending rows could be matched. Check the PO " +
                      "number, material number, line item and available " +
                      "balance quantity.";

                return Ok(new PendingInvoiceRetryResponseDto
                {
                    Success = true,
                    Message = message,
                    PendingBefore = pendingBefore,
                    MatchedRows = matchedRows,
                    RemainingRows = remainingRows
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new PendingInvoiceRetryResponseDto
                    {
                        Success = false,
                        Message =
                            "Retry matching failed. No partial matching " +
                            "changes were committed.",
                        PendingBefore = pendingBefore,
                        MatchedRows = 0,
                        RemainingRows = pendingBefore
                    });
            }
        }

        // ============================================================
        // DELETE INVOICE AND RESTORE MATERIAL QUANTITIES
        // ============================================================

        [HttpDelete("{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<ActionResult<InvoiceDeleteResponseDto>> Delete(
            int id)
        {
            if (id <= 0)
            {
                return BadRequest(new InvoiceDeleteResponseDto
                {
                    Success = false,
                    Message = "Invalid invoice ID.",
                    InvoiceId = id
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var invoice = await _context.Invoices
                    .Include(item => item.Items)
                    .FirstOrDefaultAsync(item => item.Id == id);

                if (invoice == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(new InvoiceDeleteResponseDto
                    {
                        Success = false,
                        Message = "Invoice was not found.",
                        InvoiceId = id
                    });
                }

                var quantityByMaterial = invoice.Items
                    .GroupBy(item => item.MaterialId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Sum(item => item.InvoiceQty));

                var materialIds = quantityByMaterial.Keys.ToList();

                var materials = await _context.Materials
                    .Where(material => materialIds.Contains(material.Id))
                    .ToListAsync();

                foreach (var material in materials)
                {
                    if (!quantityByMaterial.TryGetValue(
                            material.Id,
                            out var invoiceQuantity))
                    {
                        continue;
                    }

                    material.InvoiceQty = Math.Max(
                        0m,
                        material.InvoiceQty - invoiceQuantity);

                    material.BalanceQty = Math.Max(
                        0m,
                        material.Qty - material.InvoiceQty);

                    material.InvoiceStatus = GetInvoiceStatus(
                        material.Qty,
                        material.InvoiceQty,
                        material.BalanceQty);

                    material.DispatchQty = Math.Max(
                        0m,
                        material.DispatchQty - invoiceQuantity);

                    material.PendingQty = Math.Max(
                        0m,
                        material.Qty - material.DispatchQty);

                    if (material.DispatchQty <= 0)
                    {
                        material.DispatchDate = null;
                    }

                    material.DispatchStatus = GetDispatchStatus(
                        material.Qty,
                        material.DispatchQty,
                        material.PendingQty);
                }

                var pendingItems = await _context.PendingInvoiceItems
                    .Where(item => item.InvoiceId == invoice.Id)
                    .ToListAsync();

                if (pendingItems.Count > 0)
                {
                    _context.PendingInvoiceItems.RemoveRange(
                        pendingItems);
                }

                var importRows = await _context.VyaparInvoiceImportRows
                    .Where(row =>
                        row.InvoiceNumber == invoice.InvoiceNumber)
                    .ToListAsync();

                foreach (var row in importRows)
                {
                    row.Status = "Deleted";
                    row.ErrorMessage =
                        "Invoice deleted. This row can be imported again.";
                }

                _context.Invoices.Remove(invoice);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new InvoiceDeleteResponseDto
                {
                    Success = true,
                    Message =
                        $"Invoice {invoice.InvoiceNumber} was deleted " +
                        "and material invoice/dispatch quantities were restored.",
                    InvoiceId = invoice.Id
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new InvoiceDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Unable to delete the invoice. " +
                            "No partial changes were saved.",
                        InvoiceId = id
                    });
            }
        }

        // ============================================================
        // BULK DELETE INVOICES AND RESTORE MATERIAL QUANTITIES
        // ============================================================

        [HttpPost("bulk-delete")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<ActionResult<InvoiceBulkDeleteResponseDto>>
            BulkDelete(
                [FromBody]
                InvoiceBulkDeleteRequestDto? request)
        {
            var invoiceIds =
                request?.InvoiceIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList() ??
                new List<int>();

            if (invoiceIds.Count == 0)
            {
                return BadRequest(
                    new InvoiceBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Select at least one invoice to delete.",
                        RequestedCount = 0,
                        DeletedCount = 0,
                        MissingCount = 0
                    });
            }

            if (invoiceIds.Count > 200)
            {
                return BadRequest(
                    new InvoiceBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "A maximum of 200 invoices can be deleted " +
                            "at one time.",
                        RequestedCount = invoiceIds.Count,
                        DeletedCount = 0,
                        MissingCount = 0
                    });
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var invoices =
                    await _context.Invoices
                        .Include(invoice =>
                            invoice.Items)
                        .Where(invoice =>
                            invoiceIds.Contains(
                                invoice.Id))
                        .OrderBy(invoice =>
                            invoice.Id)
                        .ToListAsync();

                var missingCount =
                    invoiceIds.Count -
                    invoices.Count;

                if (invoices.Count == 0)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        new InvoiceBulkDeleteResponseDto
                        {
                            Success = false,
                            Message =
                                "None of the selected invoices were found.",
                            RequestedCount = invoiceIds.Count,
                            DeletedCount = 0,
                            MissingCount = invoiceIds.Count
                        });
                }

                var deletedInvoiceIds =
                    invoices
                        .Select(invoice =>
                            invoice.Id)
                        .ToList();

                var quantityByMaterial =
                    invoices
                        .SelectMany(invoice =>
                            invoice.Items)
                        .Where(item =>
                            item.MaterialId > 0)
                        .GroupBy(item =>
                            item.MaterialId)
                        .ToDictionary(
                            group =>
                                group.Key,
                            group =>
                                group.Sum(item =>
                                    item.InvoiceQty));

                var materialIds =
                    quantityByMaterial.Keys
                        .ToList();

                var materials =
                    materialIds.Count == 0
                        ? new List<Material>()
                        : await _context.Materials
                            .Where(material =>
                                materialIds.Contains(
                                    material.Id))
                            .ToListAsync();

                foreach (var material in materials)
                {
                    if (!quantityByMaterial.TryGetValue(
                            material.Id,
                            out var invoiceQuantity))
                    {
                        continue;
                    }

                    material.InvoiceQty =
                        Math.Max(
                            0m,
                            material.InvoiceQty -
                            invoiceQuantity);

                    material.BalanceQty =
                        Math.Max(
                            0m,
                            material.Qty -
                            material.InvoiceQty);

                    material.InvoiceStatus =
                        GetInvoiceStatus(
                            material.Qty,
                            material.InvoiceQty,
                            material.BalanceQty);

                    material.DispatchQty =
                        Math.Max(
                            0m,
                            material.DispatchQty -
                            invoiceQuantity);

                    material.PendingQty =
                        Math.Max(
                            0m,
                            material.Qty -
                            material.DispatchQty);

                    if (material.DispatchQty <= 0)
                    {
                        material.DispatchDate =
                            null;
                    }

                    material.DispatchStatus =
                        GetDispatchStatus(
                            material.Qty,
                            material.DispatchQty,
                            material.PendingQty);
                }

                var pendingItems =
                    await _context.PendingInvoiceItems
                        .Where(item =>
                            deletedInvoiceIds.Contains(
                                item.InvoiceId))
                        .ToListAsync();

                if (pendingItems.Count > 0)
                {
                    _context.PendingInvoiceItems
                        .RemoveRange(
                            pendingItems);
                }

                var invoiceNumbers =
                    invoices
                        .Select(invoice =>
                            invoice.InvoiceNumber)
                        .Where(invoiceNumber =>
                            !string.IsNullOrWhiteSpace(
                                invoiceNumber))
                        .Select(invoiceNumber =>
                            invoiceNumber!)
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToList();

                if (invoiceNumbers.Count > 0)
                {
                    var importRows =
                        await _context
                            .VyaparInvoiceImportRows
                            .Where(row =>
                                row.InvoiceNumber != null &&
                                invoiceNumbers.Contains(
                                    row.InvoiceNumber))
                            .ToListAsync();

                    foreach (var row in importRows)
                    {
                        row.Status =
                            "Deleted";

                        row.ErrorMessage =
                            "Invoice deleted. This row can be imported again.";
                    }
                }

                _context.Invoices.RemoveRange(
                    invoices);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var message =
                    $"{invoices.Count} invoice(s) deleted successfully. " +
                    "Material invoice and dispatch quantities were restored.";

                if (missingCount > 0)
                {
                    message +=
                        $" {missingCount} selected invoice(s) were not found.";
                }

                return Ok(
                    new InvoiceBulkDeleteResponseDto
                    {
                        Success = true,
                        Message = message,
                        RequestedCount = invoiceIds.Count,
                        DeletedCount = invoices.Count,
                        MissingCount = missingCount,
                        DeletedInvoiceIds =
                            deletedInvoiceIds
                    });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new InvoiceBulkDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Unable to delete the selected invoices. " +
                            "No partial changes were saved.",
                        RequestedCount = invoiceIds.Count,
                        DeletedCount = 0,
                        MissingCount = 0
                    });
            }
        }
        // ============================================================
        // DELETE IMPORT HISTORY ONLY
        // Actual invoices and material quantities are not changed.
        // ============================================================

        [HttpDelete("import-history/{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        public async Task<ActionResult<
            InvoiceImportBatchDeleteResponseDto>> DeleteImportBatch(
                int id)
        {
            if (id <= 0)
            {
                return BadRequest(
                    new InvoiceImportBatchDeleteResponseDto
                    {
                        Success = false,
                        Message = "Invalid import batch ID.",
                        BatchId = id
                    });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var batch = await _context.VyaparInvoiceImportBatches
                    .FirstOrDefaultAsync(item => item.Id == id);

                if (batch == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        new InvoiceImportBatchDeleteResponseDto
                        {
                            Success = false,
                            Message = "Import history was not found.",
                            BatchId = id
                        });
                }

                var rows = await _context.VyaparInvoiceImportRows
                    .Where(row => row.BatchId == id)
                    .ToListAsync();

                if (rows.Count > 0)
                {
                    _context.VyaparInvoiceImportRows.RemoveRange(rows);
                }

                _context.VyaparInvoiceImportBatches.Remove(batch);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new InvoiceImportBatchDeleteResponseDto
                {
                    Success = true,
                    Message =
                        $"Import history '{batch.FileName}' was deleted. " +
                        "Actual invoices and material quantities were not changed.",
                    BatchId = batch.Id
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new InvoiceImportBatchDeleteResponseDto
                    {
                        Success = false,
                        Message =
                            "Unable to delete import history. " +
                            "No partial changes were saved.",
                        BatchId = id
                    });
            }
        }


        private static PendingInvoiceItemApiDto
            MapPendingInvoiceItem(
                PendingInvoiceItem item)
        {
            return new PendingInvoiceItemApiDto
            {
                Id = item.Id,
                InvoiceId = item.InvoiceId,
                InvoiceNumber =
                    item.InvoiceNumber ?? string.Empty,
                InvoiceDate = item.InvoiceDate,
                PONumber =
                    item.PONumber ?? string.Empty,
                LineItem =
                    item.LineItem ?? string.Empty,
                MaterialNo =
                    item.MaterialNo ?? string.Empty,
                CustomerName =
                    item.CustomerName ?? string.Empty,
                Description =
                    item.Description ?? string.Empty,
                HsnSacCode =
                    item.HsnSacCode ?? string.Empty,
                Qty = item.InvoiceQty,
                PriceUnit = item.PriceUnit,
                GstAmount = item.GstAmount,
                Amount = item.Amount,
                ErrorMessage =
                    item.ErrorMessage ?? string.Empty,
                CreatedDate = item.CreatedDate
            };
        }

        private static decimal GetAvailableInvoiceBalance(
            Material material)
        {
            return Math.Max(0m, material.Qty - material.InvoiceQty);
        }

        private async Task MarkImportAuditAsManualSuccessAsync(
            PendingInvoiceItem pending,
            string actualPoNumber,
            string actualMaterialNo,
            string actualLineItem)
        {
            var invoiceNumber =
                pending.InvoiceNumber?.Trim() ??
                string.Empty;

            var poKey =
                NormalizeKey(pending.PONumber);

            var materialKey =
                NormalizeKey(pending.MaterialNo);

            var lineKey =
                NormalizeLineItem(pending.LineItem);

            var rows = await _context
                .VyaparInvoiceImportRows
                .Where(row =>
                    row.Status == "Pending" &&
                    row.InvoiceNumber == invoiceNumber)
                .ToListAsync();

            foreach (var row in rows.Where(row =>
                NormalizeKey(row.PONumber) == poKey &&
                NormalizeKey(row.MaterialNumber) == materialKey &&
                NormalizeLineItem(row.LineItem) == lineKey &&
                row.InvoiceQty == pending.InvoiceQty))
            {
                row.Status = "Success";
                row.ErrorMessage =
                    $"Manually matched to PO {actualPoNumber}, material {actualMaterialNo}, line {actualLineItem}.";
            }
        }

        private static string NormalizeKey(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(
                    value.Trim(),
                    @"[^A-Za-z0-9]",
                    string.Empty)
                .ToUpperInvariant();
        }

        private static string NormalizeLineItem(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var digits = Regex.Replace(
                value.Trim(),
                @"[^0-9]",
                string.Empty);

            return string.IsNullOrWhiteSpace(digits)
                ? string.Empty
                : digits;
        }

        // ============================================================
        // STATUS HELPERS
        // ============================================================

        private static string GetInvoiceStatus(
            decimal poQty,
            decimal invoiceQty,
            decimal balanceQty)
        {
            if (invoiceQty <= 0)
            {
                return "Not Invoiced";
            }

            if (balanceQty <= 0 || invoiceQty >= poQty)
            {
                return "Fully Invoiced";
            }

            return "Partially Invoiced";
        }

        private static string GetDispatchStatus(
            decimal poQty,
            decimal dispatchQty,
            decimal pendingQty)
        {
            if (dispatchQty <= 0)
            {
                return "Pending";
            }

            if (pendingQty <= 0 || dispatchQty >= poQty)
            {
                return "Dispatched";
            }

            return "Partial";
        }
    }
}
