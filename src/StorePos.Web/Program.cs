using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using StorePos.Core;
using StorePos.Core.Data;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Web.Infrastructure;

// Start from the folder that contains wwwroot/appsettings.json (the provided Iniciar-StorePOS.bat does this).
var builder = WebApplication.CreateBuilder(args);

// ---- Database location (relative paths are next to the executable) ----
var dbPath = builder.Configuration["StorePos:DatabasePath"] ?? "data/storepos.db";
if (!Path.IsPathRooted(dbPath)) dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
var connectionString = $"Data Source={dbPath};Cache=Private;Default Timeout=30";

// ---- Listen on the LAN (default http://0.0.0.0:5000) unless ASPNETCORE_URLS is set ----
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls(builder.Configuration["StorePos:Urls"] ?? "http://0.0.0.0:5000");

builder.Services.AddStorePosCore(connectionString);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddHostedService<AutoBackupHostedService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Denied";
        o.Cookie.Name = "storepos.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = AuthHelper.ValidateAsync;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Manager, p => p.RequireRole(nameof(StorePos.Core.Models.UserRole.Manager), nameof(StorePos.Core.Models.UserRole.Administrator)))
    .AddPolicy(Policies.Admin, p => p.RequireRole(nameof(StorePos.Core.Models.UserRole.Administrator)));

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Account/Denied");
    o.Conventions.AllowAnonymousToPage("/Setup");
    o.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

var app = builder.Build();

// ---- Create / upgrade the database and seed reference data ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
    await DbSetup.InitializeAsync(db, scope.ServiceProvider.GetRequiredService<IClock>().Now);
}

var mx = new CultureInfo("es-MX");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(mx),
    SupportedCultures = new[] { mx },
    SupportedUICultures = new[] { mx },
    RequestCultureProviders = new List<IRequestCultureProvider>()   // always es-MX
});

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public,max-age=604800"
});
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<FirstRunMiddleware>();
app.UseAuthorization();
app.MapRazorPages();

app.Logger.LogInformation("StorePOS 2.0 en {Urls} — base de datos: {Db}", string.Join(", ", app.Urls), dbPath);
app.Run();
