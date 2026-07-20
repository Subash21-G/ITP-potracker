using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace POTracker.Security
{
    /// <summary>
    /// Applies the application's session login and role rules from one
    /// central place. This prevents a hidden menu item or API endpoint
    /// from being opened by typing its URL directly.
    /// </summary>
    public sealed class SessionAuthorizationFilter : IAuthorizationFilter
    {
        private static readonly HashSet<string> SupportedRoles =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Admin",
                "Production",
                "Viewer"
            };

        private static readonly HashSet<string> AdminOnlyControllers =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Admin",
                "Analytics",
                "Audit",
                "Report"
            };

        private static readonly Dictionary<string, HashSet<string>>
            ViewerActions =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Dashboard"] = Actions("Index"),
                    ["Purchaser"] = Actions("Index"),
                    ["PurchaseOrder"] = Actions(
                        "Index",
                        "Details",
                        "ExportExcel"),
                    ["Material"] = Actions(
                        "Index",
                        "Details",
                        "ExportExcel"),
                    ["PendingMaterial"] = Actions(
                        "Index",
                        "ExportExcel"),
                    ["Dispatch"] = Actions(
                        "Index",
                        "Details"),
                    ["ProcessQueue"] = Actions("Index"),
                    ["Stock"] = Actions("Index"),
                    ["Drawing"] = Actions("Index"),
                    ["Calendar"] = Actions(
                        "Index",
                        "GetEvents"),
                    ["Alert"] = Actions("Index"),
                    ["Notification"] = Actions("Index"),
                    ["Home"] = Actions(
                        "Index",
                        "Privacy")
                };

        private static readonly Dictionary<string, HashSet<string>>
            ViewerApiActions =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["MaterialsApi"] = Actions(
                        "Index",
                        "Details"),
                    ["PurchaseOrdersApi"] = Actions(
                        "Index",
                        "Details"),

                    // A signed-in Viewer can read the current session.
                    ["AuthApi"] = Actions(
                        "GetCurrentUser",
                        "Logout")
                };

        public void OnAuthorization(
            AuthorizationFilterContext context)
        {
            var controller =
                GetRouteValue(context, "controller");

            var action =
                GetRouteValue(context, "action");

            var isApiRequest =
                IsApiRequest(context);

            if (IsPublicEndpoint(controller, action))
            {
                return;
            }

            var session =
                context.HttpContext.Session;

            var userName =
                session.GetString("UserName");

            if (string.IsNullOrWhiteSpace(userName))
            {
                RejectUnauthenticated(
                    context,
                    isApiRequest);

                return;
            }

            var role =
                session.GetString("Role")?.Trim();

            if (string.IsNullOrWhiteSpace(role) ||
                !SupportedRoles.Contains(role))
            {
                session.Clear();

                RejectUnauthenticated(
                    context,
                    isApiRequest);

                return;
            }

            if (role.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (AdminOnlyControllers.Contains(controller))
            {
                RejectForbidden(
                    context,
                    isApiRequest);

                return;
            }

            if (role.Equals(
                    "Production",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (IsViewerRequestAllowed(
                    context,
                    controller,
                    action,
                    isApiRequest))
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

            // Viewer accounts are read-only, except logout.
            var isViewerLogout =
                controller.Equals(
                    "AuthApi",
                    StringComparison.OrdinalIgnoreCase) &&
                action.Equals(
                    "Logout",
                    StringComparison.OrdinalIgnoreCase) &&
                HttpMethods.IsPost(method);

            if (isViewerLogout)
            {
                return true;
            }

            if (!HttpMethods.IsGet(method) &&
                !HttpMethods.IsHead(method))
            {
                return false;
            }

            var allowedActionsByController =
                isApiRequest
                    ? ViewerApiActions
                    : ViewerActions;

            return allowedActionsByController.TryGetValue(
                       controller,
                       out var allowedActions) &&
                   allowedActions.Contains(action);
        }

        private static bool IsApiRequest(
            AuthorizationFilterContext context)
        {
            return context.HttpContext.Request.Path
                .StartsWithSegments("/api");
        }

        private static bool IsPublicEndpoint(
            string controller,
            string action)
        {
            // Existing MVC login and registration pages remain public.
            if (controller.Equals(
                    "Account",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Angular needs these endpoints before a session exists.
            // Logout is deliberately not public.
            if (controller.Equals(
                    "AuthApi",
                    StringComparison.OrdinalIgnoreCase))
            {
                return action.Equals(
                           "GetXsrfToken",
                           StringComparison.OrdinalIgnoreCase) ||
                       action.Equals(
                           "GetCurrentUser",
                           StringComparison.OrdinalIgnoreCase) ||
                       action.Equals(
                           "Login",
                           StringComparison.OrdinalIgnoreCase);
            }

            return controller.Equals(
                       "Home",
                       StringComparison.OrdinalIgnoreCase) &&
                   (action.Equals(
                        "Error",
                        StringComparison.OrdinalIgnoreCase) ||
                    action.Equals(
                        "AccessDenied",
                        StringComparison.OrdinalIgnoreCase));
        }

        private static string GetRouteValue(
            AuthorizationFilterContext context,
            string key)
        {
            return context.ActionDescriptor.RouteValues
                       .TryGetValue(key, out var value)
                ? value ?? string.Empty
                : string.Empty;
        }

        private static HashSet<string> Actions(
            params string[] actions)
        {
            return new HashSet<string>(
                actions,
                StringComparer.OrdinalIgnoreCase);
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
                            StatusCodes.Status403Forbidden
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