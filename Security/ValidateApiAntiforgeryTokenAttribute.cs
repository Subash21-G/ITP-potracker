using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace POTracker.Security
{
    /// <summary>
    /// Validates Angular XSRF tokens without registering Razor or
    /// MVC view services.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class
        ValidateApiAntiforgeryTokenAttribute :
            TypeFilterAttribute
    {
        public ValidateApiAntiforgeryTokenAttribute()
            : base(
                typeof(
                    ValidateApiAntiforgeryTokenFilter))
        {
            Order = 1000;
        }
    }

    /// <summary>
    /// Performs antiforgery validation and returns Problem Details
    /// instead of an unhandled server exception.
    /// </summary>
    public sealed class
        ValidateApiAntiforgeryTokenFilter :
            IAsyncAuthorizationFilter
    {
        private readonly IAntiforgery
            _antiforgery;

        private readonly ILogger<
            ValidateApiAntiforgeryTokenFilter>
                _logger;

        public ValidateApiAntiforgeryTokenFilter(
            IAntiforgery antiforgery,
            ILogger<
                ValidateApiAntiforgeryTokenFilter>
                    logger)
        {
            _antiforgery =
                antiforgery;

            _logger =
                logger;
        }

        public async Task OnAuthorizationAsync(
            AuthorizationFilterContext context)
        {
            if (context.Result != null)
            {
                return;
            }

            try
            {
                await _antiforgery
                    .ValidateRequestAsync(
                        context.HttpContext);
            }
            catch (
                AntiforgeryValidationException)
            {
                var request =
                    context.HttpContext.Request;

                var traceId =
                    context.HttpContext
                        .TraceIdentifier;

                _logger.LogWarning(
                    "Antiforgery validation failed for " +
                    "{Method} {Path}. Trace ID: {TraceId}",
                    request.Method,
                    request.Path,
                    traceId);

                var problem =
                    new ProblemDetails
                    {
                        Status =
                            StatusCodes
                                .Status400BadRequest,

                        Title =
                            "Invalid security token.",

                        Detail =
                            "The request security token is missing, expired or invalid."
                    };

                problem.Extensions["traceId"] =
                    traceId;

                context.Result =
                    new BadRequestObjectResult(
                        problem);
            }
        }
    }
}