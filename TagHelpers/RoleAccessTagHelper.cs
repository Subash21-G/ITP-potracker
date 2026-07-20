using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace POTracker.TagHelpers
{
    /// <summary>
    /// Renders its child content only when the signed-in user's session role
    /// matches one of the comma-separated roles supplied in the roles
    /// attribute. Route authorization is still enforced by the server-side
    /// session authorization filter.
    /// </summary>
    [HtmlTargetElement("role-access", Attributes = "roles")]
    public sealed class RoleAccessTagHelper : TagHelper
    {
        [HtmlAttributeName("roles")]
        public string Roles { get; set; } = string.Empty;

        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; } = default!;

        public override void Process(
            TagHelperContext context,
            TagHelperOutput output)
        {
            var currentRole =
                ViewContext.HttpContext.Session
                    .GetString("Role")?
                    .Trim();

            var allowedRoles = Roles.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

            var isAllowed =
                !string.IsNullOrWhiteSpace(currentRole) &&
                allowedRoles.Any(role =>
                    role.Equals(
                        currentRole,
                        StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
                output.SuppressOutput();
                return;
            }

            // Remove the custom wrapper and render only its child markup.
            output.TagName = null;
        }
    }
}
