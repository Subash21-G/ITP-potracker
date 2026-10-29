using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace POTracker.Security
{
    /// <summary>
    /// Applies session authentication and role authorization to
    /// the Angular JSON API controllers.
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
                    "AdminApi",
                    "AnalyticsApi",
                    "AuditApi",
                    "ReportsApi"
                };

        public void OnAuthorization(
            AuthorizationFilterContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata
                .OfType<QuotationIntegrationAttribute>().Any())
            {
                QuotationIntegrationAttribute.Authorize(context);
                return;
            }

            var controller =
                GetRouteValue(
                    context,
                    "controller");

            var action =
                GetRouteValue(
                    context,
                    "action");

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
                    context);

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
                    context);

                return;
            }

            if (
                role.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase)
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
                    context);

                return;
            }

            if (
                role.Equals(
                    "Production",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                return;
            }

            if (
                IsViewerRequestAllowed(
                    context,
                    controller,
                    action)
            )
            {
                return;
            }

            RejectForbidden(
                context);
        }

        private static bool IsViewerRequestAllowed(
            AuthorizationFilterContext context,
            string controller,
            string action)
        {
            var method =
                context.HttpContext.Request.Method;

            var isLogout =
                controller.Equals(
                    "AuthApi",
                    StringComparison.OrdinalIgnoreCase) &&
                action.Equals(
                    "Logout",
                    StringComparison.OrdinalIgnoreCase) &&
                HttpMethods.IsPost(
                    method);

            if (isLogout)
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

            return
                !AdminOnlyControllers.Contains(
                    controller);
        }

        private static bool IsPublicEndpoint(
            string controller,
            string action)
        {
            if (
                !controller.Equals(
                    "AuthApi",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }

            return
                action.Equals(
                    "GetXsrfToken",
                    StringComparison.OrdinalIgnoreCase) ||
                action.Equals(
                    "GetCurrentUser",
                    StringComparison.OrdinalIgnoreCase) ||
                action.Equals(
                    "Login",
                    StringComparison.OrdinalIgnoreCase) ||
                action.Equals(
                    "Register",
                    StringComparison.OrdinalIgnoreCase);
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
                    ? value ?? string.Empty
                    : string.Empty;
        }

        private static void RejectUnauthenticated(
            AuthorizationFilterContext context)
        {
            context.Result =
                new UnauthorizedObjectResult(
                    new
                    {
                        error =
                            "Authentication is required."
                    });
        }

        private static void RejectForbidden(
            AuthorizationFilterContext context)
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
        }
    }
}
