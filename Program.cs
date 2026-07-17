using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using POTracker.Data;
using POTracker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Pending invoice auto matching service
builder.Services.AddScoped<PendingInvoiceMatcher>();

// Fix HTTP ERROR 400 for large POImport SaveAll form
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 10000;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 1024L * 1024L * 200L; // 200 MB
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

// Stock Maintenance API connection
builder.Services.AddHttpClient("StockApi", client =>
{
    var stockApiBaseUrl = builder.Configuration["StockApi:BaseUrl"];

    if (!string.IsNullOrWhiteSpace(stockApiBaseUrl))
    {
        client.BaseAddress = new Uri(stockApiBaseUrl);
    }
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();