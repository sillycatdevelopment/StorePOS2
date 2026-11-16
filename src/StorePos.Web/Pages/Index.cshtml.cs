using StorePos.Core.Models;
using StorePos.Core.Reporting;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages;

public class IndexModel(ReportService reports, InventoryService inventory, CustomerRequestService requests,
                        SupplierService suppliers, TrendService trends, SalesService sales, CashService cash, IClock clock) : AppPageModel
{
    public PeriodSummary Today { get; private set; } = null!;
    public (long Cost, long Retail) InventoryValue { get; private set; }
    public int OutCount { get; private set; }
    public int LowCount { get; private set; }
    public int PendingRequests { get; private set; }
    public List<CustomerRequest> TopRequests { get; private set; } = new();
    public List<UpcomingDelivery> Upcoming { get; private set; } = new();
    public List<ProductDemand> TrendUp { get; private set; } = new();
    public List<ProductDemand> TrendDown { get; private set; } = new();
    public List<SaleRow> Latest { get; private set; } = new();
    public DailyClosing? OpenDay { get; private set; }
    public CashSummary? Cash { get; private set; }
    public DateOnly Date { get; private set; }

    public async Task OnGetAsync()
    {
        Date = clock.Today;
        Today = await reports.GetSummaryAsync(Date, Date);
        InventoryValue = await inventory.GetInventoryValueAsync();
        (OutCount, LowCount) = await inventory.CountOutAndLowAsync();
        PendingRequests = await requests.CountPendingAsync();
        TopRequests = await requests.GetTopPendingAsync(5);
        Upcoming = await suppliers.GetUpcomingAsync(Date, 7);
        var demand = (await trends.GetDemandAsync(Date)).Values.Where(d => d.Reliable).ToList();
        TrendUp = demand.Where(d => d.Trend == TrendDirection.Up).OrderByDescending(d => d.ChangeRatio ?? 9).Take(5).ToList();
        TrendDown = demand.Where(d => d.Trend == TrendDirection.Down).OrderBy(d => d.ChangeRatio).Take(5).ToList();
        Latest = await sales.GetLatestAsync(10);
        OpenDay = await cash.GetOpenSessionAsync();
        if (OpenDay is not null) Cash = await cash.GetSummaryAsync(OpenDay.Id);
    }
}
