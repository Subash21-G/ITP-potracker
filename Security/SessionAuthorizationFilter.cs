using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace POTracker.Security
{
    /// <summary>
    /// Applies session authentication and role rules to MVC and API
    /// controllers during the final Angular migration stage.
    /// </summary>
    public sealed class SessionAuthorizationFilter :
        IAuthorizationFilter
    {
        private static readonly HashSet<string>
            SupportedRoles =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "Admin",
                    "Production",
                    "Viewer"
                };

        private static readonly HashSet<string>
            AdminOnlyControllers =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    // Temporary MVC controllers.
                    "Admin",
                    "Analytics",
                    "Audit",
                    "Report",

                    // Angular JSON APIs.
                    "AdminApi",
                    "AnalyticsApi",
                    "AuditApi",
                    "ReportsApi"
                };

        private static readonly Dictionary<
            string,
            HashSet<string>>
            ViewerMvcActions =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["Dashboard"] =
                        Actions("Index"),

                    ["Purchaser"] =
                        Actions("Index"),

                    ["PurchaseOrder"] =
                        Actions(
                            "Index",
                            "Details",
                            "ExportExcel"),

                    ["Material"] =
                        Actions(
                            "Index",
                            "Details",
                            "ExportExcel"),

                    ["PendingMaterial"] =
                        Actions(
                            "Index",
                            "ExportExcel"),

                    ["Dispatch"] =
                        Actions(
                            "Index",
                            "Details"),

                    ["ProcessQueue"] =
                        Actions("Index"),

                    ["Stock"] =
                        Actions("Index"),

                    ["Drawing"] =
                        Actions("Index"),

                    ["Calendar"] =
                        Actions(
                            "Index",
                            "GetEvents"),

                    ["Alert"] =
                        Actions("Index"),

                    ["Notification"] =
                        Actions("Index"),

                    ["Home"] =
                        Actions(
                            "Index",
                            "Privacy")
                };

        public void OnAuthorization(
            AuthorizationFilterContext context)
        {
            var controller =
                GetRouteValue(
                    context,
                    "controller");

            var action =
                GetRouteValue(
                    context,
                    "action");

            var isApiRequest =
                IsApiRequest(
                    context);

            if (
                IsPublicEndpoint(
                    controller,
                    action)
            )
            {
                return;
            }

            var session =
                context.HttpContext.Session;

            var userName =
                session.GetString(
                    "UserName");

            if (
                string.IsNullOrWhiteSpace(
                    userName)
            )
            {
                RejectUnauthenticated(
                    context,
                    isApiRequest);

                return;
            }

            var role =
                session.GetString(
                    "Role")?.Trim();

            if (
                string.IsNullOrWhiteSpace(
                    role) ||
                !SupportedRoles.Contains(
                    role)
            )
            {
                session.Clear();

                RejectUnauthenticated(
                    context,
                    isApiRequest);

                return;
            }

            if (
                role.Equals(
                    "Admin",
                    StringComparison
                        .OrdinalIgnoreCase)
            )
            {
                return;
            }

            if (
                AdminOnlyControllers.Contains(
                    controller)
            )
            {
                RejectForbidden(
                    context,
                    isApiRequest);

                return;
            }

            if (
                role.Equals(
                    "Production",
                    StringComparison
                        .OrdinalIgnoreCase)
            )
            {
                return;
            }

            if (
                IsViewerRequestAllowed(
                    context,
                    controller,
                    action,
                    isApiRequest)
            )
            {
                return;
            }

            RejectForbidden(
                context,
                isApiRequest);
        }

        private static bool IsViewerRequestAllowed(
            AuthorizationFilterContext context,
            string controller,
            string action,
            bool isApiRequest)
        {
            var method =
                context.HttpContext.Request.Method;

            var isViewerLogout =
                controller.Equals(
                    "AuthApi",
                    StringComparison
                        .OrdinalIgnoreCase) &&
                action.Equals(
                    "Logout",
                    StringComparison
                        .OrdinalIgnoreCase) &&
                HttpMethods.IsPost(
                    method);

            if (isViewerLogout)
            {
                return true;
            }

            if (
                !HttpMethods.IsGet(
                    method) &&
                !HttpMethods.IsHead(
                    method)
            )
            {
                return false;
            }

            // Viewer accounts may read every non-admin API.
            // This prevents newly added read endpoints from being
            // accidentally blocked by a stale action-name list.
            if (isApiRequest)
            {
                return
                    !AdminOnlyControllers.Contains(
                        controller);
            }

            return
                ViewerMvcActions.TryGetValue(
                    controller,
                    out var allowedActions) &&
                allowedActions.Contains(
                    action);
        }

        private static bool IsApiRequest(
            AuthorizationFilterContext context)
        {
            return context.HttpContext.Request.Path
                .StartsWithSegments(
                    "/api");
        }

        private static bool IsPublicEndpoint(
            string controller,
            string action)
        {
            // Existing MVC account pages remain public until removal.
            if (
                controller.Equals(
                    "Account",
                    StringComparison
                        .OrdinalIgnoreCase)
            )
            {
                return true;
            }

            if (
                controller.Equals(
                    "AuthApi",
                    StringComparison
                        .OrdinalIgnoreCase)
            )
            {
                return action.Equals(
                           "GetXsrfToken",
                           StringComparison
                               .OrdinalIgnoreCase) ||
                       action.Equals(
                           "GetCurrentUser",
                           StringComparison
                               .OrdinalIgnoreCase) ||
                       action.Equals(
                           "Login",
                           StringComparison
                               .OrdinalIgnoreCase) ||
                       action.Equals(
                           "Register",
                           StringComparison
                               .OrdinalIgnoreCase);
            }

            return
                controller.Equals(
                    "Home",
                    StringComparison
                        .OrdinalIgnoreCase) &&
                (
                    action.Equals(
                        "Error",
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    action.Equals(
                        "AccessDenied",
                        StringComparison
                            .OrdinalIgnoreCase)
                );
        }

        private static string GetRouteValue(
            AuthorizationFilterContext context,
            string key)
        {
            return
                context.ActionDescriptor
                    .RouteValues
                    .TryGetValue(
                        key,
                        out var value)
                    ? value ??
                      string.Empty
                    : string.Empty;
        }

        private static HashSet<string> Actions(
            params string[] actions)
        {
            return new HashSet<string>(
                actions,
                StringComparer
                    .OrdinalIgnoreCase);
        }

        private static void RejectUnauthenticated(
            AuthorizationFilterContext context,
            bool isApiRequest)
        {
            if (isApiRequest)
            {
                context.Result =
                    new UnauthorizedObjectResult(
                        new
                        {
                            error =
                                "Authentication is required."
                        });

                return;
            }

            context.Result =
                new RedirectToActionResult(
                    "Login",
                    "Account",
                    null);
        }

        private static void RejectForbidden(
            AuthorizationFilterContext context,
            bool isApiRequest)
        {
            if (isApiRequest)
            {
                context.Result =
                    new ObjectResult(
                        new
                        {
                            error =
                                "You do not have permission for this operation."
                        })
                    {
                        StatusCode =
                            StatusCodes
                                .Status403Forbidden
                    };

                return;
            }

            context.Result =
                new RedirectToActionResult(
                    "AccessDenied",
                    "Home",
                    null);
        }
    }
}