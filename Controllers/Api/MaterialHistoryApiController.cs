using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/material-history")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class MaterialHistoryApiController : ControllerBase
    {
        private const int DefaultPageSize = 25;

        private static readonly int[] SupportedPageSizes =
        {
            10,
            25,
            50,
            100
        };

        private static readonly HashSet<string> ExcelFilterColumns = new(
            new[]
            {
                "date",
                "material",
                "poline",
                "result",
                "remark"
            },
            StringComparer.OrdinalIgnoreCase);

        private readonly ApplicationDbContext _context;

        public MaterialHistoryApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(MaterialHistoryListResponseDto),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<MaterialHistoryListResponseDto>> Index(
            string? search,
            string? outcome,
            int? materialId,
            string? sortColumn = null,
            string? sortDirection = null,
            string? excelFilters = null,
            int page = 1,
            int pageSize = DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = SupportedPageSizes.Contains(pageSize)
                ? pageSize
                : DefaultPageSize;

            var normalizedOutcome = string.IsNullOrWhiteSpace(outcome)
                ? null
                : MaterialHistoryOutcomes.Normalize(outcome);

            var baseQuery = BuildBaseQuery(search, materialId);

            var summary = new MaterialHistorySummaryDto
            {
                TotalEntries = await baseQuery.CountAsync(cancellationToken),
                PerfectDeliveries = await baseQuery.CountAsync(
                    x => x.Outcome == MaterialHistoryOutcomes.PerfectDelivery,
                    cancellationToken),
                ReworkDeliveries = await baseQuery.CountAsync(
                    x => x.Outcome == MaterialHistoryOutcomes.ReworkAndDelivery,
                    cancellationToken),
                Rejected = await baseQuery.CountAsync(
                    x => x.Outcome == MaterialHistoryOutcomes.Rejected,
                    cancellationToken)
            };

            var listQuery = normalizedOutcome == null
                ? baseQuery
                : baseQuery.Where(x => x.Outcome == normalizedOutcome);

            listQuery = ApplyExcelFilters(
                listQuery,
                ParseExcelFilters(excelFilters));

            var totalRecords = await listQuery.CountAsync(cancellationToken);
            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(totalRecords / (double)pageSize));

            page = Math.Min(page, totalPages);

            var items = await ApplySort(listQuery, sortColumn, sortDirection)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new MaterialHistoryItemDto
                {
                    Id = x.Id,
                    MaterialId = x.MaterialId,
                    MaterialNo = x.Material!.MaterialNo ?? string.Empty,
                    LineItem = x.Material.LineItem,
                    PONumber = x.Material.PurchaseOrder!.PONumber,
                    Description = x.Material.ShortDescription ?? x.Material.BriefDescription,
                    Outcome = x.Outcome,
                    Remark = x.Remark,
                    OccurredDate = x.OccurredDate,
                    CreatedDate = x.CreatedDate,
                    UpdatedDate = x.UpdatedDate
                })
                .ToListAsync(cancellationToken);

            return Ok(new MaterialHistoryListResponseDto
            {
                Items = items,
                Summary = summary,
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages
            });
        }

        [HttpGet("excel-filter-values")]
        [ProducesResponseType(
            typeof(IReadOnlyList<MaterialHistoryExcelFilterValueDto>),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IReadOnlyList<MaterialHistoryExcelFilterValueDto>>>
            GetExcelFilterValues(
                string column,
                string? search,
                string? outcome,
                int? materialId,
                string? excelFilters,
                CancellationToken cancellationToken = default)
        {
            var normalizedColumn = NormalizeColumn(column);

            if (!ExcelFilterColumns.Contains(normalizedColumn))
            {
                return BadRequest(new
                {
                    error = "Unsupported Material History Excel filter column."
                });
            }

            var normalizedOutcome = string.IsNullOrWhiteSpace(outcome)
                ? null
                : MaterialHistoryOutcomes.Normalize(outcome);

            var query = BuildBaseQuery(search, materialId);

            if (normalizedOutcome != null)
            {
                query = query.Where(x => x.Outcome == normalizedOutcome);
            }

            var selections = ParseExcelFilters(excelFilters);

            selections.Remove(normalizedColumn);
            query = ApplyExcelFilters(query, selections);

            var rows = await query
                .Select(x => new MaterialHistoryFilterRow
                {
                    MaterialId = x.MaterialId,
                    MaterialNo = x.Material!.MaterialNo,
                    LineItem = x.Material.LineItem,
                    PONumber = x.Material.PurchaseOrder!.PONumber,
                    Description = x.Material.ShortDescription ?? x.Material.BriefDescription,
                    Outcome = x.Outcome,
                    Remark = x.Remark,
                    OccurredDate = x.OccurredDate
                })
                .ToListAsync(cancellationToken);

            var values = rows
                .Select(row => GetExcelFilterCell(row, normalizedColumn))
                .GroupBy(cell => cell.Value, StringComparer.OrdinalIgnoreCase)
                .Select(group => new MaterialHistoryExcelFilterValueDto
                {
                    Value = group.Key,
                    Label = group.First().Label,
                    Count = group.Count()
                })
                .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(values);
        }

        [HttpGet("material-options")]
        [ProducesResponseType(
            typeof(IReadOnlyList<MaterialHistoryMaterialOptionDto>),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<MaterialHistoryMaterialOptionDto>>>
            GetMaterialOptions(CancellationToken cancellationToken = default)
        {
            var materials = await _context.Materials
                .AsNoTracking()
                .OrderBy(x => x.MaterialNo)
                .ThenBy(x => x.PurchaseOrder!.PONumber)
                .ThenBy(x => x.LineItem)
                .Select(x => new MaterialHistoryMaterialOptionDto
                {
                    Id = x.Id,
                    MaterialNo = x.MaterialNo ?? string.Empty,
                    LineItem = x.LineItem,
                    PONumber = x.PurchaseOrder!.PONumber,
                    Description = x.ShortDescription ?? x.BriefDescription
                })
                .ToListAsync(cancellationToken);

            foreach (var material in materials)
            {
                var materialNo = string.IsNullOrWhiteSpace(material.MaterialNo)
                    ? "(No material number)"
                    : material.MaterialNo;
                var line = string.IsNullOrWhiteSpace(material.LineItem)
                    ? string.Empty
                    : $" · Line {material.LineItem}";

                material.DisplayLabel = $"{materialNo} · PO {material.PONumber}{line}";
            }

            return Ok(materials);
        }

        [HttpPost]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(MaterialHistorySaveResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MaterialHistorySaveResponseDto>> Create(
            [FromBody] MaterialHistoryUpsertRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            var validationError = ValidateRequest(request, out var normalizedOutcome);

            if (validationError != null)
            {
                return BadRequest(new { error = validationError });
            }

            if (!await _context.Materials.AnyAsync(
                    x => x.Id == request!.MaterialId,
                    cancellationToken))
            {
                return NotFound(new { error = "Material was not found." });
            }

            var history = new MaterialHistory
            {
                MaterialId = request!.MaterialId,
                Outcome = normalizedOutcome!,
                Remark = NormalizeOptional(request.Remark),
                OccurredDate = (request.OccurredDate ?? DateTime.Today).Date,
                CreatedDate = DateTime.Now
            };

            _context.MaterialHistories.Add(history);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new MaterialHistorySaveResponseDto
            {
                Success = true,
                Message = "Material history added successfully.",
                History = await LoadItem(history.Id, cancellationToken)
            });
        }

        [HttpPut("{id:int}")]
        [POTracker.Security.ValidateApiAntiforgeryToken]
        [ProducesResponseType(
            typeof(MaterialHistorySaveResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MaterialHistorySaveResponseDto>> Update(
            int id,
            [FromBody] MaterialHistoryUpsertRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            var validationError = ValidateRequest(request, out var normalizedOutcome);

            if (validationError != null)
            {
                return BadRequest(new { error = validationError });
            }

            var history = await _context.MaterialHistories
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (history == null)
            {
                return NotFound(new { error = "Material history entry was not found." });
            }

            if (!await _context.Materials.AnyAsync(
                    x => x.Id == request!.MaterialId,
                    cancellationToken))
            {
                return NotFound(new { error = "Material was not found." });
            }

            history.MaterialId = request!.MaterialId;
            history.Outcome = normalizedOutcome!;
            history.Remark = NormalizeOptional(request.Remark);
            history.OccurredDate = (request.OccurredDate ?? DateTime.Today).Date;
            history.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new MaterialHistorySaveResponseDto
            {
                Success = true,
                Message = "Material history updated successfully.",
                History = await LoadItem(history.Id, cancellationToken)
            });
        }

        private static string? ValidateRequest(
            MaterialHistoryUpsertRequestDto? request,
            out string? normalizedOutcome)
        {
            normalizedOutcome = MaterialHistoryOutcomes.Normalize(request?.Outcome);

            if (request == null || request.MaterialId <= 0)
            {
                return "Material is required.";
            }

            if (normalizedOutcome == null)
            {
                return "Select Perfect delivery, Rework and delivery, or Rejected.";
            }

            if (normalizedOutcome == MaterialHistoryOutcomes.Rejected &&
                string.IsNullOrWhiteSpace(request.Remark))
            {
                return "Rejection reason is required for rejected materials.";
            }

            return null;
        }

        private async Task<MaterialHistoryItemDto> LoadItem(
            int id,
            CancellationToken cancellationToken)
        {
            return await _context.MaterialHistories
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new MaterialHistoryItemDto
                {
                    Id = x.Id,
                    MaterialId = x.MaterialId,
                    MaterialNo = x.Material!.MaterialNo ?? string.Empty,
                    LineItem = x.Material.LineItem,
                    PONumber = x.Material.PurchaseOrder!.PONumber,
                    Description = x.Material.ShortDescription ?? x.Material.BriefDescription,
                    Outcome = x.Outcome,
                    Remark = x.Remark,
                    OccurredDate = x.OccurredDate,
                    CreatedDate = x.CreatedDate,
                    UpdatedDate = x.UpdatedDate
                })
                .SingleAsync(cancellationToken);
        }

        private IQueryable<MaterialHistory> BuildBaseQuery(
            string? search,
            int? materialId)
        {
            var query = _context.MaterialHistories.AsNoTracking();

            if (materialId.HasValue && materialId.Value > 0)
            {
                query = query.Where(x => x.MaterialId == materialId.Value);
            }

            if (string.IsNullOrWhiteSpace(search))
            {
                return query;
            }

            var term = search.Trim();

            return query.Where(x =>
                (x.Material!.MaterialNo != null && x.Material.MaterialNo.Contains(term)) ||
                (x.Material.LineItem != null && x.Material.LineItem.Contains(term)) ||
                (x.Material.ShortDescription != null && x.Material.ShortDescription.Contains(term)) ||
                (x.Material.BriefDescription != null && x.Material.BriefDescription.Contains(term)) ||
                x.Material.PurchaseOrder!.PONumber.Contains(term) ||
                (x.Remark != null && x.Remark.Contains(term)));
        }

        private static IQueryable<MaterialHistory> ApplyExcelFilters(
            IQueryable<MaterialHistory> query,
            IReadOnlyDictionary<string, HashSet<string>> selections)
        {
            foreach (var selection in selections)
            {
                var values = selection.Value;

                if (values.Count == 0)
                {
                    query = query.Where(_ => false);
                    continue;
                }

                switch (selection.Key)
                {
                    case "date":
                        var dates = values
                            .Select(value => DateTime.TryParseExact(
                                value,
                                "yyyy-MM-dd",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out var parsed)
                                    ? parsed.Date
                                    : (DateTime?)null)
                            .Where(value => value.HasValue)
                            .Select(value => value!.Value)
                            .ToArray();

                        query = dates.Length == 0
                            ? query.Where(_ => false)
                            : query.Where(x => dates.Contains(x.OccurredDate.Date));
                        break;

                    case "material":
                        var materialIds = values
                            .Select(value => int.TryParse(value, out var parsed)
                                ? parsed
                                : (int?)null)
                            .Where(value => value.HasValue)
                            .Select(value => value!.Value)
                            .ToArray();

                        query = materialIds.Length == 0
                            ? query.Where(_ => false)
                            : query.Where(x => materialIds.Contains(x.MaterialId));
                        break;

                    case "poline":
                        query = query.Where(x => values.Contains(
                            x.Material!.PurchaseOrder!.PONumber +
                            "\u001f" +
                            (x.Material.LineItem ?? string.Empty)));
                        break;

                    case "result":
                        query = query.Where(x => values.Contains(x.Outcome));
                        break;

                    case "remark":
                        query = query.Where(x => values.Contains(x.Remark ?? string.Empty));
                        break;
                }
            }

            return query;
        }

        private static IOrderedQueryable<MaterialHistory> ApplySort(
            IQueryable<MaterialHistory> query,
            string? sortColumn,
            string? sortDirection)
        {
            var column = NormalizeColumn(sortColumn);
            var descending = string.Equals(
                sortDirection,
                "desc",
                StringComparison.OrdinalIgnoreCase);

            return (column, descending) switch
            {
                ("date", false) => query
                    .OrderBy(x => x.OccurredDate)
                    .ThenBy(x => x.Id),
                ("date", true) => query
                    .OrderByDescending(x => x.OccurredDate)
                    .ThenByDescending(x => x.Id),
                ("material", false) => query
                    .OrderBy(x => x.Material!.MaterialNo)
                    .ThenBy(x => x.MaterialId)
                    .ThenBy(x => x.Id),
                ("material", true) => query
                    .OrderByDescending(x => x.Material!.MaterialNo)
                    .ThenByDescending(x => x.MaterialId)
                    .ThenByDescending(x => x.Id),
                ("poline", false) => query
                    .OrderBy(x => x.Material!.PurchaseOrder!.PONumber)
                    .ThenBy(x => x.Material!.LineItem)
                    .ThenBy(x => x.Id),
                ("poline", true) => query
                    .OrderByDescending(x => x.Material!.PurchaseOrder!.PONumber)
                    .ThenByDescending(x => x.Material!.LineItem)
                    .ThenByDescending(x => x.Id),
                ("result", false) => query
                    .OrderBy(x => x.Outcome)
                    .ThenBy(x => x.Id),
                ("result", true) => query
                    .OrderByDescending(x => x.Outcome)
                    .ThenByDescending(x => x.Id),
                ("remark", false) => query
                    .OrderBy(x => x.Remark)
                    .ThenBy(x => x.Id),
                ("remark", true) => query
                    .OrderByDescending(x => x.Remark)
                    .ThenByDescending(x => x.Id),
                _ => query
                    .OrderByDescending(x => x.OccurredDate)
                    .ThenByDescending(x => x.Id)
            };
        }

        private static Dictionary<string, HashSet<string>> ParseExcelFilters(
            string? excelFilters)
        {
            var selections = new Dictionary<string, HashSet<string>>(
                StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(excelFilters))
            {
                return selections;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
                    excelFilters);

                if (parsed == null)
                {
                    return selections;
                }

                foreach (var selection in parsed)
                {
                    var column = NormalizeColumn(selection.Key);

                    if (!ExcelFilterColumns.Contains(column))
                    {
                        continue;
                    }

                    selections[column] = new HashSet<string>(
                        selection.Value ?? Array.Empty<string>(),
                        StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (JsonException)
            {
                // Ignore malformed optional filter state and return the unfiltered result.
            }

            return selections;
        }

        private static string NormalizeColumn(string? column)
        {
            return string.Concat(
                (column ?? string.Empty)
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant));
        }

        private static ExcelFilterCell GetExcelFilterCell(
            MaterialHistoryFilterRow row,
            string column)
        {
            return column switch
            {
                "date" => new ExcelFilterCell(
                    row.OccurredDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    row.OccurredDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)),
                "material" => new ExcelFilterCell(
                    row.MaterialId.ToString(CultureInfo.InvariantCulture),
                    MaterialLabel(row.MaterialNo, row.Description)),
                "poline" => new ExcelFilterCell(
                    row.PONumber + "\u001f" + (row.LineItem ?? string.Empty),
                    PoLineLabel(row.PONumber, row.LineItem)),
                "result" => new ExcelFilterCell(
                    row.Outcome,
                    OutcomeLabel(row.Outcome)),
                "remark" => new ExcelFilterCell(
                    row.Remark ?? string.Empty,
                    string.IsNullOrWhiteSpace(row.Remark) ? "(Blank)" : row.Remark),
                _ => new ExcelFilterCell(string.Empty, "(Blank)")
            };
        }

        private static string MaterialLabel(string? materialNo, string? description)
        {
            var number = string.IsNullOrWhiteSpace(materialNo)
                ? "(No material number)"
                : materialNo.Trim();

            return string.IsNullOrWhiteSpace(description)
                ? number
                : $"{number} - {description.Trim()}";
        }

        private static string PoLineLabel(string poNumber, string? lineItem)
        {
            return string.IsNullOrWhiteSpace(lineItem)
                ? $"{poNumber} / no line"
                : $"{poNumber} / line {lineItem.Trim()}";
        }

        private static string OutcomeLabel(string outcome)
        {
            return outcome switch
            {
                MaterialHistoryOutcomes.PerfectDelivery => "Perfect delivery",
                MaterialHistoryOutcomes.ReworkAndDelivery => "Rework and delivery",
                MaterialHistoryOutcomes.Rejected => "Rejected",
                _ => outcome
            };
        }

        private sealed class MaterialHistoryFilterRow
        {
            public int MaterialId { get; init; }

            public string? MaterialNo { get; init; }

            public string? LineItem { get; init; }

            public string PONumber { get; init; } = string.Empty;

            public string? Description { get; init; }

            public string Outcome { get; init; } = string.Empty;

            public string? Remark { get; init; }

            public DateTime OccurredDate { get; init; }
        }

        private sealed record ExcelFilterCell(string Value, string Label);

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
