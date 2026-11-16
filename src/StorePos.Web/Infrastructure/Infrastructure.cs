using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;

namespace StorePos.Web.Infrastructure;

public static class Policies
{
    public const string Manager = "Manager";
    public const string Admin = "Admin";
}

/// <summary>Current user taken from the authentication cookie.</summary>
public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? P => accessor.HttpContext?.User;
    public bool IsAuthenticated => P?.Identity?.IsAuthenticated == true;
    public int? UserId => int.TryParse(P?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string UserName => P?.FindFirstValue(ClaimTypes.Name) ?? "sistema";
    public UserRole Role => Enum.TryParse<UserRole>(P?.FindFirstValue(ClaimTypes.Role), out var r) ? r : UserRole.Cashier;
}

public static class AuthHelper
{
    public static ClaimsPrincipal CreatePrincipal(User u) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
        new Claim(ClaimTypes.Name, u.DisplayName),
        new Claim(ClaimTypes.Role, u.Role.ToString()),
    }, CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>Signs the user out at once if they were deactivated or their role changed (one PK lookup per request).</summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext ctx)
    {
        if (!int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) { ctx.RejectPrincipal(); return; }
        var db = ctx.HttpContext.RequestServices.GetRequiredService<StoreDbContext>();
        var u = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.IsActive, x.Role }).FirstOrDefaultAsync();
        if (u is null || !u.IsActive || u.Role.ToString() != ctx.Principal!.FindFirstValue(ClaimTypes.Role))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

/// <summary>Until the first administrator exists, every page redirects to /Setup.</summary>
public class FirstRunMiddleware(RequestDelegate next)
{
    private static volatile bool _hasUsers;

    public async Task InvokeAsync(HttpContext ctx, UserService users)
    {
        if (!_hasUsers)
        {
            _hasUsers = await users.AnyUsersAsync();
            var path = ctx.Request.Path;
            if (!_hasUsers && !path.StartsWithSegments("/Setup") && !path.StartsWithSegments("/css") && !path.StartsWithSegments("/js"))
            {
                ctx.Response.Redirect("/Setup");
                return;
            }
        }
        await next(ctx);
    }
}

/// <summary>Base class for pages: flash messages and safe execution of business operations.</summary>
public abstract class AppPageModel : PageModel
{
    /// <summary>Error shown on this same response (when the handler returns Page()).</summary>
    public string? ErrorMessage { get; set; }

    protected ICurrentUser CurrentUser => HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
    public bool IsManager => CurrentUser.Role.AtLeast(UserRole.Manager);
    public bool IsAdmin => CurrentUser.Role.AtLeast(UserRole.Administrator);

    /// <summary>Message for the next page (use before a redirect).</summary>
    protected void Ok(string message) => TempData["Flash"] = message;
    protected void Fail(string message) => TempData["FlashError"] = message;

    /// <summary>
    /// Runs an operation. On success stores <paramref name="success"/> for the next page.
    /// On a business-rule error stores the message in <see cref="ErrorMessage"/> (and, if
    /// <paramref name="redirecting"/>, for the next page) instead of crashing.
    /// </summary>
    protected async Task<bool> TryAsync(Func<Task> action, string? success = null, bool redirecting = false)
    {
        try
        {
            await action();
            if (success is not null) Ok(success);
            return true;
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
            if (redirecting) Fail(ex.Message);
            return false;
        }
    }

    protected static long Cents(decimal pesos) => Money.ToCents(pesos);

    protected IActionResult Excel(byte[] bytes, string name) =>
        File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
}

/// <summary>Short formatting helpers for Razor views (es-MX).</summary>
public static class F
{
    public static string M(long cents) => Money.Format(cents);
    public static string Q(long qty, UnitType unit) => Qty.Format(qty, unit);
    public static decimal QIn(long qty, UnitType unit) => Qty.ToDisplay(qty, unit);
    public static string D(DateTime d) => d.ToString("dd/MM/yyyy", Money.MX);
    public static string D(DateOnly d) => d.ToString("dd/MM/yyyy", Money.MX);
    public static string DT(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", Money.MX);
    public static string DT(DateTime? d) => d is null ? "—" : DT(d.Value);
    public static string Long(DateOnly d) => d.ToString("dddd d 'de' MMMM", Money.MX);
    public static string Pct(double v) => v.ToString("P1", Money.MX);
    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");
    public static decimal P(long cents) => Money.ToPesos(cents);
}

public class PagerModel
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    /// <summary>Current query string values, so filters are kept when paging.</summary>
    public Dictionary<string, string?> Query { get; init; } = new();

    public static PagerModel From<T>(StorePos.Core.ViewModels.PagedResult<T> r, HttpRequest req) => new()
    {
        Page = r.Page, TotalPages = r.TotalPages, TotalCount = r.TotalCount,
        Query = req.Query.Where(q => !q.Key.Equals("p", StringComparison.OrdinalIgnoreCase))
                         .ToDictionary(q => q.Key, q => (string?)q.Value.ToString())
    };

    public string Url(int page)
    {
        var parts = Query.Where(kv => !string.IsNullOrEmpty(kv.Value))
                         .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}").ToList();
        parts.Add($"p={page}");
        return "?" + string.Join("&", parts);
    }
}
