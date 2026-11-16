using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Reporting;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Owners;

[Authorize(Policy = Policies.Manager)]
public class IndexModel(OwnerService owners, ExcelExportService excel, IClock clock) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public PeriodPreset Period { get; set; } = PeriodPreset.ThisMonth;
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    public (DateOnly From, DateOnly To) Range { get; private set; }
    public List<OwnerStats> Stats { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Range = Periods.Resolve(Period, clock.Today, From, To);
        Stats = await owners.GetStatsAsync(Range.From, Range.To);
    }

    public async Task<IActionResult> OnGetExportAsync(int ownerId)
    {
        Range = Periods.Resolve(Period, clock.Today, From, To);
        var bytes = await excel.OwnerAsync(ownerId, Range.From, Range.To);
        return Excel(bytes, $"dueno_{ownerId}_{Range.From:yyyyMMdd}_{Range.To:yyyyMMdd}.xlsx");
    }
}
