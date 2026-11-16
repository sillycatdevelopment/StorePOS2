using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Products;

[Authorize(Policy = Policies.Manager)]
public class CategoriesModel(ProductService products, DeletionService deletion) : AppPageModel
{
    public List<Category> Categories { get; private set; } = new();

    public async Task OnGetAsync() => Categories = await products.GetCategoriesAsync(true);

    public async Task<IActionResult> OnPostAsync(int? id, string name, bool isActive = true)
    {
        await TryAsync(() => products.SaveCategoryAsync(id, name, isActive), "Categoría guardada.", redirecting: true);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await TryAsync(() => deletion.DeleteCategoryAsync(id), "Categoría eliminada. Sus productos quedaron sin categoría.", redirecting: true);
        return RedirectToPage();
    }
}
