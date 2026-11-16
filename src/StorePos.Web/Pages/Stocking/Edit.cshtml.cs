using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Stocking;

[Authorize(Policy = Policies.Manager)]
public class EditModel(StockingService stocking, SupplierService suppliers, OwnerService owners, ProductService products, DeletionService deletion) : AppPageModel
{
    public class Row
    {
        public int ItemId { get; set; }
        public decimal Qty { get; set; }
        public decimal Cost { get; set; }
        public int? OwnerId { get; set; }
        public int? SupplierId { get; set; }
        public bool Remove { get; set; }
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public StockingGroupBy GroupBy { get; set; }
    [BindProperty] public List<Row> Rows { get; set; } = new();

    public StockingList List { get; private set; } = null!;
    public List<Supplier> Suppliers { get; private set; } = new();
    public List<Owner> Owners { get; private set; } = new();
    public bool Editable => List.Status is StockingListStatus.Draft or StockingListStatus.Purchased;

    private async Task<bool> LoadAsync()
    {
        var l = await stocking.GetAsync(Id);
        if (l is null) return false;
        List = l;
        Suppliers = await suppliers.GetAllAsync(false);
        Owners = await owners.GetAllAsync(false);
        return true;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!await LoadAsync()) return NotFound();
        var units = List.Items.ToDictionary(i => i.Id, i => i.Product?.Unit ?? UnitType.Piece);
        await TryAsync(() => stocking.UpdateItemsAsync(Id, Name, Notes, GroupBy, Rows.Where(r => units.ContainsKey(r.ItemId)).Select(r => new StockingItemEdit
        {
            ItemId = r.ItemId, Quantity = Qty.FromDisplay(Math.Max(0, r.Qty), units[r.ItemId]), EstimatedUnitCostCents = Money.ToCents(Math.Max(0, r.Cost)),
            OwnerId = r.OwnerId, SupplierId = r.SupplierId, Remove = r.Remove
        })), "Lista guardada.", redirecting: true);
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostAddAsync(string product, decimal qty)
    {
        await TryAsync(async () =>
        {
            var p = await products.FindByBarcodeAsync(product ?? "")
                    ?? (await products.SearchAsync(product, null, null, true, 1, 2)) switch
                    {
                        { TotalCount: 1 } r => await products.GetAsync(r.Items[0].Id),
                        { TotalCount: 0 } => throw new DomainException("No se encontró el producto."),
                        _ => throw new DomainException("Hay varios productos con ese nombre; escribe el código de barras o un nombre más preciso.")
                    };
            await stocking.AddItemAsync(Id, p!.Id, Qty.FromDisplay(qty, p.Unit));
        }, "Producto agregado a la lista.", redirecting: true);
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostStatusAsync(StockingListStatus status)
    {
        await TryAsync(() => stocking.SetStatusAsync(Id, status),
            status == StockingListStatus.Purchased ? "Lista marcada como comprada." : "Lista cancelada.", redirecting: true);
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        if (await TryAsync(() => deletion.DeleteStockingListAsync(Id), "Lista eliminada.", redirecting: true))
            return RedirectToPage("/Stocking/Index");
        return RedirectToPage(new { Id });
    }
}
