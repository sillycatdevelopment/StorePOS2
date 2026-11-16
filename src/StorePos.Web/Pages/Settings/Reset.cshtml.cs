using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Settings;

/// <summary>Clears test data. Always makes a backup first and requires typing BORRAR.</summary>
[Authorize(Policy = Policies.Admin)]
public class ResetModel(DeletionService deletion) : AppPageModel
{
    private const string Word = "BORRAR";

    public void OnGet() { }

    public async Task<IActionResult> OnPostOperationsAsync(string confirm)
    {
        if (!string.Equals(confirm?.Trim(), Word, StringComparison.OrdinalIgnoreCase))
        {
            Fail($"Para confirmar escribe {Word}.");
            return RedirectToPage();
        }
        string backup = "";
        if (await TryAsync(async () => backup = await deletion.ResetOperationsAsync(), null, redirecting: true))
            Ok($"Listo: se borraron ventas, inventario, cortes, entregas, listas y solicitudes. Respaldo previo: {backup}");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEverythingAsync(string confirm)
    {
        if (!string.Equals(confirm?.Trim(), Word, StringComparison.OrdinalIgnoreCase))
        {
            Fail($"Para confirmar escribe {Word}.");
            return RedirectToPage();
        }
        if (!await TryAsync(() => deletion.ResetEverythingAsync(), null, redirecting: true))
            return RedirectToPage();
        FirstRunMiddleware.Reset();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/Setup");
    }
}
