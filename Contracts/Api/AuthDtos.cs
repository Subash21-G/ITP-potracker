namespace POTracker.Contracts.Api
{
    public sealed class LoginRequestDto
    {
        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    public sealed class CurrentUserDto
    {
        public bool IsAuthenticated { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsAdmin =>
            Role.Equals(
                "Admin",
                StringComparison.OrdinalIgnoreCase);

        public bool IsProduction =>
            Role.Equals(
                "Production",
                StringComparison.OrdinalIgnoreCase);

        public bool IsViewer =>
            Role.Equals(
                "Viewer",
                StringComparison.OrdinalIgnoreCase);

        public bool CanOperate =>
            IsAdmin || IsProduction;
    }

    public sealed class AuthenticationResponseDto
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public CurrentUserDto? User { get; set; }
    }
}