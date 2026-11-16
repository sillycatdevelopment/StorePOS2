using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Reporting;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Suppliers;

[Authorize(Policy = Policies.Manager)]
public class DeliveriesModel(DeliveryService deliveries, SupplierService suppliers, ExcelExportService excel, IClock clock) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public int? SupplierId { get; set; }
    [BindProperty(SupportsGet = true)] public PeriodPreset Period { get; set; } = PeriodPreset.ThisMonth;
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;
    public (DateOnly From, DateOnly To) Range { get; private set; }
    public PagedResult<DeliveryRow> Result { get; private set; } = null!;
    public List<Supplier> Suppliers { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Range = Periods.Resolve(Period, clock.Today, From, To);
        Result = await deliveries.SearchAsync(SupplierId, Range.From, Range.To, PageNumber);
        Suppliers = await suppliers.GetAllAsync();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        Range = Periods.Resolve(Period, clock.Today, From, To);
        return Excel(await excel.DeliveriesAsync(Range.From, Range.To, SupplierId), $"entregas_{Range.From:yyyyMMdd}_{Range.To:yyyyMMdd}.xlsx");
    }
}
