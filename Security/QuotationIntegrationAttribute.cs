using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace POTracker.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class QuotationIntegrationAttribute : Attribute
{
    public static void Authorize(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = configuration["QuotationIntegration:ApiKey"];
        var supplied = context.HttpContext.Request.Headers["X-Quotation-Api-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied) ||
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "A valid quotation integration API key is required." });
        }
    }
}
