using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Cash;

public class IndexModel(CashService cash) : AppPageModel
{
    public CashSummary? Summary { get; private set; }

    public async Task OnGetAsync()
    {
        var open = await cash.GetOpenSessionAsync();
        if (open is not null) Summary = await cash.GetSummaryAsync(open.Id);
    }

    public async Task<IActionResult> OnPostOpenAsync(decimal openingCash)
    {
        await TryAsync(() => cash.OpenAsync(Money.ToCents(openingCash)), "Caja abierta. ¡Buena venta!", redirecting: true);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMovementAsync(CashMovementType type, decimal amount, bool affectsCash, string description)
    {
        await TryAsync(() => cash.AddMovementAsync(type, Money.ToCents(amount), affectsCash, description ?? ""), "Movimiento registrado.", redirecting: true);
        return RedirectToPage();
    }
}
