using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Products;

[Authorize(Policy = Policies.Manager)]
public class EditModel(ProductService products, OwnerService owners, SupplierService suppliers, InventoryService inventory) : AppPageModel
{
    public class Form
    {
        public int? Id { get; set; }
        public string Name { get; set; } = "";
        public string? Barcode { get; set; }
        public int? CategoryId { get; set; }
        public string? Description { get; set; }
        public UnitType Unit { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal MinStock { get; set; }
        public int? PreferredSupplierId { get; set; }
        public int? PriorityOwnerId { get; set; }
        public bool IsActive { get; set; } = true;
        // Optional initial stock when creating
        public decimal InitialQty { get; set; }
        public int? InitialOwnerId { get; set; }
    }

    [BindProperty] public Form Input { get; set; } = new();
    public List<Category> Categories { get; private set; } = new();
    public List<Owner> Owners { get; private set; } = new();
    public List<Supplier> Suppliers { get; private set; } = new();
    public bool IsNew => Input.Id is null or 0;

    private async Task LoadListsAsync()
    {
        Categories = await products.GetCategoriesAsync();
        Owners = await owners.GetAllAsync(includeInactive: false);
        Suppliers = await suppliers.GetAllAsync(includeInactive: false);
    }

    public async Task<IActionResult> OnGetAsync(int? id, string? barcode, string? name)
    {
        await LoadListsAsync();
        if (id is int pid)
        {
            var p = await products.GetAsync(pid);
            if (p is null) return NotFound();
            Input = new Form
            {
                Id = p.Id, Name = p.Name, Barcode = p.Barcode, CategoryId = p.CategoryId, Description = p.Description,
                Unit = p.Unit, PurchasePrice = Money.ToPesos(p.PurchasePriceCents), SalePrice = Money.ToPesos(p.SalePriceCents),
                MinStock = Qty.ToDisplay(p.MinStock, p.Unit), PreferredSupplierId = p.PreferredSupplierId,
                PriorityOwnerId = p.PriorityOwnerId, IsActive = p.IsActive
            };
        }
        else
        {
            Input.Barcode = barcode;
            Input.Name = name ?? "";
            Input.InitialOwnerId = Owners.Count == 1 ? Owners[0].Id : null;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? next)
    {
        int id = 0;
        var ok = await TryAsync(async () =>
        {
            if (Input.PurchasePrice < 0 || Input.SalePrice < 0) throw new DomainException("Los precios no pueden ser negativos.");
            bool creating = Input.Id is null or 0;
            id = await products.SaveAsync(new ProductInput
            {
                Id = Input.Id, Name = Input.Name, Barcode = Input.Barcode, CategoryId = Input.CategoryId,
                Description = Input.Description, Unit = Input.Unit,
                PurchasePriceCents = Money.ToCents(Input.PurchasePrice), SalePriceCents = Money.ToCents(Input.SalePrice),
                MinStock = Qty.FromDisplay(Input.MinStock, Input.Unit), PreferredSupplierId = Input.PreferredSupplierId,
                PriorityOwnerId = Input.PriorityOwnerId, IsActive = Input.IsActive
            });
            Input.Id = id;   // a retry after a later error updates instead of duplicating
            ModelState.Remove("Input.Id");
            if (creating && Input.InitialQty > 0)
            {
                if (Input.InitialOwnerId is not int owner) throw new DomainException("Producto guardado, pero falta indicar el dueño de la existencia inicial. Agrégala desde Inventario.");
                await inventory.AddStockAsync(id, owner, Qty.FromDisplay(Input.InitialQty, Input.Unit),
                    Money.ToCents(Input.PurchasePrice), MovementType.InitialStock, "Existencia inicial al registrar el producto");
            }
        }, "Producto guardado.");
        if (ok) return next == "new" ? RedirectToPage("/Products/Edit") : RedirectToPage("/Products/Index");
        await LoadListsAsync();
        return Page();
    }
}
