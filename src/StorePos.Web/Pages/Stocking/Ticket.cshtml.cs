using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Stocking;

/// <summary>Stocking list formatted for an 80 mm ticket printer: product, quantity, estimated cost.</summary>
[Authorize(Policy = Policies.Manager)]
public class TicketModel(StockingService stocking, SettingsService settings) : AppPageModel
{
    public StockingList List { get; private set; } = null!;
    public string StoreName { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var l = await stocking.GetAsync(id);
        if (l is null) return NotFound();
        List = l;
        StoreName = await settings.GetAsync(SettingKeys.StoreName);
        return Page();
    }
}
