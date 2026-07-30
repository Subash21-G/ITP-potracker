using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Models;

namespace POTracker.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            AppUser user,
            string password)
        {
            var fullName = user.FullName?.Trim() ?? string.Empty;
            var userName = user.UserName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "All fields are required.";
                user.FullName = fullName;
                user.UserName = userName;
                return View(user);
            }

            if (userName.Length < 4 || userName.Length > 50)
            {
                ViewBag.Error =
                    "Username must contain between 4 and 50 characters.";

                user.FullName = fullName;
                user.UserName = userName;
                return View(user);
            }

            if (!userName.All(x =>
                    char.IsLetterOrDigit(x) ||
                    x == '.' ||
                    x == '_' ||
                    x == '-'))
            {
                ViewBag.Error =
                    "Username can contain only letters, numbers, dot, " +
                    "underscore and hyphen.";

                user.FullName = fullName;
                user.UserName = userName;
                return View(user);
            }

            if (password.Length < 8 ||
                !password.Any(char.IsUpper) ||
                !password.Any(char.IsLower) ||
                !password.Any(char.IsDigit))
            {
                ViewBag.Error =
                    "Password must be at least 8 characters and contain " +
                    "an uppercase letter, lowercase letter and number.";

                user.FullName = fullName;
                user.UserName = userName;
                return View(user);
            }

            var normalizedUserName =
                userName.ToUpperInvariant();

            var exists = await _context.AppUsers.AnyAsync(x =>
                x.UserName.ToUpper() == normalizedUserName);

            if (exists)
            {
                ViewBag.Error = "Username already exists.";
                user.FullName = fullName;
                user.UserName = userName;
                return View(user);
            }

            // Only the first account in an empty database becomes Admin.
            // Any role value submitted by the browser is ignored.
            var isFirstUser =
                !await _context.AppUsers.AnyAsync();

            var newUser = new AppUser
            {
                FullName = fullName,
                UserName = userName,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(password),
                Role = isFirstUser ? "Admin" : "Viewer"
            };

            _context.AppUsers.Add(newUser);
            await _context.SaveChangesAsync();

            TempData["Success"] = isFirstUser
                ? "Administrator account created successfully. Please login."
                : "Registration successful. Please login.";

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserName") != null)
            {
                return Redirect("/app/dashboard");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string userName,
            string password)
        {
            var cleanedUserName = userName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(cleanedUserName) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                ViewBag.UserName = cleanedUserName;
                return View();
            }

            var normalizedUserName =
                cleanedUserName.ToUpperInvariant();

            var user = await _context.AppUsers.FirstOrDefaultAsync(x =>
                x.UserName.ToUpper() == normalizedUserName);

            if (user == null ||
                !BCrypt.Net.BCrypt.Verify(
                    password,
                    user.PasswordHash))
            {
                ViewBag.Error = "Invalid username or password.";
                ViewBag.UserName = cleanedUserName;
                return View();
            }

            HttpContext.Session.Clear();
            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetString("FullName", user.FullName);
            HttpContext.Session.SetString("Role", user.Role);

            return Redirect("/app/dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }
    }
}
