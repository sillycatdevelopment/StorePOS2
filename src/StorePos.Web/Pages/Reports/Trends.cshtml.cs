using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Reporting;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Reports;

[Authorize(Policy = Policies.Manager)]
public class TrendsModel(TrendService trends, IClock clock) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string Show { get; set; } = "all";
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;
    public PagedResult<ProductTrendRow> Result { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        IEnumerable<ProductTrendRow> rows = await trends.GetTrendTableAsync(clock.Today);
        if (!string.IsNullOrWhiteSpace(Q)) { var n = TextNorm.Normalize(Q); rows = rows.Where(r => TextNorm.Normalize(r.Demand.Name).Contains(n)); }
        rows = Show switch
        {
            "up" => rows.Where(r => r.Demand.Trend == TrendDirection.Up),
            "down" => rows.Where(r => r.Demand.Trend == TrendDirection.Down),
            "increase" => rows.Where(r => r.Advice == StockAdvice.Increase),
            "reduce" => rows.Where(r => r.Advice is StockAdvice.Reduce or StockAdvice.Stop),
            "seasonal" => rows.Where(r => r.Demand.SeasonalReliable),
            _ => rows
        };
        Result = PagedResult<ProductTrendRow>.FromList(rows.ToList(), PageNumber, 50);
    }
}
