using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Reporting;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Reports;

[Authorize(Policy = Policies.Manager)]
public class ProductModel(TrendService trends, ProductService products, InventoryService inventory, IClock clock) : AppPageModel
{
    public Product Product { get; private set; } = null!;
    public ProductDemand? Demand { get; private set; }
    public List<(int Year, int Month, long Qty, long RevenueCents)> Monthly { get; private set; } = new();
    public long[] Weekday { get; private set; } = new long[7];
    public long Stock { get; private set; }
    public StockAdvice Advice { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await products.GetAsync(id);
        if (p is null) return NotFound();
        Product = p;
        var today = clock.Today;
        (await trends.GetDemandAsync(today)).TryGetValue(id, out var d);
        Demand = d;
        Monthly = await trends.GetMonthlySeriesAsync(id, today, 24);
        Weekday = await trends.GetWeekdayProfileAsync(id, today);
        Stock = await inventory.GetStockAsync(id);
        if (d is not null) Advice = TrendService.Advise(d, Stock, today);
        return Page();
    }
}
