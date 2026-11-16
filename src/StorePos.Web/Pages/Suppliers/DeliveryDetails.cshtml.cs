using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Suppliers;

[Authorize(Policy = Policies.Manager)]
public class DeliveryDetailsModel(DeliveryService deliveries) : AppPageModel
{
    public Delivery Delivery { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var d = await deliveries.GetAsync(id);
        if (d is null) return NotFound();
        Delivery = d;
        return Page();
    }
}
