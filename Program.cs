using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Security;
using POTracker.Services;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// Enable native Windows Service lifetime when launched by the
// Service Control Manager. Normal command-line execution is unchanged.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "POTracker";
});

var secureCookiePolicy =
    builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

// ============================================================
// API CONTROLLERS AND GLOBAL SESSION AUTHORIZATION
// ============================================================

builder.Services.AddControllers(options =>
{
    options.Filters.Add<SessionAuthorizationFilter>();
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});

// ============================================================
// HEALTH CHECKS
// Lightweight deployment monitoring without a database query.
// ============================================================

builder.Services.AddHealthChecks();

// ============================================================
// ANGULAR XSRF / CSRF PROTECTION
// Angular sends the request token through the X-XSRF-TOKEN header.
// The readable XSRF-TOKEN cookie will be issued by the authentication API.
// ============================================================

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";

    // ASP.NET Core's protected antiforgery cookie.
    // This remains HttpOnly. Angular does not read this cookie directly.
    options.Cookie.Name = ".POTracker.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy =
        secureCookiePolicy;
});

// ============================================================
// APPLICATION SERVICES
// ============================================================

builder.Services.AddScoped<PendingInvoiceMatcher>();
builder.Services.AddHostedService<PendingInvoiceAutoMatchService>();
builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.Configure<PrivateDocumentStorageOptions>(
    builder.Configuration.GetSection(
        PrivateDocumentStorageOptions.SectionName));

builder.Services.AddSingleton<
    PrivateDocumentStorageService>();

builder.Services.AddSingleton<
    PrivateDocumentReferenceService>();

// ============================================================
// LARGE PO / INVOICE IMPORT SUPPORT
// ============================================================

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 10000;
    options.ValueLengthLimit = int.MaxValue;

    // Maximum combined material-document upload size: 350 MB.
    options.MultipartBodyLengthLimit =
        1024L * 1024L * 350L;

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

builder.Services.Configure<BrotliCompressionProviderOptions>(
    options =>
    {
        options.Level = CompressionLevel.Fastest;
    });

builder.Services.Configure<GzipCompressionProviderOptions>(
    options =>
    {
        options.Level = CompressionLevel.Fastest;
    });

// ============================================================
// DATABASE
// DbContext pooling reduces repeated DbContext creation cost.
//
// Retry execution strategy is intentionally not enabled because
// invoice imports use explicit database transactions.
// ============================================================

builder.Services.AddDbContextPool<ApplicationDbContext>(
    options =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString(
                "DefaultConnection"),
            sqlOptions =>
            {
                sqlOptions.CommandTimeout(60);
            });
    },
    poolSize: 128);

builder.Services.AddHostedService<
    MaterialRmGradeBackfillService>();

// ============================================================
// SESSION
// ============================================================

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);

    options.Cookie.Name = ".POTracker.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy =
        secureCookiePolicy;
});

builder.Services.AddHttpContextAccessor();

// ============================================================
// STOCK MAINTENANCE API
// ============================================================

builder.Services.AddHttpClient("StockApi", client =>
{
    var stockApiBaseUrl =
        builder.Configuration["StockApi:BaseUrl"];

    if (!string.IsNullOrWhiteSpace(stockApiBaseUrl))
    {
        client.BaseAddress = new Uri(stockApiBaseUrl);
    }

    // External stock is optional page enrichment. Keep a short
    // transport ceiling so it can never hold the main application.
    client.Timeout = TimeSpan.FromSeconds(2);
});

// ============================================================
// BUILD APPLICATION
// ============================================================
builder.Services.AddMemoryCache();

builder.Services.AddScoped<
    POTracker.Services.ExternalStockService>();

var app = builder.Build();

// Keep the database schema in sync with the application before serving
// material details.  This is especially important for installations that
// already have POTrackerDB and are upgrading to a build with new material
// fields (for example RMSize); without applying the pending migration the
// material-details query fails with an invalid-column error.
using (var migrationScope = app.Services.CreateScope())
{
    var database = migrationScope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    database.Database.Migrate();
}

// ============================================================
// ERROR HANDLING
// ============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

// ============================================================
// RESPONSE COMPRESSION
// Must be before static files and endpoint execution.
// ============================================================

