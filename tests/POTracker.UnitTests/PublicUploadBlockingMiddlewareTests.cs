using Microsoft.AspNetCore.Http;
using POTracker.Security;

namespace POTracker.UnitTests.Security
{
    public sealed class
        PublicUploadBlockingMiddlewareTests
    {
        [Fact]
        public async Task
            InvokeAsync_BlocksDirectUploadFileRequest()
        {
            var nextInvoked =
                false;

            var middleware =
                new PublicUploadBlockingMiddleware(
                    context =>
                    {
                        nextInvoked = true;

                        context.Response.StatusCode =
                            StatusCodes.Status204NoContent;

                        return Task.CompletedTask;
                    });

            var context =
                new DefaultHttpContext();

            context.Request.Path =
                "/uploads/po/sample.pdf";

            await middleware.InvokeAsync(
                context);

            Assert.False(
                nextInvoked);

            Assert.Equal(
                StatusCodes.Status404NotFound,
                context.Response.StatusCode);

            Assert.Equal(
                "no-store, no-cache, must-revalidate",
                context.Response.Headers.CacheControl
                    .ToString());

            Assert.Equal(
                "no-cache",
                context.Response.Headers.Pragma
                    .ToString());

            Assert.Equal(
                "0",
                context.Response.Headers.Expires
                    .ToString());
        }

        [Fact]
        public async Task
            InvokeAsync_BlocksUploadPathCaseInsensitively()
        {
            var nextInvoked =
                false;

            var middleware =
                new PublicUploadBlockingMiddleware(
                    context =>
                    {
                        nextInvoked = true;

                        return Task.CompletedTask;
                    });

            var context =
                new DefaultHttpContext();

            context.Request.Path =
                "/UPLOADS";

            await middleware.InvokeAsync(
                context);

            Assert.False(
                nextInvoked);

            Assert.Equal(
                StatusCodes.Status404NotFound,
                context.Response.StatusCode);
        }

        [Fact]
        public async Task
            InvokeAsync_AllowsNonUploadPath()
        {
            var nextInvoked =
                false;

            var middleware =
                new PublicUploadBlockingMiddleware(
                    context =>
                    {
                        nextInvoked = true;

                        context.Response.StatusCode =
                            StatusCodes.Status204NoContent;

                        return Task.CompletedTask;
                    });

            var context =
                new DefaultHttpContext();

            context.Request.Path =
                "/uploads-archive/sample.pdf";

            await middleware.InvokeAsync(
                context);

            Assert.True(
                nextInvoked);

            Assert.Equal(
                StatusCodes.Status204NoContent,
                context.Response.StatusCode);
        }
    }
}
