using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Reporting;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Cash;

[Authorize(Policy = Policies.Manager)]
public class HistoryModel(CashService cash, ExcelExportService excel, IClock clock) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public PeriodPreset Period { get; set; } = PeriodPreset.ThisMonth;
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;
    public (DateOnly From, DateOnly To) Range { get; private set; }
    public PagedResult<DailyClosing> Result { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Range = Periods.Resolve(Period, clock.Today, From, To);
        Result = await cash.GetHistoryAsync(Range.From, Range.To, PageNumber);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        Range = Periods.Resolve(Period, clock.Today, From, To);
        return Excel(await excel.ClosingsAsync(Range.From, Range.To), $"cortes_{Range.From:yyyyMMdd}_{Range.To:yyyyMMdd}.xlsx");
    }
}
