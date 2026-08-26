using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.ViewModels;

namespace POTracker.Controllers
{
    public partial class PendingMaterialController
    {
        private static readonly HashSet<string>
            PendingMaterialExcelFilterColumns =
                new(
                    new[]
                    {
                        "due",
                        "po",
                        "material",
                        "description",
                        "poqty",
                        "invoice",
                        "invoicebalance",
                        "dispatch",
                        "pendingdispatch",
                        "progress",
                        "process",
                        "invoicestatus",
                        "dispatchstatus",
                        "delivery",
                        "assigned"
                    },
                    StringComparer.OrdinalIgnoreCase);

        [HttpGet("~/api/pending-materials")]
        public async Task<IActionResult> GetPendingMaterialsApi(
            string? search,
            string? poNumber,
            string? handler,
            string? assignedDashboard,
            string? processStatus,
            string? invoiceStatus,
            string? dispatchStatus,
            DateTime? poDateFrom,
            DateTime? poDateTo,
            DateTime? poReceivedDateFrom,
            DateTime? poReceivedDateTo,
            DateTime? deliveryDateFrom,
            DateTime? deliveryDateTo,
            bool overdueOnly = false,
            bool dueThisWeekOnly = false,

            string? dueStatus = null,
            string? columnPoNumber = null,
            string? columnHandler = null,
            string? columnMaterialNo = null,
            string? columnLineItem = null,
            string? columnDescription = null,
            decimal? minPoQuantity = null,
            decimal? maxPoQuantity = null,
            decimal? minInvoiceQuantity = null,
            decimal? maxInvoiceQuantity = null,
            decimal? minInvoiceBalanceQuantity = null,
            decimal? maxInvoiceBalanceQuantity = null,
            decimal? minDispatchQuantity = null,
            decimal? maxDispatchQuantity = null,
            decimal? minPendingDispatchQuantity = null,
            decimal? maxPendingDispatchQuantity = null,
            decimal? minCompletionPercentage = null,
            decimal? maxCompletionPercentage = null,
            string? columnProcessStatus = null,
            string? columnInvoiceStatus = null,
            string? columnDispatchStatus = null,
            DateTime? columnDeliveryDateFrom = null,
            DateTime? columnDeliveryDateTo = null,
            string? columnAssignedDashboard = null,
            string? excelFilters = null,
            string? sortColumn = null,
            string? sortDirection = null,

            int page = 1,
            int pageSize = 25)
        {
            if (!IsLoggedIn())
            {
                return Unauthorized(new
                {
                    message =
                        "Your session has expired. Sign in again."
                });
            }

            page = Math.Max(1, page);
            pageSize = NormalizePageSize(pageSize);

            var baseQuery =
                CreatePendingQuery();

            var dropdownData =
                await LoadDropdownDataAsync(baseQuery);

            var filteredQuery =
                ApplyFilters(
                    baseQuery,
                    search,
                    poNumber,
                    handler,
                    assignedDashboard,
                    processStatus,
                    invoiceStatus,
                    dispatchStatus,
                    poDateFrom,
                    poDateTo,
                    poReceivedDateFrom,
                    poReceivedDateTo,
                    deliveryDateFrom,
                    deliveryDateTo,
                    overdueOnly,
                    dueThisWeekOnly);

            filteredQuery =
                ApplyHeaderFilters(
                    filteredQuery,
                    dueStatus,
                    columnPoNumber,
                    columnHandler,
                    columnMaterialNo,
                    columnLineItem,
                    columnDescription,
                    minPoQuantity,
                    maxPoQuantity,
                    minInvoiceQuantity,
                    maxInvoiceQuantity,
                    minInvoiceBalanceQuantity,
                    maxInvoiceBalanceQuantity,
                    minDispatchQuantity,
                    maxDispatchQuantity,
                    minPendingDispatchQuantity,
                    maxPendingDispatchQuantity,
                    minCompletionPercentage,
                    maxCompletionPercentage,
                    columnProcessStatus,
                    columnInvoiceStatus,
                    columnDispatchStatus,
                    columnDeliveryDateFrom,
                    columnDeliveryDateTo,
                    columnAssignedDashboard);

            var excelFilterSelections =
                ParsePendingMaterialExcelFilters(
                    excelFilters);

            var requiresInMemory =
                excelFilterSelections.Count >
                    0 ||
                !string.IsNullOrWhiteSpace(
                    sortColumn);

            PendingMaterialSummaryViewModel
                summary;

            List<PendingMaterialRowViewModel>
                pageRows;

            int totalRecords;

            int totalPages;

            if (!requiresInMemory)
            {
                summary =
                    await BuildSummaryAsync(
                        filteredQuery);

                totalRecords =
                    summary.TotalPendingMaterials;

                totalPages =
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

                var materials =
                    await filteredQuery
                        .OrderBy(material =>
                            !material.DeliveryDate
                                .HasValue
                                ? 2
                                : material.DeliveryDate <
                                  today
                                    ? 0
                                    : 1)
                        .ThenBy(material =>
                            material.DeliveryDate)
                        .ThenByDescending(material =>
                            material.Id)
                        .Skip(
                            (page - 1) *
                            pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                pageRows =
                    materials
                        .Select(MapRow)
                        .ToList();
            }
            else
            {
                var allRows =
                    (
                        await filteredQuery
                            .ToListAsync()
                    )
                    .Select(MapRow)
                    .Where(row =>
                        MatchesPendingMaterialExcelFilters(
                            row,
                            excelFilterSelections))
                    .ToList();

                summary =
                    BuildSummary(
                        allRows);

                totalRecords =
                    allRows.Count;

                totalPages =
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

                pageRows =
                    ApplyPendingMaterialSortInMemory(
                            allRows,
                            sortColumn,
                            sortDirection)
                        .Skip(
                            (page - 1) *
                            pageSize)
                        .Take(pageSize)
                        .ToList();
            }

            var items =
                pageRows
                    .Select(
                        MapPendingMaterialApiItem)
                    .ToList();

            return Ok(
                new PendingMaterialsResponseDto
                {
                    Items = items,

                    Summary =
                        new PendingMaterialSummaryDto
                        {
                            TotalPendingMaterials =
                                summary.TotalPendingMaterials,
                            OverdueMaterials =
                                summary.OverdueMaterials,
                            DueTodayMaterials =
                                summary.DueTodayMaterials,
                            DueThisWeekMaterials =
                                summary.DueThisWeekMaterials,
                            WithoutDeliveryDate =
                                summary.WithoutDeliveryDate,
                            TotalPOQuantity =
                                summary.TotalPOQuantity,
                            TotalInvoiceQuantity =
                                summary.TotalInvoiceQuantity,
                            TotalInvoiceBalanceQuantity =
                                summary.TotalInvoiceBalanceQuantity,
                            TotalDispatchQuantity =
                                summary.TotalDispatchQuantity,
                            TotalPendingDispatchQuantity =
                                summary.TotalPendingDispatchQuantity,
                            TotalPendingValue =
                                summary.TotalPendingValue,
                            AssignmentSummary =
                                summary.AssignmentSummary
                                    .Select(item =>
                                        new PendingMaterialAssignmentDto
                                        {
                                            AssignedDashboard =
                                                item.AssignedDashboard,
                                            MaterialCount =
                                                item.MaterialCount,
                                            PendingQuantity =
                                                item.PendingQuantity,
                                            OverdueCount =
                                                item.OverdueCount
                                        })
                                    .ToList()
                        },

                    Filters =
                        new PendingMaterialFilterDataDto
                        {
                            Handlers =
                                MapFilterOptions(
                                    dropdownData.Handlers),
                            AssignedDashboards =
                                MapFilterOptions(
                                    dropdownData.AssignedDashboards),
                            ProcessStatuses =
                                MapFilterOptions(
                                    dropdownData.ProcessStatuses),
                            InvoiceStatuses =
                                MapFilterOptions(
                                    dropdownData.InvoiceStatuses),
                            DispatchStatuses =
                                MapFilterOptions(
                                    dropdownData.DispatchStatuses)
                        },

                    Page = page,
                    PageSize = pageSize,
                    TotalRecords = totalRecords,
                    TotalPages = totalPages
                });
        }

        [HttpGet("~/api/pending-materials/excel-filter-values")]
        [ProducesResponseType(
            typeof(
                IReadOnlyList<
                    ExcelFilterValueDto>),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<
                IReadOnlyList<
                    ExcelFilterValueDto>>>
            GetPendingMaterialExcelFilterValues(
                string column,
                string? search,
                string? poNumber,
                string? handler,
                string? assignedDashboard,
                string? processStatus,
                string? invoiceStatus,
                string? dispatchStatus,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                bool overdueOnly = false,
                bool dueThisWeekOnly = false,
                string? dueStatus = null,
                string? columnPoNumber = null,
                string? columnHandler = null,
                string? columnMaterialNo = null,
                string? columnLineItem = null,
                string? columnDescription = null,
                decimal? minPoQuantity = null,
                decimal? maxPoQuantity = null,
                decimal? minInvoiceQuantity = null,
                decimal? maxInvoiceQuantity = null,
                decimal? minInvoiceBalanceQuantity = null,
                decimal? maxInvoiceBalanceQuantity = null,
                decimal? minDispatchQuantity = null,
                decimal? maxDispatchQuantity = null,
                decimal? minPendingDispatchQuantity = null,
                decimal? maxPendingDispatchQuantity = null,
                decimal? minCompletionPercentage = null,
                decimal? maxCompletionPercentage = null,
                string? columnProcessStatus = null,
                string? columnInvoiceStatus = null,
                string? columnDispatchStatus = null,
                DateTime? columnDeliveryDateFrom = null,
                DateTime? columnDeliveryDateTo = null,
                string? columnAssignedDashboard = null,
                string? excelFilters = null,
                CancellationToken cancellationToken = default)
        {
            if (!IsLoggedIn())
            {
                return Unauthorized();
            }

            var normalizedColumn =
                column?
                    .Trim()
                    .ToLowerInvariant() ??
                string.Empty;

            if (
                !PendingMaterialExcelFilterColumns
                    .Contains(
                        normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Unsupported Pending Materials Excel filter column."
                    });
            }

            var filteredQuery =
                ApplyFilters(
                    CreatePendingQuery(),
                    search,
                    poNumber,
                    handler,
                    assignedDashboard,
                    processStatus,
                    invoiceStatus,
                    dispatchStatus,
                    poDateFrom,
                    poDateTo,
                    poReceivedDateFrom,
                    poReceivedDateTo,
                    deliveryDateFrom,
                    deliveryDateTo,
                    overdueOnly,
                    dueThisWeekOnly);

            filteredQuery =
                ApplyHeaderFilters(
                    filteredQuery,
                    dueStatus,
                    columnPoNumber,
                    columnHandler,
                    columnMaterialNo,
                    columnLineItem,
                    columnDescription,
                    minPoQuantity,
                    maxPoQuantity,
                    minInvoiceQuantity,
                    maxInvoiceQuantity,
                    minInvoiceBalanceQuantity,
                    maxInvoiceBalanceQuantity,
                    minDispatchQuantity,
                    maxDispatchQuantity,
                    minPendingDispatchQuantity,
                    maxPendingDispatchQuantity,
                    minCompletionPercentage,
                    maxCompletionPercentage,
                    columnProcessStatus,
                    columnInvoiceStatus,
                    columnDispatchStatus,
                    columnDeliveryDateFrom,
                    columnDeliveryDateTo,
                    columnAssignedDashboard);

            var rows =
                (
                    await filteredQuery
                        .ToListAsync(
                            cancellationToken)
                )
                .Select(MapRow)
                .ToList();

            var selections =
                ParsePendingMaterialExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var values =
                rows
                    .Where(row =>
                        MatchesPendingMaterialExcelFilters(
                            row,
                            selections))
                    .Select(row =>
                        GetPendingMaterialExcelCellValue(
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

        /// <summary>
        /// Exports all filtered Pending Materials used by the Angular screen.
        /// </summary>
        [HttpGet("~/api/pending-materials/export-excel")]
        [Produces(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult>
            ExportPendingMaterialsExcelApi(
                string? search,
                string? poNumber,
                string? handler,
                string? assignedDashboard,
                string? processStatus,
                string? invoiceStatus,
                string? dispatchStatus,
                DateTime? poDateFrom,
                DateTime? poDateTo,
                DateTime? poReceivedDateFrom,
                DateTime? poReceivedDateTo,
                DateTime? deliveryDateFrom,
                DateTime? deliveryDateTo,
                bool overdueOnly = false,
                bool dueThisWeekOnly = false,

                string? dueStatus = null,
                string? columnPoNumber = null,
                string? columnHandler = null,
                string? columnMaterialNo = null,
                string? columnLineItem = null,
                string? columnDescription = null,
                decimal? minPoQuantity = null,
                decimal? maxPoQuantity = null,
                decimal? minInvoiceQuantity = null,
                decimal? maxInvoiceQuantity = null,
                decimal? minInvoiceBalanceQuantity = null,
                decimal? maxInvoiceBalanceQuantity = null,
                decimal? minDispatchQuantity = null,
                decimal? maxDispatchQuantity = null,
                decimal? minPendingDispatchQuantity = null,
                decimal? maxPendingDispatchQuantity = null,
                decimal? minCompletionPercentage = null,
                decimal? maxCompletionPercentage = null,
                string? columnProcessStatus = null,
                string? columnInvoiceStatus = null,
                string? columnDispatchStatus = null,
                DateTime? columnDeliveryDateFrom = null,
                DateTime? columnDeliveryDateTo = null,
                string? columnAssignedDashboard = null,
                string? excelFilters = null,
                string? sortColumn = null,
                string? sortDirection = null,

                CancellationToken cancellationToken = default)
        {
            if (!IsLoggedIn())
            {
                return Unauthorized(new
                {
                    message =
                        "Your session has expired. Sign in again."
                });
            }

            var filteredQuery =
                ApplyFilters(
                    CreatePendingQuery(),
                    search,
                    poNumber,
                    handler,
                    assignedDashboard,
                    processStatus,
                    invoiceStatus,
                    dispatchStatus,
                    poDateFrom,
                    poDateTo,
                    poReceivedDateFrom,
                    poReceivedDateTo,
                    deliveryDateFrom,
                    deliveryDateTo,
                    overdueOnly,
                    dueThisWeekOnly);

            filteredQuery =
                ApplyHeaderFilters(
                    filteredQuery,
                    dueStatus,
                    columnPoNumber,
                    columnHandler,
                    columnMaterialNo,
                    columnLineItem,
                    columnDescription,
                    minPoQuantity,
                    maxPoQuantity,
                    minInvoiceQuantity,
                    maxInvoiceQuantity,
                    minInvoiceBalanceQuantity,
                    maxInvoiceBalanceQuantity,
                    minDispatchQuantity,
                    maxDispatchQuantity,
                    minPendingDispatchQuantity,
                    maxPendingDispatchQuantity,
                    minCompletionPercentage,
                    maxCompletionPercentage,
                    columnProcessStatus,
                    columnInvoiceStatus,
                    columnDispatchStatus,
                    columnDeliveryDateFrom,
                    columnDeliveryDateTo,
                    columnAssignedDashboard);

            var materials =
                await filteredQuery
                    .OrderBy(material =>
                        !material.DeliveryDate.HasValue
                            ? 2
                            : material.DeliveryDate.Value.Date <
                              DateTime.Today
                                ? 0
                                : 1)
                    .ThenBy(material =>
                        material.DeliveryDate)
                    .ThenByDescending(material =>
                        material.Id)
                    .ToListAsync(cancellationToken);

            var excelFilterSelections =
                ParsePendingMaterialExcelFilters(
                    excelFilters);

            var rows =
                ApplyPendingMaterialSortInMemory(
                    materials
                        .Select(MapRow)
                        .Where(row =>
                            MatchesPendingMaterialExcelFilters(
                                row,
                                excelFilterSelections)),
                    sortColumn,
                    sortDirection)
                .ToList();

            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add(
                    "Pending Materials");

            WriteExcel(
                worksheet,
                rows);

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Pending_Materials_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        private static Dictionary<
            string,
            string[]>
            ParsePendingMaterialExcelFilters(
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
                        PendingMaterialExcelFilterColumns
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
            MatchesPendingMaterialExcelFilters(
                PendingMaterialRowViewModel row,
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
                    GetPendingMaterialExcelCellValue(
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
            GetPendingMaterialExcelCellValue(
                PendingMaterialRowViewModel row,
                string column)
        {
            var normalized =
                column
                    .Trim()
                    .ToLowerInvariant();

            return normalized switch
            {
                "due" =>
                    row.DueStatus,

                "po" =>
                    string.Join(
                        " | ",
                        new[]
                        {
                            row.PONumber,
                            string.IsNullOrWhiteSpace(
                                row.Handler)
                                ? "Unassigned"
                                : row.Handler
                        }),

                "material" =>
                    string.Join(
                        " | ",
                        new[]
                        {
                            row.MaterialNo,
                            "Line " +
                            (
                                string.IsNullOrWhiteSpace(
                                    row.LineItem)
                                    ? "—"
                                    : row.LineItem
                            )
                        }),

                "description" =>
                    GetPendingDescriptionExcelValue(
                        row),

                "poqty" =>
                    row.POQuantity
                        .ToString(
                            "0.###",
                            CultureInfo.InvariantCulture),

                "invoice" =>
                    row.InvoiceQuantity
                        .ToString(
                            "0.###",
                            CultureInfo.InvariantCulture),

                "invoicebalance" =>
                    row.InvoiceBalanceQuantity
                        .ToString(
                            "0.###",
                            CultureInfo.InvariantCulture),

                "dispatch" =>
                    row.DispatchQuantity
                        .ToString(
                            "0.###",
                            CultureInfo.InvariantCulture),

                "pendingdispatch" =>
                    row.PendingDispatchQuantity
                        .ToString(
                            "0.###",
                            CultureInfo.InvariantCulture),

                "progress" =>
                    row.CompletionPercentage
                        .ToString(
                            "0",
                            CultureInfo.InvariantCulture) +
                    "%",

                "process" =>
                    row.ProcessStatus,

                "invoicestatus" =>
                    row.InvoiceStatus,

                "dispatchstatus" =>
                    row.DispatchStatus,

                "delivery" =>
                    row.DeliveryDate
                        ?.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)
                    ?? string.Empty,

                "assigned" =>
                    string.IsNullOrWhiteSpace(
                        row.AssignedDashboard)
                        ? "Unassigned"
                        : row.AssignedDashboard,

                _ =>
                    string.Empty
            };
        }

        private static string
            GetPendingDescriptionExcelValue(
                PendingMaterialRowViewModel row)
        {
            var description =
                !string.IsNullOrWhiteSpace(
                    row.ShortDescription)
                    ? row.ShortDescription
                    : row.BriefDescription ??
                      string.Empty;

            if (
                string.IsNullOrWhiteSpace(
                    row.DrawingNo)
            )
            {
                return description;
            }

            return string.IsNullOrWhiteSpace(
                    description)
                ? "Drawing: " +
                  row.DrawingNo
                : description +
                  " | Drawing: " +
                  row.DrawingNo;
        }

        private static IOrderedEnumerable<
            PendingMaterialRowViewModel>
            ApplyPendingMaterialSortInMemory(
                IEnumerable<
                    PendingMaterialRowViewModel> rows,
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
                var today =
                    DateTime.Today;

                return rows
                    .OrderBy(row =>
                        !row.DeliveryDate.HasValue
                            ? 2
                            : row.DeliveryDate.Value.Date <
                              today
                                ? 0
                                : 1)
                    .ThenBy(row =>
                        row.DeliveryDate)
                    .ThenByDescending(row =>
                        row.MaterialId);
            }

            Func<
                PendingMaterialRowViewModel,
                object?> selector =
                column switch
                {
                    "due" =>
                        row =>
                            row.DueStatus,

                    "po" =>
                        row =>
                            row.PONumber,

                    "material" =>
                        row =>
                            row.MaterialNo,

                    "description" =>
                        row =>
                            !string.IsNullOrWhiteSpace(
                                row.ShortDescription)
                                ? row.ShortDescription
                                : row.BriefDescription,

                    "poqty" =>
                        row =>
                            row.POQuantity,

                    "invoice" =>
                        row =>
                            row.InvoiceQuantity,

                    "invoicebalance" =>
                        row =>
                            row.InvoiceBalanceQuantity,

                    "dispatch" =>
                        row =>
                            row.DispatchQuantity,

                    "pendingdispatch" =>
                        row =>
                            row.PendingDispatchQuantity,

                    "progress" =>
                        row =>
                            row.CompletionPercentage,

                    "process" =>
                        row =>
                            row.ProcessStatus,

                    "invoicestatus" =>
                        row =>
                            row.InvoiceStatus,

                    "dispatchstatus" =>
                        row =>
                            row.DispatchStatus,

                    "delivery" =>
                        row =>
                            row.DeliveryDate,

                    "assigned" =>
                        row =>
                            row.AssignedDashboard,

                    _ =>
                        row =>
                            row.MaterialId
                };

            return descending
                ? rows
                    .OrderByDescending(
                        selector)
                    .ThenByDescending(
                        row =>
                            row.MaterialId)
                : rows
                    .OrderBy(
                        selector)
                    .ThenBy(
                        row =>
                            row.MaterialId);
        }

        private static IQueryable<POTracker.Models.Material>
            ApplyHeaderFilters(
                IQueryable<POTracker.Models.Material> query,
                string? dueStatus,
                string? columnPoNumber,
                string? columnHandler,
                string? columnMaterialNo,
                string? columnLineItem,
                string? columnDescription,
                decimal? minPoQuantity,
                decimal? maxPoQuantity,
                decimal? minInvoiceQuantity,
                decimal? maxInvoiceQuantity,
                decimal? minInvoiceBalanceQuantity,
                decimal? maxInvoiceBalanceQuantity,
                decimal? minDispatchQuantity,
                decimal? maxDispatchQuantity,
                decimal? minPendingDispatchQuantity,
                decimal? maxPendingDispatchQuantity,
                decimal? minCompletionPercentage,
                decimal? maxCompletionPercentage,
                string? columnProcessStatus,
                string? columnInvoiceStatus,
                string? columnDispatchStatus,
                DateTime? columnDeliveryDateFrom,
                DateTime? columnDeliveryDateTo,
                string? columnAssignedDashboard)
        {
            var today =
                DateTime.Today;

            var weekEnd =
                today.AddDays(7);

            if (!string.IsNullOrWhiteSpace(dueStatus))
            {
                var value =
                    dueStatus.Trim();

                if (value == "No Delivery Date")
                {
                    query =
                        query.Where(x =>
                            !x.DeliveryDate.HasValue);
                }
                else if (value == "Overdue")
                {
                    query =
                        query.Where(x =>
                            x.DeliveryDate.HasValue &&
                            x.DeliveryDate.Value.Date < today &&
                            x.PendingQty > 0);
                }
                else if (value == "Due Today")
                {
                    query =
                        query.Where(x =>
                            x.DeliveryDate.HasValue &&
                            x.DeliveryDate.Value.Date == today &&
                            x.PendingQty > 0);
                }
                else if (value == "Due This Week")
                {
                    query =
                        query.Where(x =>
                            x.DeliveryDate.HasValue &&
                            x.DeliveryDate.Value.Date >= today &&
                            x.DeliveryDate.Value.Date <= weekEnd &&
                            x.PendingQty > 0);
                }
                else if (value == "Upcoming")
                {
                    query =
                        query.Where(x =>
                            x.DeliveryDate.HasValue &&
                            (
                                x.PendingQty <= 0 ||
                                x.DeliveryDate.Value.Date > weekEnd
                            ));
                }
            }

            if (!string.IsNullOrWhiteSpace(columnPoNumber))
            {
                var value =
                    columnPoNumber.Trim();

                query =
                    query.Where(x =>
                        x.PurchaseOrder != null &&
                        x.PurchaseOrder.PONumber.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(columnHandler))
            {
                var value =
                    columnHandler.Trim();

                query =
                    query.Where(x =>
                        x.PurchaseOrder != null &&
                        x.PurchaseOrder.BuyerContactPerson != null &&
                        x.PurchaseOrder.BuyerContactPerson.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(columnMaterialNo))
            {
                var value =
                    columnMaterialNo.Trim();

                query =
                    query.Where(x =>
                        x.MaterialNo != null &&
                        x.MaterialNo.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(columnLineItem))
            {
                var value =
                    columnLineItem.Trim();

                query =
                    query.Where(x =>
                        x.LineItem != null &&
                        x.LineItem.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(columnDescription))
            {
                var value =
                    columnDescription.Trim();

                query =
                    query.Where(x =>
                        (
                            x.ShortDescription != null &&
                            x.ShortDescription.Contains(value)
                        ) ||
                        (
                            x.BriefDescription != null &&
                            x.BriefDescription.Contains(value)
                        ) ||
                        (
                            x.DrawingNo != null &&
                            x.DrawingNo.Contains(value)
                        ));
            }

            if (minPoQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.Qty >= minPoQuantity.Value);
            }

            if (maxPoQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.Qty <= maxPoQuantity.Value);
            }

            if (minInvoiceQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.InvoiceQty >= minInvoiceQuantity.Value);
            }

            if (maxInvoiceQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.InvoiceQty <= maxInvoiceQuantity.Value);
            }

            if (minInvoiceBalanceQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        (
                            x.BalanceQty < 0
                                ? 0
                                : x.BalanceQty
                        ) >= minInvoiceBalanceQuantity.Value);
            }

            if (maxInvoiceBalanceQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        (
                            x.BalanceQty < 0
                                ? 0
                                : x.BalanceQty
                        ) <= maxInvoiceBalanceQuantity.Value);
            }

            if (minDispatchQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.DispatchQty >= minDispatchQuantity.Value);
            }

            if (maxDispatchQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        x.DispatchQty <= maxDispatchQuantity.Value);
            }

            if (minPendingDispatchQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        (
                            x.PendingQty < 0
                                ? 0
                                : x.PendingQty
                        ) >= minPendingDispatchQuantity.Value);
            }

            if (maxPendingDispatchQuantity.HasValue)
            {
                query =
                    query.Where(x =>
                        (
                            x.PendingQty < 0
                                ? 0
                                : x.PendingQty
                        ) <= maxPendingDispatchQuantity.Value);
            }

            if (minCompletionPercentage.HasValue)
            {
                var minimum =
                    Math.Clamp(
                        minCompletionPercentage.Value,
                        0m,
                        100m);

                query =
                    query.Where(x =>
                        (
                            x.Qty <= 0
                                ? 0m
                                : (
                                    (
                                        (
                                            x.InvoiceQty >
                                            x.DispatchQty
                                                ? x.InvoiceQty
                                                : x.DispatchQty
                                        ) /
                                        x.Qty *
                                        100m
                                    ) > 100m
                                        ? 100m
                                        : (
                                            (
                                                x.InvoiceQty >
                                                x.DispatchQty
                                                    ? x.InvoiceQty
                                                    : x.DispatchQty
                                            ) /
                                            x.Qty *
                                            100m
                                        )
                                )
                        ) >= minimum);
            }

            if (maxCompletionPercentage.HasValue)
            {
                var maximum =
                    Math.Clamp(
                        maxCompletionPercentage.Value,
                        0m,
                        100m);

                query =
                    query.Where(x =>
                        (
                            x.Qty <= 0
                                ? 0m
                                : (
                                    (
                                        (
                                            x.InvoiceQty >
                                            x.DispatchQty
                                                ? x.InvoiceQty
                                                : x.DispatchQty
                                        ) /
                                        x.Qty *
                                        100m
                                    ) > 100m
                                        ? 100m
                                        : (
                                            (
                                                x.InvoiceQty >
                                                x.DispatchQty
                                                    ? x.InvoiceQty
                                                    : x.DispatchQty
                                            ) /
                                            x.Qty *
                                            100m
                                        )
                                )
                        ) <= maximum);
            }

            if (!string.IsNullOrWhiteSpace(columnProcessStatus))
            {
                var value =
                    columnProcessStatus.Trim();

                query =
                    query.Where(x =>
                        x.ProcessStatus == value);
            }

            if (!string.IsNullOrWhiteSpace(columnInvoiceStatus))
            {
                var value =
                    columnInvoiceStatus.Trim();

                query =
                    query.Where(x =>
                        x.InvoiceStatus == value);
            }

            if (!string.IsNullOrWhiteSpace(columnDispatchStatus))
            {
                var value =
                    columnDispatchStatus.Trim();

                query =
                    query.Where(x =>
                        x.DispatchStatus == value);
            }

            if (columnDeliveryDateFrom.HasValue)
            {
                var date =
                    columnDeliveryDateFrom.Value.Date;

                query =
                    query.Where(x =>
                        x.DeliveryDate.HasValue &&
                        x.DeliveryDate.Value.Date >= date);
            }

            if (columnDeliveryDateTo.HasValue)
            {
                var date =
                    columnDeliveryDateTo.Value.Date;

                query =
                    query.Where(x =>
                        x.DeliveryDate.HasValue &&
                        x.DeliveryDate.Value.Date <= date);
            }

            if (!string.IsNullOrWhiteSpace(columnAssignedDashboard))
            {
                var value =
                    columnAssignedDashboard.Trim();

                query =
                    query.Where(x =>
                        x.AssignedDashboard == value);
            }

            return query;
        }

        private PendingMaterialItemDto
            MapPendingMaterialApiItem(
                PendingMaterialRowViewModel row)
        {
            return new PendingMaterialItemDto
            {
                MaterialId = row.MaterialId,
                PurchaseOrderId = row.PurchaseOrderId,
                PONumber = row.PONumber,
                Handler = row.Handler,
                PODate = row.PODate,
                POReceivedDate = row.POReceivedDate,
                LineItem = row.LineItem,
                MaterialNo = row.MaterialNo,
                HsnSacCode = row.HsnSacCode,
                ShortDescription = row.ShortDescription,
                BriefDescription = row.BriefDescription,
                Unit = row.Unit,
                Department = row.Department,
                DrawingNo = row.DrawingNo,
                DrawingVersion = row.DrawingVersion,
                POQuantity = row.POQuantity,
                InvoiceQuantity = row.InvoiceQuantity,
                InvoiceBalanceQuantity = row.InvoiceBalanceQuantity,
                DispatchQuantity = row.DispatchQuantity,
                PendingDispatchQuantity = row.PendingDispatchQuantity,
                StockQuantity = row.StockQuantity,
                UnitCost = row.UnitCost,
                TotalValue = row.TotalValue,
                DeliveryDate = row.DeliveryDate,
                ProcessStartDate = row.ProcessStartDate,
                AlertDate = row.AlertDate,
                ProcessStatus = row.ProcessStatus,
                InvoiceStatus = row.InvoiceStatus,
                DispatchStatus = row.DispatchStatus,
                AssignedDashboard = row.AssignedDashboard,
                Remarks = row.Remarks,
                PdfFilePath =
                    _documentReferences.ToDownloadUrl(
                        row.PdfFilePath,
                        "pdfs"),
                DrawingFilePath =
                    _documentReferences.ToDownloadUrl(
                        row.DrawingFilePath,
                        "drawings"),
                CompletionPercentage = row.CompletionPercentage,
                OverdueDays = row.OverdueDays,
                IsOverdue = row.IsOverdue,
                IsDueToday = row.IsDueToday,
                IsDueThisWeek = row.IsDueThisWeek,
                DueStatus = row.DueStatus
            };
        }

        private static List<PendingMaterialFilterOptionDto>
            MapFilterOptions(
                IEnumerable<
                    PendingMaterialFilterOptionViewModel
                > options)
        {
            return options
                .Select(option =>
                    new PendingMaterialFilterOptionDto
                    {
                        Value = option.Value,
                        Text = option.Text,
                        Count = option.Count
                    })
                .ToList();
        }
    }
}
