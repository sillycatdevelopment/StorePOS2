using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Sales;

public class IndexModel(SalesService sales, OwnerService owners) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public SaleStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int? PaymentMethodId { get; set; }
    [BindProperty(SupportsGet = true)] public int? OwnerId { get; set; }
    [BindProperty(SupportsGet = true)] public int? Folio { get; set; }
    [BindProperty(SupportsGet = true)] public string? Product { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedResult<SaleRow> Result { get; private set; } = null!;
    public List<PaymentMethod> Methods { get; private set; } = new();
    public List<Owner> Owners { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Result = await sales.SearchSalesAsync(new SaleFilter
        {
            From = From, To = To, Status = Status, PaymentMethodId = PaymentMethodId, OwnerId = OwnerId, SaleId = Folio, Product = Product
        }, PageNumber);
        Methods = await sales.GetPaymentMethodsAsync(true);
        Owners = await owners.GetAllAsync();
    }
}
