using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/admin/users-excel")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class AdminUsersExcelApiController :
        ControllerBase
    {
        private static readonly string[] AllowedRoles =
        {
            "Viewer",
            "Production",
            "Admin"
        };

        private static readonly HashSet<string>
            ExcelColumns =
                new(
                    new[]
                    {
                        "fullname",
                        "username",
                        "role"
                    },
                    StringComparer.OrdinalIgnoreCase);

        private readonly ApplicationDbContext _context;

        public AdminUsersExcelApiController(
            ApplicationDbContext context)
        {
            _context =
                context;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(AdminUsersResponseDto),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<AdminUsersResponseDto>>
            GetUsers(
                string? sortColumn,
                string? sortDirection,
                string? excelFilters,
                CancellationToken cancellationToken =
                    default)
        {
            var currentUserName =
                HttpContext.Session.GetString(
                    "UserName") ??
                string.Empty;

            var query =
                ApplyExcelFilters(
                    _context.AppUsers
                        .AsNoTracking(),
                    excelFilters);

            query =
                ApplySort(
                    query,
                    sortColumn,
                    sortDirection);

            var users =
                await query
                    .ToListAsync(
                        cancellationToken);

            return Ok(
                BuildResponse(
                    users,
                    currentUserName));
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
            ExcelFilterValues(
                string column,
                string? excelFilters,
                CancellationToken cancellationToken =
                    default)
        {
            var normalizedColumn =
                NormalizeColumn(
                    column);

            if (
                !ExcelColumns.Contains(
                    normalizedColumn)
            )
            {
                return BadRequest(
                    new
                    {
                        message =
                            "Unknown Admin User Excel-filter column."
                    });
            }

            var selections =
                ParseExcelFilters(
                    excelFilters);

            selections.Remove(
                normalizedColumn);

            var remainingFilters =
                selections.Count == 0
                    ? null
                    : JsonSerializer.Serialize(
                        selections);

            var users =
                await ApplyExcelFilters(
                        _context.AppUsers
                            .AsNoTracking(),
                        remainingFilters)
                    .ToListAsync(
                        cancellationToken);

            var values =
                users
                    .Select(
                        user =>
                            new
                            {
                                Value =
                                    GetExcelValue(
                                        user,
                                        normalizedColumn),

                                Label =
                                    GetExcelLabel(
                                        user,
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
                                        .Select(
                                            item =>
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

            return Ok(
                values);
        }

        private static AdminUsersResponseDto
            BuildResponse(
                IReadOnlyCollection<AppUser> users,
                string currentUserName)
        {
            return new AdminUsersResponseDto
            {
                Users =
                    users
                        .Select(
                            user =>
                                Map(
                                    user,
                                    currentUserName))
                        .ToList(),

                Summary =
                    new AdminUserSummaryDto
                    {
                        TotalUsers =
                            users.Count,

                        Administrators =
                            users.Count(
                                user =>
                                    user.Role.Equals(
                                        "Admin",
                                        StringComparison.OrdinalIgnoreCase)),

                        ProductionUsers =
                            users.Count(
                                user =>
                                    user.Role.Equals(
                                        "Production",
                                        StringComparison.OrdinalIgnoreCase)),

                        Viewers =
                            users.Count(
                                user =>
                                    user.Role.Equals(
                                        "Viewer",
                                        StringComparison.OrdinalIgnoreCase))
                    },

                AllowedRoles =
                    AllowedRoles.ToList()
            };
        }

        private static IQueryable<AppUser>
            ApplyExcelFilters(
                IQueryable<AppUser> query,
                string? excelFilters)
        {
            var selections =
                ParseExcelFilters(
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
                    case "fullname":
                        query =
                            query.Where(
                                user =>
                                    selected.Contains(
                                        user.FullName));
                        break;

                    case "username":
                        query =
                            query.Where(
                                user =>
                                    selected.Contains(
                                        user.UserName));
                        break;

                    case "role":
                        query =
                            query.Where(
                                user =>
                                    selected.Contains(
                                        user.Role));
                        break;
                }
            }

            return query;
        }

        private static IQueryable<AppUser>
            ApplySort(
                IQueryable<AppUser> query,
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

            return column switch
            {
                "fullname" when descending =>
                    query
                        .OrderByDescending(
                            user =>
                                user.FullName)
                        .ThenByDescending(
                            user =>
                                user.UserName),

                "fullname" =>
                    query
                        .OrderBy(
                            user =>
                                user.FullName)
                        .ThenBy(
                            user =>
                                user.UserName),

                "username" when descending =>
                    query
                        .OrderByDescending(
                            user =>
                                user.UserName)
                        .ThenByDescending(
                            user =>
                                user.FullName),

                "username" =>
                    query
                        .OrderBy(
                            user =>
                                user.UserName)
                        .ThenBy(
                            user =>
                                user.FullName),

                "role" when descending =>
                    query
                        .OrderByDescending(
                            user =>
                                user.Role)
                        .ThenByDescending(
                            user =>
                                user.FullName),

                "role" =>
                    query
                        .OrderBy(
                            user =>
                                user.Role)
                        .ThenBy(
                            user =>
                                user.FullName),

                _ =>
                    query
                        .OrderBy(
                            user =>
                                user.FullName)
                        .ThenBy(
                            user =>
                                user.UserName)
            };
        }

        private static Dictionary<
                string,
                string[]>
            ParseExcelFilters(
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
                        NormalizeColumn(
                            pair.Key);

                    if (
                        !ExcelColumns.Contains(
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
            GetExcelValue(
                AppUser user,
                string column)
        {
            return column switch
            {
                "fullname" =>
                    user.FullName,

                "username" =>
                    user.UserName,

                "role" =>
                    user.Role,

                _ =>
                    string.Empty
            };
        }

        private static string
            GetExcelLabel(
                AppUser user,
                string column)
        {
            var value =
                GetExcelValue(
                    user,
                    column);

            return string.IsNullOrEmpty(
                value)
                    ? "(Blank)"
                    : value;
        }

        private static string
            NormalizeColumn(
                string? column)
        {
            return column?
                .Trim()
                .ToLowerInvariant() ??
                string.Empty;
        }

        private static AdminUserDto
            Map(
                AppUser user,
                string currentUserName)
        {
            return new AdminUserDto
            {
                Id =
                    user.Id,

                FullName =
                    user.FullName,

                UserName =
                    user.UserName,

                Role =
                    user.Role,

                IsCurrentUser =
                    user.UserName.Equals(
                        currentUserName,
                        StringComparison.OrdinalIgnoreCase)
            };
        }
    }
}
