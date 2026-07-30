using System.Collections.Generic;

namespace POTracker.Contracts.Api
{
    public sealed class AdminUserDto
    {
        public int Id { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string UserName { get; init; } = string.Empty;

        public string Role { get; init; } = string.Empty;

        public bool IsCurrentUser { get; init; }
    }

    public sealed class AdminUserSummaryDto
    {
        public int TotalUsers { get; init; }

        public int Administrators { get; init; }

        public int ProductionUsers { get; init; }

        public int Viewers { get; init; }
    }

    public sealed class AdminUsersResponseDto
    {
        public List<AdminUserDto> Users { get; init; } = new();

        public AdminUserSummaryDto Summary { get; init; } = new();

        public List<string> AllowedRoles { get; init; } = new();
    }

    public sealed class UpdateUserRoleRequestDto
    {
        public string Role { get; init; } = string.Empty;
    }

    public sealed class AdminUserMutationResponseDto
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public AdminUserDto? User { get; init; }
    }
}