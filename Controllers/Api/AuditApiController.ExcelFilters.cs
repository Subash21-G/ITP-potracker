using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    public sealed partial class AuditApiController
    {
        private static readonly HashSet<string>
            AuditExcelColumns =
                new(
                    new[]
                    {
                        "date",
                        "material",
                        "po",
                        "action",
                        "process",
                        "dispatch",
                        "owner"
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
            ExcelFilterValues(
                string column,
                string? search,
                string? owner,
                string? processStatus,
                string? dispatchStatus,
                DateTime? dateFrom,
                DateTime? dateTo,
                string? materialNo,
                string? poNumber,
                string? action,
                string? columnProcessStatus,
                string? columnDispatchStatus,
                string? columnOwner,
                DateTime? createdDateFrom,
                DateTime? createdDateTo,
                string? excelFilters,
                CancellationToken cancellationToken =
                    default)
        {
            var normalizedColumn =
                NormalizeAuditExcelColumn(
                    column);

            if (
                !AuditExcelColumns.Contains(
                    normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        message =
                            "Unknown audit Excel-filter column."
                    });
            }

            var selections =
                ParseAuditExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var remainingFilters =
                selections.Count == 0
                    ? null
                    : JsonSerializer.Serialize(
                        selections);

            async Task<AuditPagedResponseDto?>
                LoadPageAsync(
                    int pageNumber)
            {
                var result =
                    await Index(
                        search:
                            search,

                        owner:
                            owner,

                        processStatus:
                            processStatus,

                        dispatchStatus:
                            dispatchStatus,

                        dateFrom:
                            dateFrom,

                        dateTo:
                            dateTo,

                        materialNo:
                            materialNo,

                        poNumber:
                            poNumber,

                        action:
                            action,

                        columnProcessStatus:
                            columnProcessStatus,

                        columnDispatchStatus:
                            columnDispatchStatus,

                        columnOwner:
                            columnOwner,

                        createdDateFrom:
                            createdDateFrom,

                        createdDateTo:
                            createdDateTo,

                        sortColumn:
                            null,

                        sortDirection:
                            null,

                        excelFilters:
                            remainingFilters,

                        page:
                            pageNumber,

                        pageSize:
                            100,

                        cancellationToken:
                            cancellationToken);

                return UnwrapAuditPage(
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
                        message =
                            "Audit Excel-filter values could not be loaded."
                    });
            }

            var items =
                new List<AuditItemDto>(
                    firstPage.Items);

            for (
                var pageNumber = 2;
                pageNumber <=
                    firstPage.TotalPages;
                pageNumber++)
            {
                var page =
                    await LoadPageAsync(
                        pageNumber);

                if (page == null)
                {
                    return BadRequest(
                        new
                        {
                            message =
                                "Audit Excel-filter values could not be fully loaded."
                        });
                }

                items.AddRange(
                    page.Items);
            }

            var values =
                items
                    .Select(
                        item =>
                            new
                            {
                                Value =
                                    GetAuditExcelValue(
                                        item,
                                        normalizedColumn),

                                Label =
                                    GetAuditExcelLabel(
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
                                        .Select(item =>
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

            return Ok(values);
        }

        private static AuditPagedResponseDto?
            UnwrapAuditPage(
                ActionResult<
                    AuditPagedResponseDto> result)
        {
            if (result.Value != null)
            {
                return result.Value;
            }

            return result.Result is
                    OkObjectResult ok &&
                ok.Value is
                    AuditPagedResponseDto response
                ? response
                : null;
        }

        private static IQueryable<Material>
            ApplyAuditExcelFilters(
                IQueryable<Material> query,
                string? excelFilters)
        {
            var selections =
                ParseAuditExcelFilters(
                    excelFilters);

            foreach (
                var pair in selections)
            {
                var selected =
                    pair.Value ??
                    Array.Empty<string>();

                if (selected.Length == 0)
                {
                    return query.Where(
                        _ => false);
                }

                switch (pair.Key)
                {
                    case "date":
                    {
                        var dates =
                            selected
                                .Select(
                                    value =>
                                        DateTime.TryParse(
                                            value,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.RoundtripKind,
                                            out var parsed)
                                            ? parsed
                                            : (DateTime?)null)
                                .Where(
                                    value =>
                                        value.HasValue)
                                .Select(
                                    value =>
                                        value!.Value)
                                .ToArray();

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
                                material =>
                                    dates.Contains(
                                        material.CreatedDate));

                        break;
                    }

                    case "material":
                        query =
                            query.Where(
                                material =>
                                    selected.Contains(
                                        material.MaterialNo ??
                                        string.Empty));
                        break;

                    case "po":
                        query =
                            query.Where(
                                material =>
                                    selected.Contains(
                                        material.PurchaseOrder != null
                                            ? material.PurchaseOrder.PONumber
                                            : string.Empty));
                        break;

                    case "action":
                        if (
                            !selected.Contains(
                                AuditAction,
                                StringComparer.OrdinalIgnoreCase)
                        )
                        {
                            return query.Where(
                                _ => false);
                        }
                        break;

                    case "process":
                        query =
                            query.Where(
                                material =>
                                    selected.Contains(
                                        material.ProcessStatus ??
                                        string.Empty));
                        break;

                    case "dispatch":
                        query =
                            query.Where(
                                material =>
                                    selected.Contains(
                                        material.DispatchStatus ??
                                        string.Empty));
                        break;

                    case "owner":
                        query =
                            query.Where(
                                material =>
                                    selected.Contains(
                                        material.AssignedDashboard ??
                                        string.Empty));
                        break;
                }
            }

            return query;
        }

        private static IQueryable<Material>
            ApplyAuditSort(
                IQueryable<Material> query,
                string? sortColumn,
                string? sortDirection)
        {
            var column =
                NormalizeAuditExcelColumn(
                    sortColumn);

            var descending =
                string.Equals(
                    sortDirection?.Trim(),
                    "desc",
                    StringComparison.OrdinalIgnoreCase);

            if (
                !AuditExcelColumns.Contains(
                    column)
            )
            {
                return query
                    .OrderByDescending(
                        material =>
                            material.CreatedDate)
                    .ThenByDescending(
                        material =>
                            material.Id);
            }

            return column switch
            {
                "date" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.CreatedDate)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "date" =>
                    query
                        .OrderBy(
                            material =>
                                material.CreatedDate)
                        .ThenBy(
                            material =>
                                material.Id),

                "material" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.MaterialNo)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "material" =>
                    query
                        .OrderBy(
                            material =>
                                material.MaterialNo)
                        .ThenBy(
                            material =>
                                material.Id),

                "po" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.PurchaseOrder != null
                                    ? material.PurchaseOrder.PONumber
                                    : string.Empty)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "po" =>
                    query
                        .OrderBy(
                            material =>
                                material.PurchaseOrder != null
                                    ? material.PurchaseOrder.PONumber
                                    : string.Empty)
                        .ThenBy(
                            material =>
                                material.Id),

                "action" when descending =>
                    query
                        .OrderByDescending(
                            _ =>
                                AuditAction)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "action" =>
                    query
                        .OrderBy(
                            _ =>
                                AuditAction)
                        .ThenBy(
                            material =>
                                material.Id),

                "process" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.ProcessStatus)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "process" =>
                    query
                        .OrderBy(
                            material =>
                                material.ProcessStatus)
                        .ThenBy(
                            material =>
                                material.Id),

                "dispatch" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.DispatchStatus)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "dispatch" =>
                    query
                        .OrderBy(
                            material =>
                                material.DispatchStatus)
                        .ThenBy(
                            material =>
                                material.Id),

                "owner" when descending =>
                    query
                        .OrderByDescending(
                            material =>
                                material.AssignedDashboard)
                        .ThenByDescending(
                            material =>
                                material.Id),

                "owner" =>
                    query
                        .OrderBy(
                            material =>
                                material.AssignedDashboard)
                        .ThenBy(
                            material =>
                                material.Id),

                _ =>
                    query
                        .OrderByDescending(
                            material =>
                                material.CreatedDate)
                        .ThenByDescending(
                            material =>
                                material.Id)
            };
        }

        private static Dictionary<
                string,
                string[]>
            ParseAuditExcelFilters(
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

                if (parsed == null)
                {
                    return result;
                }

                foreach (
                    var pair in parsed)
                {
                    var column =
                        NormalizeAuditExcelColumn(
                            pair.Key);

                    if (
                        !AuditExcelColumns.Contains(
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
                // Invalid client JSON behaves as no Excel filter.
            }

            return result;
        }

        private static string
            GetAuditExcelValue(
                AuditItemDto item,
                string column)
        {
            return column switch
            {
                "date" =>
                    item.CreatedDate.ToString(
                        "O",
                        CultureInfo.InvariantCulture),

                "material" =>
                    item.MaterialNo ??
                    string.Empty,

                "po" =>
                    item.PONumber ??
                    string.Empty,

                "action" =>
                    item.Action ??
                    string.Empty,

                "process" =>
                    item.ProcessStatus ??
                    string.Empty,

                "dispatch" =>
                    item.DispatchStatus ??
                    string.Empty,

                "owner" =>
                    item.Owner ??
                    string.Empty,

                _ =>
                    string.Empty
            };
        }

        private static string
            GetAuditExcelLabel(
                AuditItemDto item,
                string column)
        {
            if (
                string.Equals(
                    column,
                    "date",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                return item.CreatedDate.ToString(
                    "dd MMM yyyy, hh:mm tt",
                    CultureInfo.InvariantCulture);
            }

            var value =
                GetAuditExcelValue(
                    item,
                    column);

            return string.IsNullOrEmpty(
                value)
                    ? "(Blank)"
                    : value;
        }

        private static string
            NormalizeAuditExcelColumn(
                string? column)
        {
            return column?
                .Trim()
                .ToLowerInvariant() ??
                string.Empty;
        }
    }
}
