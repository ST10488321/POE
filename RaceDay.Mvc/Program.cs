using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC with automatic anti-forgery validation on every POST
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();

// Server-side session: keeps the logged-in user, role and the API session between pages
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".RaceDay.Mvc.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromMinutes(30);
});

// The ONLY way this app gets data: the Part 2 API.
// UseCookies = false so the API's session cookie is never shared between different users.
builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });

var app = builder.Build();

// Same culture everywhere (local and Docker) so dates and decimals behave identically
var culture = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = new[] { culture },
    SupportedUICultures = new[] { culture }
});

// Friendly error pages - never raw exceptions
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
