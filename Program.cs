using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Security;
using POTracker.Services;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// MVC
// ============================================================

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<SessionAuthorizationFilter>();
});

// ============================================================
// APPLICATION SERVICES
// ============================================================

builder.Services.AddScoped<PendingInvoiceMatcher>();

// ============================================================
// LARGE PO / INVOICE IMPORT SUPPORT
// ============================================================

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 10000;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 1024L * 1024L * 200L; // 200 MB
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// ============================================================
// RESPONSE COMPRESSION
// Compress dynamic HTML, CSS, JavaScript and JSON responses.
// ============================================================

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();

    options.MimeTypes =
        ResponseCompressionDefaults.MimeTypes.Concat(
            new[]
            {
                "application/json",
                "application/javascript",
                "text/javascript",
                "image/svg+xml"
            });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// ============================================================
// DATABASE
// DbContext pooling reduces repeated DbContext creation cost.
// Retry execution strategy is intentionally not enabled because invoice
// imports use explicit database transactions.
// ============================================================

builder.Services.AddDbContextPool<ApplicationDbContext>(
    options =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions =>
            {
                sqlOptions.CommandTimeout(60);
            });
    },
    poolSize: 128);

// ============================================================
// SESSION
// ============================================================

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddHttpContextAccessor();

// ============================================================
// STOCK MAINTENANCE API
// ============================================================

builder.Services.AddHttpClient("StockApi", client =>
{
    var stockApiBaseUrl = builder.Configuration["StockApi:BaseUrl"];

    if (!string.IsNullOrWhiteSpace(stockApiBaseUrl))
    {
        client.BaseAddress = new Uri(stockApiBaseUrl);
    }

    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// ============================================================
// ERROR HANDLING
// ============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ============================================================
// RESPONSE COMPRESSION
// Must be before static files and endpoints.
// ============================================================

app.UseResponseCompression();
app.UseHttpsRedirection();

// ============================================================
// STATIC FILE CACHING
// CSS, JS and libraries are cached.
// Uploaded business documents are never browser-cached.
// ============================================================

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var requestPath = context.Context.Request.Path;

        if (requestPath.StartsWithSegments("/uploads"))
        {
            context.Context.Response.Headers.CacheControl =
                "no-store, no-cache, must-revalidate";
            context.Context.Response.Headers.Pragma = "no-cache";
            context.Context.Response.Headers.Expires = "0";
            return;
        }

        if (requestPath.StartsWithSegments("/app"))
        {
            var isAngularDocument =
                requestPath.Value?.EndsWith(
                    ".html",
                    StringComparison.OrdinalIgnoreCase) == true;

            context.Context.Response.Headers.CacheControl =
                isAngularDocument
                    ? "no-cache"
                    : "public,max-age=31536000,immutable";

            return;
        }

        var isCacheableAsset =
            requestPath.StartsWithSegments("/css") ||
            requestPath.StartsWithSegments("/js") ||
            requestPath.StartsWithSegments("/lib") ||
            requestPath.StartsWithSegments("/images") ||
            requestPath.Value?.EndsWith(
                "favicon.ico",
                StringComparison.OrdinalIgnoreCase) == true;

        if (isCacheableAsset)
        {
            const int cacheDurationSeconds = 60 * 60 * 24 * 7; // 7 days
            context.Context.Response.Headers.CacheControl =
                $"public,max-age={cacheDurationSeconds}";
        }
        else
        {
            context.Context.Response.Headers.CacheControl = "no-cache";
        }
    }
});

// ============================================================
// REQUEST PIPELINE
// ============================================================

app.UseRouting();
app.UseSession();
app.UseAuthorization();

// ============================================================
// ROUTES
// ============================================================

// Attribute-routed JSON APIs used by the Angular application.
app.MapControllers();

// Existing MVC pages remain available during the gradual migration.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// The production Angular build is served by ASP.NET from wwwroot/app.
// Only /app routes use the SPA fallback, so MVC and API 404s stay intact.
app.MapGet(
    "/app",
    () => Results.Redirect("/app/materials"));

app.MapFallbackToFile(
    "app/{*path:nonfile}",
    "app/index.html");

app.Run();
