using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Suppliers;

/// <summary>Register a real delivery: what arrived, from which supplier, owned by whom, at what cost.</summary>
[Authorize(Policy = Policies.Manager)]
public class DeliveryModel(SupplierService suppliers, OwnerService owners, ProductService products, DeliveryService deliveries) : AppPageModel
{
    public class Row
    {
        public int ProductId { get; set; }
        public int OwnerId { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
    }

    [BindProperty(SupportsGet = true)] public int? SupplierId { get; set; }
    [BindProperty] public string? InvoiceNumber { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public bool UpdateReferenceCost { get; set; } = true;
    [BindProperty] public List<Row> Items { get; set; } = new();

    public List<Supplier> Suppliers { get; private set; } = new();
    public List<Owner> Owners { get; private set; } = new();

    private async Task LoadAsync()
    {
        Suppliers = await suppliers.GetAllAsync(includeInactive: false);
        Owners = await owners.GetAllAsync(includeInactive: false);
    }

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>Product search for the delivery grid (optionally only this supplier's products first).</summary>
    public async Task<IActionResult> OnGetSearchAsync(string q)
    {
        var list = await products.QueryProducts(q, null, null, true).OrderBy(p => p.SearchName).Take(20)
            .Select(p => new { id = p.Id, name = p.Name, unit = (int)p.Unit, cost = p.PurchasePriceCents / 100.0, barcode = p.Barcode,
                               supplierId = p.PreferredSupplierId, ownerId = p.PriorityOwnerId })
            .ToListAsync();
        return new JsonResult(list);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        int id = 0;
        var ok = await TryAsync(async () =>
        {
            if (SupplierId is not int sid) throw new DomainException("Selecciona el proveedor.");
            var units = new Dictionary<int, UnitType>();
            foreach (var pid in Items.Select(i => i.ProductId).Distinct())
                units[pid] = (await products.GetAsync(pid))?.Unit ?? throw new DomainException("Producto no encontrado.");
            if (Items.Any(i => i.UnitCost < 0)) throw new DomainException("Los costos no pueden ser negativos.");
            id = await deliveries.RegisterAsync(new DeliveryInput
            {
                SupplierId = sid, InvoiceNumber = InvoiceNumber, Notes = Notes, UpdateReferenceCost = UpdateReferenceCost,
                Items = Items.Where(i => i.Qty > 0).Select(i => new DeliveryLineInput
                {
                    ProductId = i.ProductId, OwnerId = i.OwnerId,
                    Quantity = Qty.FromDisplay(i.Qty, units[i.ProductId]), UnitCostCents = Money.ToCents(i.UnitCost)
                }).ToList()
            });
        }, "Entrega registrada. La mercancía ya está en inventario.");
        if (ok) return RedirectToPage("/Suppliers/DeliveryDetails", new { id });
        await LoadAsync();
        return Page();
    }
}
