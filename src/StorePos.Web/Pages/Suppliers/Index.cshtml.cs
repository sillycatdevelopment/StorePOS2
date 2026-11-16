using Microsoft.AspNetCore.Authorization;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Suppliers;

[Authorize(Policy = Policies.Manager)]
public class IndexModel(SupplierService suppliers, ProductService products, IClock clock) : AppPageModel
{
    public List<Supplier> Suppliers { get; private set; } = new();
    public List<UpcomingDelivery> Upcoming { get; private set; } = new();
    public Dictionary<int, int> ProductCounts { get; private set; } = new();
    public DateOnly Today { get; private set; }

    public async Task OnGetAsync()
    {
        Today = clock.Today;
        Suppliers = await suppliers.GetAllAsync();
        Upcoming = await suppliers.GetUpcomingAsync(Today, 7);
        foreach (var s in Suppliers)
            ProductCounts[s.Id] = (await products.SearchAsync(null, null, s.Id, true, 1, 1)).TotalCount;
    }
}
