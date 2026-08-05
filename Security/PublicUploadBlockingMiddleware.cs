using Microsoft.AspNetCore.Http;

namespace POTracker.Security
{
    /// <summary>
    /// Prevents files retained under wwwroot/uploads from being
    /// downloaded through unauthenticated static-file URLs.
    /// </summary>
    public sealed class PublicUploadBlockingMiddleware
    {
        private readonly RequestDelegate _next;

        public PublicUploadBlockingMiddleware(
            RequestDelegate next)
        {
            _next =
                next ??
                throw new ArgumentNullException(
                    nameof(next));
        }

        public Task InvokeAsync(
            HttpContext context)
        {
            if (context.Request.Path
                    .StartsWithSegments(
                        "/uploads"))
            {
                context.Response.StatusCode =
                    StatusCodes.Status404NotFound;

                context.Response.Headers.CacheControl =
                    "no-store, no-cache, must-revalidate";

                context.Response.Headers.Pragma =
                    "no-cache";

                context.Response.Headers.Expires =
                    "0";

                return Task.CompletedTask;
            }

            return _next(context);
        }
    }
}
