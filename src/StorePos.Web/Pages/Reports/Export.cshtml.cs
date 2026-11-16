using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Reporting;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Reports;

/// <summary>All Excel exports in one place, each with date and owner filters.</summary>
[Authorize(Policy = Policies.Manager)]
public class ExportModel(ExcelExportService excel, OwnerService owners, SupplierService suppliers, StockingService stocking, IClock clock) : AppPageModel
{
    public List<Owner> Owners { get; private set; } = new();
    public List<Supplier> Suppliers { get; private set; } = new();
    public List<StockingList> Lists { get; private set; } = new();
    public DateOnly Today { get; private set; }

    public async Task OnGetAsync()
    {
        Today = clock.Today;
        Owners = await owners.GetAllAsync();
        Suppliers = await suppliers.GetAllAsync();
        Lists = (await stocking.SearchAsync(null, 1, 30)).Items.ToList();
    }

    private (DateOnly, DateOnly) R(PeriodPreset period, DateOnly? from, DateOnly? to) => Periods.Resolve(period, clock.Today, from, to);
    private static string Stamp(DateOnly f, DateOnly t) => f == t ? $"{f:yyyyMMdd}" : $"{f:yyyyMMdd}_{t:yyyyMMdd}";

    public async Task<IActionResult> OnGetSalesAsync(PeriodPreset period, DateOnly? from, DateOnly? to, int? ownerId, string grouping = "day")
    {
        var (f, t) = R(period, from, to);
        return Excel(await excel.SalesAsync(f, t, ownerId, grouping), $"ventas_{Stamp(f, t)}.xlsx");
    }

    public async Task<IActionResult> OnGetProfitAsync(PeriodPreset period, DateOnly? from, DateOnly? to, int? ownerId)
    {
        var (f, t) = R(period, from, to);
        return Excel(await excel.ProfitAsync(f, t, ownerId), $"ganancias_{Stamp(f, t)}.xlsx");
    }

    public async Task<IActionResult> OnGetOwnerAsync(PeriodPreset period, DateOnly? from, DateOnly? to, int ownerId)
    {
        var (f, t) = R(period, from, to);
        return Excel(await excel.OwnerAsync(ownerId, f, t), $"dueno_{ownerId}_{Stamp(f, t)}.xlsx");
    }

    public async Task<IActionResult> OnGetInventoryAsync(StockFilter status, int? ownerId)
    {
        var title = status switch { StockFilter.Low => "Productos con existencia baja", StockFilter.Out => "Productos agotados", _ => "Inventario" };
        return Excel(await excel.InventoryAsync(new InventoryFilter { Status = status, OwnerId = ownerId }, title), $"inventario_{status}_{clock.Today:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> OnGetDeliveriesAsync(PeriodPreset period, DateOnly? from, DateOnly? to, int? supplierId)
    {
        var (f, t) = R(period, from, to);
        return Excel(await excel.DeliveriesAsync(f, t, supplierId), $"entregas_{Stamp(f, t)}.xlsx");
    }

    public async Task<IActionResult> OnGetClosingsAsync(PeriodPreset period, DateOnly? from, DateOnly? to)
    {
        var (f, t) = R(period, from, to);
        return Excel(await excel.ClosingsAsync(f, t), $"cortes_{Stamp(f, t)}.xlsx");
    }

    public async Task<IActionResult> OnGetStockingAsync(int listId) =>
        Excel(await excel.StockingListAsync(listId), $"lista_surtido_{listId}.xlsx");

    public async Task<IActionResult> OnGetRequestsAsync(RequestStatus? status) =>
        Excel(await excel.CustomerRequestsAsync(status), $"solicitudes_{clock.Today:yyyyMMdd}.xlsx");
}
