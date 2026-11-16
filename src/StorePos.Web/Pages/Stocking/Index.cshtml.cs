using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Stocking;

[Authorize(Policy = Policies.Manager)]
public class IndexModel(StockingService stocking, SupplierService suppliers, OwnerService owners, CustomerRequestService requests) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public int? SupplierId { get; set; }
    [BindProperty(SupportsGet = true)] public int? OwnerId { get; set; }
    public List<ReorderSuggestion> Suggestions { get; private set; } = new();
    public List<StockingList> Lists { get; private set; } = new();
    public List<Supplier> Suppliers { get; private set; } = new();
    public List<Owner> Owners { get; private set; } = new();
    public List<CustomerRequest> NewProductRequests { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Suggestions = (await stocking.GetSuggestionsAsync())
            .Where(s => (SupplierId == null || s.SupplierId == SupplierId) && (OwnerId == null || s.OwnerId == OwnerId)).ToList();
        Lists = (await stocking.SearchAsync(null, 1, 20)).Items.ToList();
        Suppliers = await suppliers.GetAllAsync(false);
        Owners = await owners.GetAllAsync(false);
        NewProductRequests = (await requests.GetTopPendingAsync(50)).Where(r => r.ProductId == null).ToList();
    }

    public async Task<IActionResult> OnPostGenerateAsync(string? name, StockingGroupBy groupBy, int? supplierId, int? ownerId, bool includeRequests)
    {
        int id = 0;
        if (await TryAsync(async () => id = await stocking.GenerateListAsync(name, groupBy, supplierId, ownerId, includeRequests),
                           "Lista generada. Revisa y ajusta las cantidades antes de comprar.", redirecting: true))
            return RedirectToPage("/Stocking/Edit", new { id });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEmptyAsync(string name, StockingGroupBy groupBy)
    {
        int id = await stocking.CreateEmptyAsync(name, groupBy);
        return RedirectToPage("/Stocking/Edit", new { id });
    }
}
