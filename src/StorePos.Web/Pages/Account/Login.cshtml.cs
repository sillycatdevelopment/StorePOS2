using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Account;

public class LoginModel(UserService users) : AppPageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.ValidateAsync(Username ?? "", Password ?? "");
        if (user is null)
        {
            // Small fixed delay slows down password guessing from the LAN.
            await Task.Delay(700);
            ErrorMessage = "Usuario o contraseña incorrectos.";
            return Page();
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AuthHelper.CreatePrincipal(user));
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }
}
