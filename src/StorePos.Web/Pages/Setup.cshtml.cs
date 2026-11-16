using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages;

/// <summary>First run only: creates the administrator, the store name and the first owner.</summary>
public class SetupModel(UserService users, OwnerService owners, SettingsService settings) : AppPageModel
{
    public async Task<IActionResult> OnGetAsync() => await users.AnyUsersAsync() ? Redirect("/") : Page();

    public async Task<IActionResult> OnPostAsync(string storeName, string displayName, string username, string password, string ownerName)
    {
        if (await users.AnyUsersAsync()) return Redirect("/");
        var ok = await TryAsync(async () =>
        {
            await users.CreateAsync(username, displayName, password, StorePos.Core.Models.UserRole.Administrator);
            await owners.SaveAsync(null, ownerName, null, null, true);
            await settings.SetManyAsync(new Dictionary<string, string> { [SettingKeys.StoreName] = storeName }, silent: true);
        });
        if (!ok) return Page();
        var user = await users.ValidateAsync(username, password);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AuthHelper.CreatePrincipal(user!));
        Ok("Listo. Ahora registra tus productos y abre la caja para empezar a vender.");
        return Redirect("/");
    }
}
