using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Sales;

public class DetailsModel(SalesService sales) : AppPageModel
{
    public Sale Sale { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var s = await sales.GetSaleAsync(id);
        if (s is null) return NotFound();
        Sale = s;
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, string reason)
    {
        await TryAsync(() => sales.CancelSaleAsync(id, reason ?? ""), "Venta cancelada. El inventario regresó a los lotes de cada dueño.", redirecting: true);
        return RedirectToPage(new { id });
    }
}
