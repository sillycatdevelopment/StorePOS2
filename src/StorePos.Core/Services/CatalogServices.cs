using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;

namespace StorePos.Core.Services;

public class ProductService(StoreDbContext db, AuditService audit, IClock clock)
{
    public IQueryable<Product> QueryProducts(string? text, int? categoryId, int? supplierId, bool? active)
    {
        var q = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var t = text.Trim();
            var like = "%" + TextNorm.EscapeLike(TextNorm.Normalize(t)) + "%";
            q = q.Where(p => p.Barcode == t || EF.Functions.Like(p.SearchName, like, "\\"));
        }
        if (categoryId is not null) q = q.Where(p => p.CategoryId == categoryId);
        if (supplierId is not null) q = q.Where(p => p.PreferredSupplierId == supplierId);
        if (active is not null) q = q.Where(p => p.IsActive == active);
        return q;
    }

    public Task<PagedResult<ProductRow>> SearchAsync(string? text, int? categoryId, int? supplierId, bool? active, int page, int pageSize = 50)
    {
        var q = QueryProducts(text, categoryId, supplierId, active)
            .OrderBy(p => p.SearchName)
            .Select(p => new ProductRow
            {
                Id = p.Id, Name = p.Name, Barcode = p.Barcode, Unit = p.Unit,
                CategoryName = p.Category != null ? p.Category.Name : null,
                SupplierName = p.PreferredSupplier != null ? p.PreferredSupplier.Name : null,
                PurchasePriceCents = p.PurchasePriceCents, SalePriceCents = p.SalePriceCents,
                MinStock = p.MinStock, IsActive = p.IsActive,
                Stock = db.InventoryLots.Where(l => l.ProductId == p.Id).Sum(l => (long?)l.QtyRemaining) ?? 0
            });
        return PagedResult<ProductRow>.CreateAsync(q, page, pageSize);
    }

    public Task<Product?> GetAsync(int id) =>
        db.Products.AsNoTracking().Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);

    /// <summary>All active products as id/name pairs, for selection lists.</summary>
    public Task<List<Option>> GetOptionsAsync() =>
        db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SearchName).Select(p => new Option(p.Id, p.Name)).ToListAsync();

    public Task<Product?> FindByBarcodeAsync(string barcode)
    {
        var code = barcode.Trim();
        return db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Barcode == code);
    }

    public async Task<int> SaveAsync(ProductInput input)
    {
        var name = TextNorm.Clean(input.Name) ?? throw new DomainException("El nombre del producto es obligatorio.");
        var barcode = TextNorm.Clean(input.Barcode);
        if (input.PurchasePriceCents < 0 || input.SalePriceCents < 0) throw new DomainException("Los precios no pueden ser negativos.");
        if (input.MinStock < 0) throw new DomainException("El mínimo de existencia no puede ser negativo.");

        if (barcode is not null)
        {
            var clash = await db.Products.Where(p => p.Barcode == barcode && p.Id != input.Id)
                                         .Select(p => p.Name).FirstOrDefaultAsync();
            if (clash is not null) throw new DomainException($"El código de barras {barcode} ya pertenece a \"{clash}\".");
        }

        var now = clock.Now;
        Product p;
        if (input.Id is int id && id > 0)
        {
            p = await db.Products.FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Producto no encontrado.");
            if (p.Unit != input.Unit && await db.InventoryLots.AnyAsync(l => l.ProductId == id))
                throw new DomainException("No se puede cambiar la unidad (pieza/kg) de un producto que ya tiene inventario o ventas.");

            if (p.SalePriceCents != input.SalePriceCents || p.PurchasePriceCents != input.PurchasePriceCents)
                audit.Add("Precio modificado", "Product", p.Id,
                    $"{p.Name}: venta {Money.Format(p.SalePriceCents)} → {Money.Format(input.SalePriceCents)}, " +
                    $"compra {Money.Format(p.PurchasePriceCents)} → {Money.Format(input.PurchasePriceCents)}");
            if (p.IsActive != input.IsActive)
                audit.Add(input.IsActive ? "Producto activado" : "Producto desactivado", "Product", p.Id, p.Name);
        }
        else
        {
            p = new Product { CreatedAt = now };
            db.Products.Add(p);
        }

        p.Name = name;
        p.SearchName = TextNorm.Normalize(name);
        p.Barcode = barcode;
        p.CategoryId = input.CategoryId;
        p.Description = TextNorm.Clean(input.Description);
        p.Unit = input.Unit;
        p.PurchasePriceCents = input.PurchasePriceCents;
        p.SalePriceCents = input.SalePriceCents;
        p.MinStock = input.MinStock;
        p.PreferredSupplierId = input.PreferredSupplierId;
        p.PriorityOwnerId = input.PriorityOwnerId;
        p.IsActive = input.IsActive;
        p.UpdatedAt = now;

        await db.SaveChangesAsync();
        return p.Id;
    }

    // ---- Categories ----

    public Task<List<Category>> GetCategoriesAsync(bool includeInactive = false) =>
        db.Categories.AsNoTracking().Where(c => includeInactive || c.IsActive).OrderBy(c => c.Name).ToListAsync();

    public async Task SaveCategoryAsync(int? id, string name, bool isActive)
    {
        var clean = TextNorm.Clean(name) ?? throw new DomainException("El nombre de la categoría es obligatorio.");
        if (await db.Categories.AnyAsync(c => c.Name == clean && c.Id != id))
            throw new DomainException("Ya existe una categoría con ese nombre.");
        Category c;
        if (id is > 0)
            c = await db.Categories.FindAsync(id.Value) ?? throw new DomainException("Categoría no encontrada.");
        else
            db.Categories.Add(c = new Category());
        c.Name = clean;
        c.IsActive = isActive;
        await db.SaveChangesAsync();
    }
}

