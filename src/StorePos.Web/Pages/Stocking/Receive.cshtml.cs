using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Stocking;

/// <summary>Converts a stocking list into real inventory: only what actually arrived, at the real cost.</summary>
[Authorize(Policy = Policies.Manager)]
public class ReceiveModel(StockingService stocking, SupplierService suppliers, OwnerService owners) : AppPageModel
{
    public class Row
    {
        public int ItemId { get; set; }
        public decimal Qty { get; set; }
        public decimal Cost { get; set; }
        public int? OwnerId { get; set; }
        public int? SupplierId { get; set; }
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public List<Row> Rows { get; set; } = new();
    [BindProperty] public string? InvoiceNumber { get; set; }
    [BindProperty] public bool UpdateReferenceCost { get; set; } = true;

    public StockingList List { get; private set; } = null!;
    public List<Supplier> Suppliers { get; private set; } = new();
    public List<Owner> Owners { get; private set; } = new();

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

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return NotFound();
        var units = List.Items.ToDictionary(i => i.Id, i => i.Product?.Unit ?? UnitType.Piece);
        List<int> ids = new();
        var ok = await TryAsync(async () =>
        {
            ids = await stocking.ReceiveAsync(Id, Rows.Where(r => units.ContainsKey(r.ItemId) && r.Qty > 0).Select(r => new ReceiveLine
            {
                ItemId = r.ItemId, Quantity = Qty.FromDisplay(r.Qty, units[r.ItemId]), UnitCostCents = Money.ToCents(Math.Max(0, r.Cost)),
                OwnerId = r.OwnerId, SupplierId = r.SupplierId
            }), InvoiceNumber, UpdateReferenceCost);
        }, null);
        if (!ok) return Page();
        Ok($"Mercancía recibida e ingresada al inventario (entregas #{string.Join(", #", ids)}).");
        return ids.Count == 1 ? RedirectToPage("/Suppliers/DeliveryDetails", new { id = ids[0] }) : RedirectToPage("/Suppliers/Deliveries");
    }
}
