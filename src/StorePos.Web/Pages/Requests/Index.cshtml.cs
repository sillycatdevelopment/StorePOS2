using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Requests;

/// <summary>Very quick capture of products customers ask for but the store doesn't have.</summary>
public class IndexModel(CustomerRequestService requests, ProductService products) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public RequestStatus? Status { get; set; } = RequestStatus.Pending;
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;
    public PagedResult<CustomerRequest> Result { get; private set; } = null!;
    public List<Category> Categories { get; private set; } = new();

    public async Task OnGetAsync(string? all)
    {
        if (all == "1") Status = null;
        Result = await requests.SearchAsync(Status, Q, PageNumber);
        Categories = await products.GetCategoriesAsync();
    }

    public async Task<IActionResult> OnPostAddAsync(string productName, string? barcode, int? categoryId, int quantity, string? notes)
    {
        CustomerRequest? r = null;
        await TryAsync(async () => r = await requests.AddAsync(productName, barcode, categoryId, Math.Max(0, quantity), notes), null, redirecting: true);
        if (r is not null) Ok(r.RequestCount == 1 ? $"Registrado: {r.ProductName}." : $"{r.ProductName}: ya lo han pedido {r.RequestCount} veces.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, RequestStatus newStatus)
    {
        if (!IsManager) return Forbid();
        await TryAsync(() => requests.SetStatusAsync(id, newStatus), "Estado actualizado.", redirecting: true);
        return RedirectToPage();
    }
}
