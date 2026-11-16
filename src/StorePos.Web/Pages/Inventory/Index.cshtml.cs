using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Reporting;
using StorePos.Core.Services;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Inventory;

public class IndexModel(InventoryService inventory, ProductService products, OwnerService owners,
                        SupplierService suppliers, ExcelExportService excel) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public int? OwnerId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SupplierId { get; set; }
    [BindProperty(SupportsGet = true)] public StockFilter Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedResult<InventoryRow> Result { get; private set; } = null!;
    public (long Cost, long Retail) Value { get; private set; }
    public List<Category> Categories { get; private set; } = new();
    public List<Owner> Owners { get; private set; } = new();
    public List<Supplier> Suppliers { get; private set; } = new();

    private InventoryFilter Filter => new() { Text = Q, CategoryId = CategoryId, OwnerId = OwnerId, SupplierId = SupplierId, Status = Status };

    public async Task OnGetAsync()
    {
        Result = await inventory.GetInventoryAsync(Filter, PageNumber);
        Value = await inventory.GetInventoryValueAsync(OwnerId);
        Categories = await products.GetCategoriesAsync(true);
        Owners = await owners.GetAllAsync();
        Suppliers = await suppliers.GetAllAsync();
    }

    public async Task<IActionResult> OnGetExportAsync(string kind)
    {
        if (!IsManager) return Forbid();
        var f = Filter;
        string title, file;
        switch (kind)
        {
            case "low": f.Status = StockFilter.Low; title = "Productos con existencia baja"; file = "existencia_baja"; break;
            case "out": f.Status = StockFilter.Out; title = "Productos agotados"; file = "agotados"; break;
            default: title = "Inventario"; file = "inventario"; break;
        }
        var bytes = await excel.InventoryAsync(f, title);
        return Excel(bytes, $"{file}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }
}
