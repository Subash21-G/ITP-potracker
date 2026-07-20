using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;

namespace POTracker.Controllers
{
    public class AdminController : Controller
    {
        private static readonly string[] AllowedRoles =
        {
            "Viewer",
            "Production",
            "Admin"
        };

        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Users()
        {
            var users = await _context.AppUsers
                .AsNoTracking()
                .OrderBy(x => x.FullName)
                .ThenBy(x => x.UserName)
                .ToListAsync();

            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(
            int id,
            string? role)
        {
            var normalizedRole = AllowedRoles.FirstOrDefault(
                allowedRole => allowedRole.Equals(
                    role?.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (normalizedRole == null)
            {
                TempData["Error"] =
                    "Select a valid role: Viewer, Production or Admin.";

                return RedirectToAction(nameof(Users));
            }

            var user = await _context.AppUsers
                .FirstOrDefaultAsync(x => x.Id == id);

            if (user == null)
            {
                TempData["Error"] = "The selected user no longer exists.";
                return RedirectToAction(nameof(Users));
            }

            var currentUserName =
                HttpContext.Session.GetString("UserName");

            if (string.Equals(
                    user.UserName,
                    currentUserName,
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] =
                    "You cannot change your own role while signed in. " +
                    "Ask another administrator to change it.";

                return RedirectToAction(nameof(Users));
            }

            var isDemotingAdministrator =
                user.Role.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase) &&
                !normalizedRole.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase);

            if (isDemotingAdministrator)
            {
                var administratorCount =
                    await _context.AppUsers.CountAsync(x =>
                        x.Role == "Admin");

                if (administratorCount <= 1)
                {
                    TempData["Error"] =
                        "The last administrator cannot be demoted.";

                    return RedirectToAction(nameof(Users));
                }
            }

            if (user.Role.Equals(
                    normalizedRole,
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["Success"] =
                    $"{user.FullName} is already a {normalizedRole}.";

                return RedirectToAction(nameof(Users));
            }

            user.Role = normalizedRole;
            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{user.FullName}'s role was changed to {normalizedRole}. " +
                "The user must sign out and sign in again.";

            return RedirectToAction(nameof(Users));
        }

        public IActionResult Settings()
        {
            return View();
        }
    }
}
