using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Contracts.Api;
using POTracker.Data;

namespace POTracker.Controllers.Api
{
    /// <summary>
    /// Session-based authentication endpoints used by the Angular application.
    /// Existing MVC login and logout actions remain available during migration.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public sealed class AuthApiController : ControllerBase
    {
        private const string UserNameSessionKey = "UserName";
        private const string FullNameSessionKey = "FullName";
        private const string RoleSessionKey = "Role";
        private const string AngularXsrfCookieName = "XSRF-TOKEN";

        private readonly ApplicationDbContext _context;
        private readonly IAntiforgery _antiforgery;

        public AuthApiController(
            ApplicationDbContext context,
            IAntiforgery antiforgery)
        {
            _context = context;
            _antiforgery = antiforgery;
        }

        /// <summary>
        /// Issues the antiforgery cookies required before Angular sends
        /// login, logout, POST, PUT, PATCH or DELETE requests.
        /// </summary>
        [HttpGet("xsrf-token")]
        [IgnoreAntiforgeryToken]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetXsrfToken()
        {
            var tokens =
                _antiforgery.GetAndStoreTokens(HttpContext);

            if (string.IsNullOrWhiteSpace(tokens.RequestToken))
            {
                return Problem(
                    title: "Unable to create the security token.",
                    statusCode:
                        StatusCodes.Status500InternalServerError);
            }

            // Angular must be able to read this request-token cookie.
            // The protected ASP.NET antiforgery cookie remains HttpOnly
            // and is configured separately in Program.cs.
            Response.Cookies.Append(
                AngularXsrfCookieName,
                tokens.RequestToken,
                new CookieOptions
                {
                    HttpOnly = false,
                    IsEssential = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });

            return Ok(new
            {
                success = true
            });
        }

        /// <summary>
        /// Returns the user currently stored in the ASP.NET session.
        /// This endpoint also returns a normal response when signed out,
        /// allowing Angular to decide whether to show the login page.
        /// </summary>
        [HttpGet("me")]
        [IgnoreAntiforgeryToken]
        [ProducesResponseType(
            typeof(CurrentUserDto),
            StatusCodes.Status200OK)]
        public ActionResult<CurrentUserDto> GetCurrentUser()
        {
            return Ok(CreateCurrentUser());
        }

        /// <summary>
        /// Verifies the existing BCrypt password and creates the same
        /// session values used by the MVC application.
        /// </summary>
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
        public async Task<ActionResult<AuthenticationResponseDto>>
            Login(
                LoginRequestDto request,
                CancellationToken cancellationToken)
        {
            var userName =
                request.UserName?.Trim() ?? string.Empty;

            var password =
                request.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(
                    new AuthenticationResponseDto
                    {
                        Success = false,
                        Message =
                            "Username and password are required."
                    });
            }

            var normalizedUserName =
                userName.ToUpperInvariant();

            var user = await _context.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item =>
                        item.UserName.ToUpper() ==
                        normalizedUserName,
                    cancellationToken);

            var passwordIsValid = false;

            if (user != null &&
                !string.IsNullOrWhiteSpace(user.PasswordHash))
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
                    // Treat a malformed legacy hash as invalid login data.
                    passwordIsValid = false;
                }
            }

            if (user == null || !passwordIsValid)
            {
                return Unauthorized(
                    new AuthenticationResponseDto
                    {
                        Success = false,
                        Message =
                            "Invalid username or password."
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
                    Success = true,
                    Message = "Login successful.",
                    User = CreateCurrentUser()
                });
        }

        /// <summary>
        /// Clears the ASP.NET session used by Angular and MVC.
        /// </summary>
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
                    Success = true,
                    Message = "Logout successful.",
                    User = new CurrentUserDto
                    {
                        IsAuthenticated = false
                    }
                });
        }

        private CurrentUserDto CreateCurrentUser()
        {
            var userName =
                HttpContext.Session.GetString(
                    UserNameSessionKey);

            if (string.IsNullOrWhiteSpace(userName))
            {
                return new CurrentUserDto
                {
                    IsAuthenticated = false
                };
            }

            return new CurrentUserDto
            {
                IsAuthenticated = true,
                UserName = userName,
                FullName =
                    HttpContext.Session.GetString(
                        FullNameSessionKey) ??
                    userName,
                Role =
                    HttpContext.Session.GetString(
                        RoleSessionKey) ??
                    string.Empty
            };
        }
    }
}