public class OwnerService(StoreDbContext db, AuditService audit, IClock clock)
{
    public Task<List<Owner>> GetAllAsync(bool includeInactive = true) =>
        db.Owners.AsNoTracking().Where(o => includeInactive || o.IsActive).OrderBy(o => o.Name).ToListAsync();

    public Task<Owner?> GetAsync(int id) => db.Owners.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);

    public async Task<int> SaveAsync(int? id, string name, string? contact, string? notes, bool isActive)
    {
        var clean = TextNorm.Clean(name) ?? throw new DomainException("El nombre del dueño es obligatorio.");
        Owner o;
        if (id is > 0)
        {
            o = await db.Owners.FindAsync(id.Value) ?? throw new DomainException("Dueño no encontrado.");
            if (o.IsActive != isActive)
                audit.Add(isActive ? "Dueño activado" : "Dueño desactivado", "Owner", o.Id, o.Name);
        }
        else
        {
            db.Owners.Add(o = new Owner { CreatedAt = clock.Now });
        }
        o.Name = clean;
        o.Contact = TextNorm.Clean(contact);
        o.Notes = TextNorm.Clean(notes);
        o.IsActive = isActive;
        await db.SaveChangesAsync();
        return o.Id;
    }

    /// <summary>Stock, inventory value and sales/profit per owner for a date range (one query per aspect).</summary>
    public async Task<List<OwnerStats>> GetStatsAsync(DateOnly from, DateOnly to)
    {
        var owners = await db.Owners.AsNoTracking().OrderBy(o => o.Name).ToListAsync();

        var stock = await db.InventoryLots.AsNoTracking().Where(l => l.QtyRemaining > 0)
            .Select(l => new { l.OwnerId, l.QtyRemaining, l.UnitCostCents, l.Product!.Unit, l.Product.SalePriceCents })
            .ToListAsync();

        var sales = await db.SaleItems.AsNoTracking()
            .Where(i => i.BusinessDate >= from && i.BusinessDate <= to && i.Sale!.Status == SaleStatus.Completed)
            .GroupBy(i => i.OwnerId)
            .Select(g => new
            {
                OwnerId = g.Key,
                Revenue = g.Sum(i => i.LineTotalCents),
                Cost = g.Sum(i => i.CostCents),
                Lines = g.Count()
            }).ToListAsync();

        // Stock sold to another owner (manual transfers and boxes bought to sell individually).
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var transfers = await db.InventoryMovements.AsNoTracking()
            .Where(m => m.Type == MovementType.TransferOut && m.CreatedAt >= fromDt && m.CreatedAt < toDt)
            .Select(m => new { m.OwnerId, m.Quantity, m.UnitCostCents, Price = m.UnitPriceCents ?? 0, m.Product!.Unit })
            .ToListAsync();

        return owners.Select(o =>
        {
            var s = sales.FirstOrDefault(x => x.OwnerId == o.Id);
            var t = transfers.Where(x => x.OwnerId == o.Id).ToList();
            var lots = stock.Where(l => l.OwnerId == o.Id).ToList();
            return new OwnerStats
            {
                Owner = o,
                ProductsInStock = lots.Count,
                InventoryCostCents = lots.Sum(l => Money.LineAmount(l.UnitCostCents, l.QtyRemaining, l.Unit)),
                InventoryRetailCents = lots.Sum(l => Money.LineAmount(l.SalePriceCents, l.QtyRemaining, l.Unit)),
                RevenueCents = s?.Revenue ?? 0,
                CostCents = s?.Cost ?? 0,
                SaleLines = s?.Lines ?? 0,
                TransferRevenueCents = t.Sum(x => Money.LineAmount(x.Price, -x.Quantity, x.Unit)),
                TransferCostCents = t.Sum(x => Money.LineAmount(x.UnitCostCents, -x.Quantity, x.Unit))
            };
        }).ToList();
    }
}