app.UseResponseCompression();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ============================================================
// BLOCK DIRECT PUBLIC DOCUMENT ACCESS
//
// Business documents must be downloaded only through the
// authenticated /api/private-documents endpoint.
// ============================================================

app.UseMiddleware<PublicUploadBlockingMiddleware>();

// ============================================================
// STATIC FILE CACHING
//
// Shared images and the favicon are cached.
// Uploaded business documents are never browser-cached.
// Angular index.html is not cached.
// Hashed Angular assets are cached for one year.
// ============================================================

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var requestPath =
            context.Context.Request.Path;

        // Never cache uploaded PO, invoice, drawing or document files.
        if (requestPath.StartsWithSegments("/uploads"))
        {
            context.Context.Response.Headers.CacheControl =
                "no-store, no-cache, must-revalidate";

            context.Context.Response.Headers.Pragma =
                "no-cache";

            context.Context.Response.Headers.Expires =
                "0";

            return;
        }

        // Angular production application files.
        if (requestPath.StartsWithSegments("/app"))
        {
            var isAngularDocument =
                requestPath.Value?.EndsWith(
                    ".html",
                    StringComparison.OrdinalIgnoreCase)
                == true;

            context.Context.Response.Headers.CacheControl =
                isAngularDocument
                    ? "no-cache, no-store, must-revalidate"
                    : "public,max-age=31536000,immutable";

            return;
        }

        var isCacheableAsset =
            requestPath.StartsWithSegments("/images") ||
            requestPath.Value?.EndsWith(
                "favicon.ico",
                StringComparison.OrdinalIgnoreCase)
            == true;

        if (isCacheableAsset)
        {
            const int cacheDurationSeconds =
                60 * 60 * 24 * 7;

            context.Context.Response.Headers.CacheControl =
                $"public,max-age={cacheDurationSeconds}";
        }
        else
        {
            context.Context.Response.Headers.CacheControl =
                "no-cache";
        }
    }
});

// ============================================================
// REQUEST PIPELINE
// ============================================================

app.UseRouting();

// Session must execute before authorization and controllers.
app.UseSession();

app.UseAuthorization();

// ============================================================
// API ROUTES
// ============================================================

// Attribute-routed JSON APIs used by Angular.
app.MapControllers();

// Public lightweight health probe.
// This verifies that the web process and request pipeline are running.
// It intentionally does not expose database or document details.
app.MapHealthChecks(
    "/health");

// ============================================================
// ANGULAR ENTRY AND LEGACY URL REDIRECTS
// ============================================================

// The application root enters the Angular dashboard.
// Angular guards redirect signed-out users to /app/login.
app.MapGet(
    "/",
    () => Results.Redirect("/app/dashboard"));

app.MapGet(
    "/app",
    () => Results.Redirect("/app/dashboard"));

// Preserve old bookmarks while serving only Angular pages.
app.MapGet(
    "/Account",
    () => Results.Redirect("/app/login"));

app.MapGet(
    "/Account/Login",
    () => Results.Redirect("/app/login"));

app.MapGet(
    "/Account/Register",
    () => Results.Redirect("/app/register"));

app.MapGet(
    "/Admin",
    () => Results.Redirect("/app/admin/users"));

app.MapGet(
    "/Admin/Users",
    () => Results.Redirect("/app/admin/users"));

app.MapGet(
    "/Admin/Settings",
    () => Results.Redirect("/app/admin/settings"));

app.MapGet(
    "/Home",
    () => Results.Redirect("/app/dashboard"));

app.MapGet(
    "/Home/Index",
    () => Results.Redirect("/app/dashboard"));

app.MapGet(
    "/Home/Privacy",
    () => Results.Redirect("/app/dashboard"));

app.MapGet(
    "/Home/AccessDenied",
    () => Results.Redirect("/app/access-denied"));

// Return Angular index.html when the browser directly opens or
// refreshes an Angular client-side route.
//
// Only routes under /app use this fallback, so API 404
// responses remain unaffected.
app.MapFallbackToFile(
    "app/{*path:nonfile}",
    "app/index.html",
    new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            // The fallback has its own static-file middleware; it does not use
            // the cache policy configured by UseStaticFiles above.
            context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Context.Response.Headers.Pragma = "no-cache";
            context.Context.Response.Headers.Expires = "0";
        }
    });

// ============================================================
// START APPLICATION
// ============================================================


app.Run();
