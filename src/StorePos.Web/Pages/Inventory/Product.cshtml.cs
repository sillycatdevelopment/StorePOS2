using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Inventory;

/// <summary>One product: stock per owner, open lots, movements and (managers) stock operations.</summary>
public class ProductModel(ProductService products, InventoryService inventory, OwnerService owners) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public Product Product { get; private set; } = null!;
    public List<OwnerStock> ByOwner { get; private set; } = new();
    public List<InventoryLot> Lots { get; private set; } = new();
    public PagedResult<MovementRow> Movements { get; private set; } = null!;
    public List<Owner> Owners { get; private set; } = new();
    public PackLink? Link { get; private set; }
    public List<Option> ProductOptions { get; private set; } = new();
    public long PackStock { get; private set; }

    private async Task<bool> LoadAsync()
    {
        var p = await products.GetAsync(Id);
        if (p is null) return false;
        Product = p;
        ByOwner = await inventory.GetOwnerStockAsync(Id);
        Lots = await inventory.GetOpenLotsAsync(Id);
        Movements = await inventory.GetMovementsAsync(Id, null, null, null, null, PageNumber, 30);
        Owners = await owners.GetAllAsync(includeInactive: false);
        Link = await inventory.GetPackLinkAsync(Id);
        if (Link is null) ProductOptions = (await products.GetOptionsAsync()).Where(o => o.Id != Id).ToList();
        else PackStock = await inventory.GetStockAsync(Link.PackProductId);
        return true;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

    private async Task<IActionResult> Done() { if (!await LoadAsync()) return NotFound(); return ErrorMessage is null ? RedirectToPage(new { Id }) : Page(); }

    public async Task<IActionResult> OnPostAddAsync(int ownerId, MovementType type, decimal qty, decimal unitCost, string? reason)
    {
        if (!IsManager) return Forbid();
        var p = await products.GetAsync(Id); if (p is null) return NotFound();
        await TryAsync(() => inventory.AddStockAsync(Id, ownerId, Qty.FromDisplay(qty, p.Unit), Money.ToCents(unitCost), type, reason),
                       "Existencia agregada.");
        return await Done();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int ownerId, MovementType type, decimal qty, string reason)
    {
        if (!IsManager) return Forbid();
        var p = await products.GetAsync(Id); if (p is null) return NotFound();
        await TryAsync(() => inventory.RemoveStockAsync(Id, ownerId, Qty.FromDisplay(qty, p.Unit), type, reason ?? ""), "Salida registrada.");
        return await Done();
    }

    public async Task<IActionResult> OnPostCountAsync(int ownerId, decimal counted, string reason)
    {
        if (!IsManager) return Forbid();
        var p = await products.GetAsync(Id); if (p is null) return NotFound();
        await TryAsync(() => inventory.AdjustToCountAsync(Id, ownerId, Qty.FromDisplay(counted, p.Unit), reason ?? ""), "Conteo registrado.");
        return await Done();
    }

    public async Task<IActionResult> OnPostTransferAsync(int fromOwnerId, int toOwnerId, decimal qty, decimal unitPrice, string? reason)
    {
        if (!IsManager) return Forbid();
        var p = await products.GetAsync(Id); if (p is null) return NotFound();
        await TryAsync(() => inventory.TransferAsync(Id, fromOwnerId, toOwnerId, Qty.FromDisplay(qty, p.Unit), Money.ToCents(unitPrice), reason),
                       "Traspaso registrado.");
        return await Done();
    }

    // ---- Box ↔ individual pieces (e.g. cigarette packs and single cigarettes)

    public async Task<IActionResult> OnPostLinkAsync(string role, int otherProductId, int unitsPerPack, int? unitOwnerId, bool autoOpen)
    {
        if (!IsManager) return Forbid();
        bool thisIsUnit = role == "unit";
        await TryAsync(() => inventory.SavePackLinkAsync(thisIsUnit ? Id : otherProductId, thisIsUnit ? otherProductId : Id,
                                                         unitsPerPack, unitOwnerId, autoOpen), "Caja y suelto ligados.");
        return await Done();
    }

    public async Task<IActionResult> OnPostUnlinkAsync(int unitProductId)
    {
        if (!IsManager) return Forbid();
        await TryAsync(() => inventory.RemovePackLinkAsync(unitProductId), "Se quitó la liga entre caja y suelto.");
        return await Done();
    }

    public async Task<IActionResult> OnPostOpenPacksAsync(int packs, int? fromOwnerId)
    {
        if (!IsManager) return Forbid();
        await TryAsync(() => inventory.OpenPacksAsync(Id, packs, fromOwnerId), packs == 1 ? "Caja abierta." : $"{packs} cajas abiertas.");
        return await Done();
    }
}
