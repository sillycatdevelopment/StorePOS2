using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Account;

public class PasswordModel(UserService users) : AppPageModel
{
    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string current, string @new)
    {
        if (await TryAsync(() => users.ChangeOwnPasswordAsync(current ?? "", @new ?? ""), "Contraseña actualizada."))
            return Redirect("/");
        return Page();
    }
}
