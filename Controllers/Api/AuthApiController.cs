using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers.Api
{
    /// <summary>
    /// Session-based authentication endpoints used by Angular.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class AuthApiController :
        ControllerBase
    {
        private const string UserNameSessionKey =
            "UserName";

        private const string FullNameSessionKey =
            "FullName";

        private const string RoleSessionKey =
            "Role";

        private const string AngularXsrfCookieName =
            "XSRF-TOKEN";

        private static readonly HashSet<string>
            SupportedRoles =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "Admin",
                    "Production",
                    "Viewer"
                };

        private readonly ApplicationDbContext _context;

        private readonly IAntiforgery _antiforgery;

        public AuthApiController(
            ApplicationDbContext context,
            IAntiforgery antiforgery)
        {
            _context =
                context;

            _antiforgery =
                antiforgery;
        }

        // ============================================================
        // XSRF TOKEN
        // ============================================================

        [HttpGet("xsrf-token")]
        [IgnoreAntiforgeryToken]
        [ProducesResponseType(
            StatusCodes.Status200OK)]
        public IActionResult GetXsrfToken()
        {
            var tokens =
                _antiforgery.GetAndStoreTokens(
                    HttpContext);

            if (
                string.IsNullOrWhiteSpace(
                    tokens.RequestToken)
            )
            {
                return Problem(
                    title:
                        "Unable to create the security token.",
                    statusCode:
                        StatusCodes
                            .Status500InternalServerError);
            }

            Response.Cookies.Append(
                AngularXsrfCookieName,
                tokens.RequestToken,
                new CookieOptions
                {
                    HttpOnly =
                        false,

                    IsEssential =
                        true,

                    Secure =
                        Request.IsHttps,

                    SameSite =
                        SameSiteMode.Lax,

                    Path =
                        "/"
                });

            return Ok(
                new
                {
                    success =
                        true
                });
        }

        // ============================================================
        // CURRENT USER
        // ============================================================

        [HttpGet("me")]
        [IgnoreAntiforgeryToken]
        [ProducesResponseType(
            typeof(CurrentUserDto),
            StatusCodes.Status200OK)]
        public ActionResult<CurrentUserDto>
            GetCurrentUser()
        {
            return Ok(
                CreateCurrentUser());
        }

        // ============================================================
        // LOGIN
        // ============================================================

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        [ProducesResponseType(
            typeof(AuthenticationResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(AuthenticationResponseDto),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(AuthenticationResponseDto),
            StatusCodes.Status401Unauthorized)]
        public async Task<
            ActionResult<AuthenticationResponseDto>>
            Login(
                LoginRequestDto request,
                CancellationToken cancellationToken)
        {
            var userName =
                request.UserName?.Trim() ??
                string.Empty;

            var password =
                request.Password ??
                string.Empty;

            if (
                string.IsNullOrWhiteSpace(
                    userName) ||
                string.IsNullOrWhiteSpace(
                    password)
            )
            {
                return BadRequest(
                    new AuthenticationResponseDto
                    {
                        Success =
                            false,

                        Message =
                            "Username and password are required."
                    });
            }

            var normalizedUserName =
                userName.ToUpperInvariant();

            var user =
                await _context.AppUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item =>
                            item.UserName.ToUpper() ==
                            normalizedUserName,
                        cancellationToken);

            var passwordIsValid =
                false;

            if (
                user != null &&
                !string.IsNullOrWhiteSpace(
                    user.PasswordHash)
            )
            {
                try
                {
                    passwordIsValid =
                        BCrypt.Net.BCrypt.Verify(
                            password,
                            user.PasswordHash);
                }
                catch
                {
                    passwordIsValid =
                        false;
                }
            }

            if (
                user == null ||
                !passwordIsValid
            )
            {
                return Unauthorized(
                    new AuthenticationResponseDto
                    {
                        Success =
                            false,

                        Message =
                            "Invalid username or password."
                    });
            }

            if (
                !SupportedRoles.Contains(
                    user.Role)
            )
            {
                return Unauthorized(
                    new AuthenticationResponseDto
                    {
                        Success =
                            false,

                        Message =
                            "This account has an invalid role. Contact an administrator."
                    });
            }

            HttpContext.Session.Clear();

            HttpContext.Session.SetString(
                UserNameSessionKey,
                user.UserName);

            HttpContext.Session.SetString(
                FullNameSessionKey,
                user.FullName);

            HttpContext.Session.SetString(
                RoleSessionKey,
                user.Role);

            return Ok(
                new AuthenticationResponseDto
                {
                    Success =
                        true,

                    Message =
                        "Login successful.",

                    User =
                        CreateCurrentUser()
                });
        }

        // ============================================================
        // REGISTRATION
        // ============================================================

        [HttpPost("register")]
        [ValidateAntiForgeryToken]
        [ProducesResponseType(
            typeof(RegistrationResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(RegistrationResponseDto),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(RegistrationResponseDto),
            StatusCodes.Status409Conflict)]
        public async Task<
            ActionResult<RegistrationResponseDto>>
            Register(
                RegisterRequestDto request,
                CancellationToken cancellationToken)
        {
            var fullName =
                request.FullName?.Trim() ??
                string.Empty;

            var userName =
                request.UserName?.Trim() ??
                string.Empty;

            var password =
                request.Password ??
                string.Empty;

            if (
                string.IsNullOrWhiteSpace(
                    fullName) ||
                string.IsNullOrWhiteSpace(
                    userName) ||
                string.IsNullOrWhiteSpace(
                    password)
            )
            {
                return BadRequest(
                    RegistrationFailure(
                        "All fields are required."));
            }

            if (fullName.Length > 100)
            {
                return BadRequest(
                    RegistrationFailure(
                        "Full name cannot exceed 100 characters."));
            }

            if (
                userName.Length < 4 ||
                userName.Length > 50
            )
            {
                return BadRequest(
                    RegistrationFailure(
                        "Username must contain between 4 and 50 characters."));
            }

            if (
                !userName.All(character =>
                    char.IsLetterOrDigit(
                        character) ||
                    character == '.' ||
                    character == '_' ||
                    character == '-')
            )
            {
                return BadRequest(
                    RegistrationFailure(
                        "Username can contain only letters, numbers, dot, underscore and hyphen."));
            }

            if (
                password.Length < 8 ||
                !password.Any(
                    char.IsUpper) ||
                !password.Any(
                    char.IsLower) ||
                !password.Any(
                    char.IsDigit)
            )
            {
                return BadRequest(
                    RegistrationFailure(
                        "Password must be at least 8 characters and contain an uppercase letter, lowercase letter and number."));
            }

            var normalizedUserName =
                userName.ToUpperInvariant();

            var userExists =
                await _context.AppUsers
                    .AsNoTracking()
                    .AnyAsync(
                        item =>
                            item.UserName.ToUpper() ==
                            normalizedUserName,
                        cancellationToken);

            if (userExists)
            {
                return Conflict(
                    RegistrationFailure(
                        "Username already exists."));
            }

            var isFirstUser =
                !await _context.AppUsers
                    .AsNoTracking()
                    .AnyAsync(
                        cancellationToken);

            var role =
                isFirstUser
                    ? "Admin"
                    : "Viewer";

            var user =
                new AppUser
                {
                    FullName =
                        fullName,

                    UserName =
                        userName,

                    PasswordHash =
                        BCrypt.Net.BCrypt
                            .HashPassword(
                                password),

                    Role =
                        role
                };

            _context.AppUsers.Add(
                user);

            try
            {
                await _context.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException)
            {
                return Conflict(
                    RegistrationFailure(
                        "The account could not be created because the username is already in use."));
            }

            return Ok(
                new RegistrationResponseDto
                {
                    Success =
                        true,

                    Message =
                        isFirstUser
                            ? "Administrator account created successfully."
                            : "User account created successfully.",

                    IsFirstAdministrator =
                        isFirstUser,

                    Role =
                        role
                });
        }

        // ============================================================
        // LOGOUT
        // ============================================================

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        [ProducesResponseType(
            typeof(AuthenticationResponseDto),
            StatusCodes.Status200OK)]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return Ok(
                new AuthenticationResponseDto
                {
                    Success =
                        true,

                    Message =
                        "Logout successful.",

                    User =
                        new CurrentUserDto
                        {
                            IsAuthenticated =
                                false
                        }
                });
        }

        private CurrentUserDto
            CreateCurrentUser()
        {
            var userName =
                HttpContext.Session.GetString(
                    UserNameSessionKey);

            var role =
                HttpContext.Session.GetString(
                    RoleSessionKey)?.Trim();

            if (
                string.IsNullOrWhiteSpace(
                    userName) ||
                string.IsNullOrWhiteSpace(
                    role) ||
                !SupportedRoles.Contains(
                    role)
            )
            {
                HttpContext.Session.Clear();

                return new CurrentUserDto
                {
                    IsAuthenticated =
                        false
                };
            }

            return new CurrentUserDto
            {
                IsAuthenticated =
                    true,

                UserName =
                    userName,

                FullName =
                    HttpContext.Session.GetString(
                        FullNameSessionKey) ??
                    userName,

                Role =
                    role
            };
        }

        private static RegistrationResponseDto
            RegistrationFailure(
                string message)
        {
            return new RegistrationResponseDto
            {
                Success =
                    false,

                Message =
                    message
            };
        }
    }
}