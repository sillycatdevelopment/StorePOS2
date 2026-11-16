using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Services;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Cash;

public class DetailsModel(CashService cash) : AppPageModel
{
    public CashSummary Summary { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Summary = await cash.GetSummaryAsync(id);
        // Cashiers may see the closing they just made; older closings are for managers.
        if (!IsManager && Summary.Closing.ClosedByUserId != CurrentUser.UserId) return Forbid();
        return Page();
    }
}
