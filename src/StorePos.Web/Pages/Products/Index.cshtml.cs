using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Products;

public class IndexModel(ProductService products, SupplierService suppliers) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SupplierId { get; set; }
    [BindProperty(SupportsGet = true)] public string Active { get; set; } = "1";
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedResult<ProductRow> Result { get; private set; } = null!;
    public List<Category> Categories { get; private set; } = new();
    public List<Supplier> Suppliers { get; private set; } = new();

    public async Task OnGetAsync()
    {
        bool? active = Active switch { "1" => true, "0" => false, _ => null };
        Result = await products.SearchAsync(Q, CategoryId, SupplierId, active, PageNumber);
        Categories = await products.GetCategoriesAsync(true);
        Suppliers = await suppliers.GetAllAsync();
    }
}
