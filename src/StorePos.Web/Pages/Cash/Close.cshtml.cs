using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Cash;

public class CloseModel(CashService cash) : AppPageModel
{
    public CashSummary Summary { get; private set; } = null!;
    [BindProperty] public decimal Counted { get; set; }
    [BindProperty] public string? Notes { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Summary = await cash.GetSummaryAsync(id);
        if (Summary.Closing.Status == ClosingStatus.Closed) return RedirectToPage("/Cash/Details", new { id });
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (await TryAsync(() => cash.CloseAsync(id, Money.ToCents(Counted), Notes), "Corte de caja guardado."))
            return RedirectToPage("/Cash/Details", new { id });
        Summary = await cash.GetSummaryAsync(id);
        return Page();
    }
}