public class SupplierService(StoreDbContext db, AuditService audit)
{
    public Task<List<Supplier>> GetAllAsync(bool includeInactive = true) =>
        db.Suppliers.AsNoTracking().Include(s => s.Schedules)
          .Where(s => includeInactive || s.IsActive).OrderBy(s => s.Name).ToListAsync();

    public Task<Supplier?> GetAsync(int id) =>
        db.Suppliers.AsNoTracking().Include(s => s.Schedules).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<int> SaveAsync(int? id, string name, string? contact, string? phone, string? notes,
                                     bool isActive, IEnumerable<DayOfWeek> days)
    {
        var clean = TextNorm.Clean(name) ?? throw new DomainException("El nombre del proveedor es obligatorio.");
        Supplier s;
        if (id is > 0)
            s = await db.Suppliers.Include(x => x.Schedules).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new DomainException("Proveedor no encontrado.");
        else
            db.Suppliers.Add(s = new Supplier());

        s.Name = clean;
        s.Contact = TextNorm.Clean(contact);
        s.Phone = TextNorm.Clean(phone);
        s.Notes = TextNorm.Clean(notes);
        s.IsActive = isActive;

        var wanted = days.Distinct().ToHashSet();
        var current = s.Schedules.Select(x => x.Day).ToHashSet();
        foreach (var gone in s.Schedules.Where(x => !wanted.Contains(x.Day)).ToList())
            db.SupplierSchedules.Remove(gone);
        foreach (var d in wanted.Where(d => !current.Contains(d)))
            s.Schedules.Add(new SupplierSchedule { Day = d });

        if (id is > 0 && !wanted.SetEquals(current))
            audit.Add("Horario de proveedor modificado", "Supplier", s.Id,
                $"{s.Name}: {string.Join(", ", wanted.OrderBy(d => d).Select(DisplayNames.Of))}");

        await db.SaveChangesAsync();
        return s.Id;
    }

    /// <summary>Days from <paramref name="today"/> to the next scheduled visit strictly after today (1–7), or null.</summary>
    public static int? DaysToNextDelivery(IEnumerable<DayOfWeek> days, DateOnly today)
    {
        int? best = null;
        foreach (var d in days)
        {
            int diff = ((int)d - (int)today.DayOfWeek + 7) % 7;
            if (diff == 0) diff = 7;
            if (best is null || diff < best) best = diff;
        }
        return best;
    }

    /// <summary>Average days between visits (7 / visits per week), or null with no schedule.</summary>
    public static double? CycleDays(IEnumerable<DayOfWeek> days)
    {
        int n = days.Distinct().Count();
        return n == 0 ? null : 7.0 / n;
    }

    /// <summary>Scheduled visits from today for the next <paramref name="daysAhead"/> days.</summary>
    public async Task<List<UpcomingDelivery>> GetUpcomingAsync(DateOnly today, int daysAhead = 7)
    {
        var suppliers = await GetAllAsync(includeInactive: false);
        var todayStart = today.ToDateTime(TimeOnly.MinValue);
        var receivedToday = await db.Deliveries.AsNoTracking()
            .Where(d => d.ReceivedAt >= todayStart).Select(d => d.SupplierId).Distinct().ToListAsync();

        var result = new List<UpcomingDelivery>();
        for (int i = 0; i < daysAhead; i++)
        {
            var date = today.AddDays(i);
            foreach (var s in suppliers.Where(s => s.Schedules.Any(x => x.Day == date.DayOfWeek)))
                result.Add(new UpcomingDelivery(date, s.Id, s.Name, i == 0 && receivedToday.Contains(s.Id)));
        }
        return result;
    }
}
