using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Pos;

public class IndexModel(SalesService sales, CashService cash, SettingsService settings, OwnerService owners) : AppPageModel
{
    public List<PaymentMethod> Methods { get; private set; } = new();
    public DailyClosing? OpenDay { get; private set; }
    public AllocationMode Mode { get; private set; }
    public List<Owner> Owners { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Methods = await sales.GetPaymentMethodsAsync();
        OpenDay = await cash.GetOpenSessionAsync();
        Mode = await settings.GetAllocationModeAsync();
        Owners = await owners.GetAllAsync(includeInactive: false);
    }

    /// <summary>Exact barcode lookup (scanner). Returns 404 JSON when not found.</summary>
    public async Task<IActionResult> OnGetLookupAsync(string code)
    {
        var p = await sales.LookupBarcodeAsync(code ?? "");
        return p is null ? new JsonResult(new { found = false }) : new JsonResult(new { found = true, product = p });
    }

    public async Task<IActionResult> OnGetSearchAsync(string q) =>
        new JsonResult(await sales.SearchAsync(q ?? "", 20));

    public async Task<IActionResult> OnPostCompleteAsync([FromBody] SaleRequest request)
    {
        try
        {
            int id = await sales.CompleteSaleAsync(request);
            var sale = await sales.GetSaleAsync(id);
            return new JsonResult(new
            {
                ok = true, id, total = Money.Format(sale!.TotalCents), change = Money.Format(sale.ChangeCents),
                changeCents = sale.ChangeCents
            });
        }
        catch (DomainException ex)
        {
            return new JsonResult(new { ok = false, error = ex.Message });
        }
    }
}
