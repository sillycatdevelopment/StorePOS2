using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Settings;

[Authorize(Policy = Policies.Admin)]
public class IndexModel(SettingsService settings, SalesService sales, DeletionService deletion) : AppPageModel
{
    public Dictionary<string, string> Values { get; private set; } = new();
    public List<PaymentMethod> Methods { get; private set; } = new();

    public async Task OnGetAsync()
    {
        foreach (var key in SettingsService.Defaults.Keys) Values[key] = await settings.GetAsync(key);
        Methods = await sales.GetPaymentMethodsAsync(true);
    }

    public async Task<IActionResult> OnPostAsync(string storeName, string receiptFooter, AllocationMode allocationMode,
        int lowStockMargin, int safetyDays, int defaultCycleDays, int historyDays)
    {
        await TryAsync(() => settings.SetManyAsync(new Dictionary<string, string>
        {
            [SettingKeys.StoreName] = string.IsNullOrWhiteSpace(storeName) ? "Mi Tienda" : storeName,
            [SettingKeys.ReceiptFooter] = receiptFooter ?? "",
            [SettingKeys.AllocationMode] = allocationMode.ToString(),
            [SettingKeys.LowStockMarginPercent] = Math.Clamp(lowStockMargin, 0, 500).ToString(),
            [SettingKeys.SafetyDays] = Math.Clamp(safetyDays, 0, 60).ToString(),
            [SettingKeys.DefaultCycleDays] = Math.Clamp(defaultCycleDays, 1, 60).ToString(),
            [SettingKeys.HistoryDays] = Math.Clamp(historyDays, 7, 180).ToString(),
        }), "Configuración guardada.", redirecting: true);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMethodAsync(int? id, string name, bool isCash, bool isActive, int sortOrder)
    {
        await TryAsync(() => sales.SavePaymentMethodAsync(id, name, isCash, isActive, sortOrder), "Forma de pago guardada.", redirecting: true);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteMethodAsync(int id)
    {
        await TryAsync(() => deletion.DeletePaymentMethodAsync(id), "Forma de pago eliminada.", redirecting: true);
        return RedirectToPage();
    }
}
