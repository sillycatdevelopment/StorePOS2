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
public class IndexModel(ReportService reports, OwnerService owners, IClock clock) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public PeriodPreset Period { get; set; } = PeriodPreset.ThisMonth;
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? OwnerId { get; set; }
    [BindProperty(SupportsGet = true)] public string Group { get; set; } = "day";

    public (DateOnly From, DateOnly To) Range { get; private set; }
    public PeriodSummary Summary { get; private set; } = null!;
    public List<GroupTotal> ByPeriod { get; private set; } = new();
    public List<GroupTotal> ByOwner { get; private set; } = new();
    public List<GroupTotal> ByCategory { get; private set; } = new();
    public List<GroupTotal> BySupplier { get; private set; } = new();
    public List<GroupTotal> ByPayment { get; private set; } = new();
    public List<ProductSales> Best { get; private set; } = new();
    public List<ProductSales> Worst { get; private set; } = new();
    public List<Owner> Owners { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Range = Periods.Resolve(Period, clock.Today, From, To);
        var (f, t) = Range;
        Summary = await reports.GetSummaryAsync(f, t, OwnerId);
        ByPeriod = Group switch
        {
            "week" => (await reports.ByDayAsync(f, t, OwnerId))
                .GroupBy(d => d.Date!.Value.AddDays(-(((int)d.Date.Value.DayOfWeek + 6) % 7)))
                .Select(g => new GroupTotal("Semana del " + F.D(g.Key), g.Sum(x => x.RevenueCents), g.Sum(x => x.CostCents), g.Sum(x => x.SaleCount), 0) { Date = g.Key })
                .ToList(),
            "month" => await reports.ByMonthAsync(f, t, OwnerId),
            "year" => await reports.ByYearAsync(f, t, OwnerId),
            _ => await reports.ByDayAsync(f, t, OwnerId)
        };
        if (OwnerId is null) ByOwner = await reports.ByOwnerAsync(f, t);
        ByCategory = await reports.ByCategoryAsync(f, t, OwnerId);
        BySupplier = await reports.BySupplierAsync(f, t, OwnerId);
        ByPayment = await reports.ByPaymentMethodAsync(f, t, OwnerId);
        Best = await reports.ProductRankingAsync(f, t, OwnerId, best: true, take: 15);
        Worst = await reports.ProductRankingAsync(f, t, OwnerId, best: false, take: 15);
        Owners = await owners.GetAllAsync();
    }
}
