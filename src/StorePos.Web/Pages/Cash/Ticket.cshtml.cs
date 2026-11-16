using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Services;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Cash;

/// <summary>Cash day summary (corte de caja) formatted for an 80 mm ticket printer.</summary>
public class TicketModel(CashService cash, SettingsService settings) : AppPageModel
{
    public CashSummary Summary { get; private set; } = null!;
    public string StoreName { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Summary = await cash.GetSummaryAsync(id);
        var c = Summary.Closing;
        // Same rule as the closing details page: cashiers only see a closing they closed (or the open day).
        if (!IsManager && c.ClosedByUserId != CurrentUser.UserId && c.Status == StorePos.Core.Models.ClosingStatus.Closed) return Forbid();
        StoreName = await settings.GetAsync(SettingKeys.StoreName);
        return Page();
    }
}
