using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Sales;

public class ReceiptModel(SalesService sales, SettingsService settings) : AppPageModel
{
    public Sale Sale { get; private set; } = null!;
    public string StoreName { get; private set; } = "";
    public string Footer { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var s = await sales.GetSaleAsync(id);
        if (s is null) return NotFound();
        Sale = s;
        StoreName = await settings.GetAsync(SettingKeys.StoreName);
        Footer = await settings.GetAsync(SettingKeys.ReceiptFooter);
        return Page();
    }
}
