using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    [ApiController]
    [Route("api/admin")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class AdminApiController :
        ControllerBase
    {
        private static readonly string[] AllowedRoles =
        {
            "Viewer",
            "Production",
            "Admin"
        };

        private readonly ApplicationDbContext _context;

        public AdminApiController(
            ApplicationDbContext context)
        {
            _context =
                context;
        }

        // ============================================================
        // USERS
        // ============================================================

        [HttpGet("users")]
        [ProducesResponseType(
            typeof(AdminUsersResponseDto),
            StatusCodes.Status200OK)]
        public async Task<
            ActionResult<AdminUsersResponseDto>>
            GetUsers(
                CancellationToken cancellationToken)
        {
            var currentUserName =
                HttpContext.Session.GetString(
                    "UserName") ??
                string.Empty;

            var users =
                await _context.AppUsers
                    .AsNoTracking()
                    .OrderBy(user =>
                        user.FullName)
                    .ThenBy(user =>
                        user.UserName)
                    .ToListAsync(
                        cancellationToken);

            return Ok(
                new AdminUsersResponseDto
                {
                    Users =
                        users
                            .Select(user =>
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
                                users.Count(user =>
                                    user.Role.Equals(
                                        "Admin",
                                        StringComparison
                                            .OrdinalIgnoreCase)),

                            ProductionUsers =
                                users.Count(user =>
                                    user.Role.Equals(
                                        "Production",
                                        StringComparison
                                            .OrdinalIgnoreCase)),

                            Viewers =
                                users.Count(user =>
                                    user.Role.Equals(
                                        "Viewer",
                                        StringComparison
                                            .OrdinalIgnoreCase))
                        },

                    AllowedRoles =
                        AllowedRoles.ToList()
                });
        }

        // ============================================================
        // UPDATE ROLE
        // ============================================================

        [HttpPut("users/{id:int}/role")]
        [ValidateAntiForgeryToken]
        [ProducesResponseType(
            typeof(AdminUserMutationResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(AdminUserMutationResponseDto),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(AdminUserMutationResponseDto),
            StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<AdminUserMutationResponseDto>>
            UpdateRole(
                int id,
                UpdateUserRoleRequestDto request,
                CancellationToken cancellationToken)
        {
            var normalizedRole =
                AllowedRoles.FirstOrDefault(
                    allowedRole =>
                        allowedRole.Equals(
                            request.Role?.Trim(),
                            StringComparison
                                .OrdinalIgnoreCase));

            if (normalizedRole == null)
            {
                return BadRequest(
                    Failure(
                        "Select a valid role: Viewer, Production or Admin."));
            }

            var user =
                await _context.AppUsers
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id == id,
                        cancellationToken);

            if (user == null)
            {
                return NotFound(
                    Failure(
                        "The selected user no longer exists."));
            }

            var currentUserName =
                HttpContext.Session.GetString(
                    "UserName") ??
                string.Empty;

            if (
                user.UserName.Equals(
                    currentUserName,
                    StringComparison
                        .OrdinalIgnoreCase)
            )
            {
                return BadRequest(
                    Failure(
                        "You cannot change your own role while signed in. Ask another administrator to change it."));
            }

            var isDemotingAdministrator =
                user.Role.Equals(
                    "Admin",
                    StringComparison
                        .OrdinalIgnoreCase) &&
                !normalizedRole.Equals(
                    "Admin",
                    StringComparison
                        .OrdinalIgnoreCase);

            if (isDemotingAdministrator)
            {
                var administratorCount =
                    await _context.AppUsers
                        .CountAsync(
                            item =>
                                item.Role == "Admin",
                            cancellationToken);

                if (administratorCount <= 1)
                {
                    return BadRequest(
                        Failure(
                            "The last administrator cannot be demoted."));
                }
            }

            if (
                user.Role.Equals(
                    normalizedRole,
                    StringComparison
                        .OrdinalIgnoreCase)
            )
            {
                return Ok(
                    new AdminUserMutationResponseDto
                    {
                        Success =
                            true,

                        Message =
                            $"{user.FullName} is already a {normalizedRole}.",

                        User =
                            Map(
                                user,
                                currentUserName)
                    });
            }

            user.Role =
                normalizedRole;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                new AdminUserMutationResponseDto
                {
                    Success =
                        true,

                    Message =
                        $"{user.FullName}'s role was changed to {normalizedRole}. The user must sign out and sign in again.",

                    User =
                        Map(
                            user,
                            currentUserName)
                });
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
                        StringComparison
                            .OrdinalIgnoreCase)
            };
        }

        private static AdminUserMutationResponseDto
            Failure(
                string message)
        {
            return new AdminUserMutationResponseDto
            {
                Success =
                    false,

                Message =
                    message
            };
        }
    }
}