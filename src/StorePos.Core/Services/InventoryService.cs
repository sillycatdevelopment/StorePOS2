using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;

namespace StorePos.Core.Services;

/// <summary>
/// All stock changes go through here. Every change writes an <see cref="InventoryMovement"/>,
/// stock lives in <see cref="InventoryLot"/>s per owner and is consumed FIFO within an owner.
/// Methods ending in "Core" do not save or open transactions; callers compose them inside their own.
/// </summary>
public class InventoryService(StoreDbContext db, AuditService audit, ICurrentUser user, IClock clock, WriteGate gate, SettingsService settings)
{
    // ------------------------------------------------------------------ queries

    public async Task<List<OwnerStock>> GetOwnerStockAsync(int productId)
    {
        var rows = await db.InventoryLots.AsNoTracking()
            .Where(l => l.ProductId == productId && l.QtyRemaining > 0)
            .GroupBy(l => new { l.OwnerId, l.Owner!.Name })
            .Select(g => new
            {
                g.Key.OwnerId, g.Key.Name,
                Qty = g.Sum(l => l.QtyRemaining),
                CostTimesQty = g.Sum(l => l.QtyRemaining * l.UnitCostCents),
                Oldest = g.Min(l => l.ReceivedAt)
            })
            .ToListAsync();
        var unit = await db.Products.Where(p => p.Id == productId).Select(p => p.Unit).FirstAsync();
        return rows.Select(r => new OwnerStock(r.OwnerId, r.Name, r.Qty, ScaleValue(r.CostTimesQty, unit), r.Oldest))
                   .OrderBy(r => r.OwnerName).ToList();
    }

    public Task<long> GetStockAsync(int productId, int? ownerId = null) =>
        db.InventoryLots.Where(l => l.ProductId == productId && (ownerId == null || l.OwnerId == ownerId))
          .SumAsync(l => l.QtyRemaining);

    public Task<List<InventoryLot>> GetOpenLotsAsync(int productId) =>
        db.InventoryLots.AsNoTracking().Include(l => l.Owner).Include(l => l.Supplier)
          .Where(l => l.ProductId == productId && l.QtyRemaining > 0)
          .OrderBy(l => l.OwnerId).ThenBy(l => l.ReceivedAt).ThenBy(l => l.Id).ToListAsync();

    /// <summary>Converts Σ(qty × unit cost) to centavos, dividing by 1000 for kilogram products.</summary>
    public static long ScaleValue(long qtyTimesCents, UnitType unit) =>
        unit == UnitType.Piece ? qtyTimesCents
            : (long)Math.Round(qtyTimesCents / 1000m, MidpointRounding.AwayFromZero);

    public static StockStatus StatusOf(long stock, long min, int marginPercent)
    {
        if (stock <= 0) return StockStatus.Out;
        if (min > 0 && stock <= min) return StockStatus.Low;
        if (min > 0 && stock <= min + min * marginPercent / 100) return StockStatus.Approaching;
        return StockStatus.Ok;
    }

    public async Task<PagedResult<InventoryRow>> GetInventoryAsync(InventoryFilter f, int page, int pageSize = 50)
    {
        int margin = await settings.GetIntAsync(SettingKeys.LowStockMarginPercent);
        var products = db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(f.Text))
        {
            var t = f.Text.Trim();
            var like = "%" + TextNorm.EscapeLike(TextNorm.Normalize(t)) + "%";
            products = products.Where(p => p.Barcode == t || EF.Functions.Like(p.SearchName, like, "\\"));
        }
        if (f.CategoryId is not null) products = products.Where(p => p.CategoryId == f.CategoryId);
        if (f.SupplierId is not null) products = products.Where(p => p.PreferredSupplierId == f.SupplierId);

        int? ownerId = f.OwnerId;
        var q = products.Select(p => new
        {
            p.Id, p.Name, p.Barcode, p.Unit, p.MinStock, p.SalePriceCents, p.PurchasePriceCents, p.SearchName,
            CategoryName = p.Category != null ? p.Category.Name : null,
            SupplierName = p.PreferredSupplier != null ? p.PreferredSupplier.Name : null,
            Stock = db.InventoryLots.Where(l => l.ProductId == p.Id && (ownerId == null || l.OwnerId == ownerId))
                                    .Sum(l => (long?)l.QtyRemaining) ?? 0
        });

        if (ownerId is not null)   // only products this owner has ever stocked
            q = q.Where(x => db.InventoryLots.Any(l => l.ProductId == x.Id && l.OwnerId == ownerId));

        q = f.Status switch
        {
            StockFilter.InStock => q.Where(x => x.Stock > 0),
            StockFilter.Out => q.Where(x => x.Stock <= 0),
            StockFilter.Low => q.Where(x => x.Stock > 0 && x.MinStock > 0 && x.Stock <= x.MinStock),
            StockFilter.LowOrOut => q.Where(x => x.Stock <= 0 || (x.MinStock > 0 && x.Stock <= x.MinStock)),
            StockFilter.Approaching => q.Where(x => x.MinStock > 0 && x.Stock > x.MinStock && x.Stock <= x.MinStock + x.MinStock * margin / 100),
            _ => q
        };

        int total = await q.CountAsync();
        if (page < 1) page = 1;
        var pageRows = await q.OrderBy(x => x.SearchName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var ids = pageRows.Select(r => r.Id).ToList();
        var byOwner = await db.InventoryLots.AsNoTracking()
            .Where(l => ids.Contains(l.ProductId) && l.QtyRemaining > 0 && (ownerId == null || l.OwnerId == ownerId))
            .GroupBy(l => new { l.ProductId, l.OwnerId, l.Owner!.Name })
            .Select(g => new { g.Key.ProductId, g.Key.Name, Qty = g.Sum(l => l.QtyRemaining), Value = g.Sum(l => l.QtyRemaining * l.UnitCostCents) })
            .ToListAsync();

        var items = pageRows.Select(r =>
        {
            var owners = byOwner.Where(o => o.ProductId == r.Id).OrderBy(o => o.Name).ToList();
            return new InventoryRow
            {
                ProductId = r.Id, Name = r.Name, Barcode = r.Barcode, Unit = r.Unit,
                CategoryName = r.CategoryName, SupplierName = r.SupplierName,
                Stock = r.Stock, MinStock = r.MinStock,
                SalePriceCents = r.SalePriceCents, PurchasePriceCents = r.PurchasePriceCents,
                CostValueCents = ScaleValue(owners.Sum(o => o.Value), r.Unit),
                RetailValueCents = Money.LineAmount(r.SalePriceCents, Math.Max(0, r.Stock), r.Unit),
                Owners = owners.Select(o => new OwnerQty(o.Name, o.Qty)).ToList(),
                Status = StatusOf(r.Stock, r.MinStock, margin)
            };
        }).ToList();

        return new PagedResult<InventoryRow> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    /// <summary>Current inventory value at cost and at retail price (all owners or one).</summary>
    public async Task<(long Cost, long Retail)> GetInventoryValueAsync(int? ownerId = null)
    {
        var rows = await db.InventoryLots.AsNoTracking()
            .Where(l => l.QtyRemaining > 0 && (ownerId == null || l.OwnerId == ownerId))
            .GroupBy(l => l.Product!.Unit)
            .Select(g => new
            {
                Unit = g.Key,
                Cost = g.Sum(l => l.QtyRemaining * l.UnitCostCents),
                Retail = g.Sum(l => l.QtyRemaining * l.Product!.SalePriceCents)
            }).ToListAsync();
        return (rows.Sum(r => ScaleValue(r.Cost, r.Unit)), rows.Sum(r => ScaleValue(r.Retail, r.Unit)));
    }

    public async Task<(int Out, int Low)> CountOutAndLowAsync()
    {
        var q = db.Products.AsNoTracking().Where(p => p.IsActive).Select(p => new
        {
            p.MinStock,
            Stock = db.InventoryLots.Where(l => l.ProductId == p.Id).Sum(l => (long?)l.QtyRemaining) ?? 0
        });
        int outCount = await q.CountAsync(x => x.Stock <= 0);
        int low = await q.CountAsync(x => x.Stock > 0 && x.MinStock > 0 && x.Stock <= x.MinStock);
        return (outCount, low);
    }

    public Task<PagedResult<MovementRow>> GetMovementsAsync(int? productId, int? ownerId, MovementType? type,
                                                            DateOnly? from, DateOnly? to, int page, int pageSize = 50)
    {
        var q = db.InventoryMovements.AsNoTracking().AsQueryable();
        if (productId is not null) q = q.Where(m => m.ProductId == productId);
        if (ownerId is not null) q = q.Where(m => m.OwnerId == ownerId);
        if (type is not null) q = q.Where(m => m.Type == type);
        if (from is { } f) { var d = f.ToDateTime(TimeOnly.MinValue); q = q.Where(m => m.CreatedAt >= d); }
        if (to is { } t) { var d = t.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(m => m.CreatedAt < d); }

        return PagedResult<MovementRow>.CreateAsync(q.OrderByDescending(m => m.Id).Select(m => new MovementRow
        {
            Id = m.Id, CreatedAt = m.CreatedAt, Type = m.Type, Quantity = m.Quantity,
            ProductId = m.ProductId, ProductName = m.Product!.Name, Unit = m.Product.Unit,
            OwnerName = m.Owner!.Name, UnitCostCents = m.UnitCostCents, UnitPriceCents = m.UnitPriceCents,
            Reason = m.Reason, SaleId = m.SaleId, DeliveryId = m.DeliveryId,
            UserName = m.User != null ? m.User.DisplayName : null
        }), page, pageSize);
    }

    // ------------------------------------------------------------------ composable core operations

    /// <summary>Creates a lot and its inbound movement. Does not save.</summary>
    public InventoryLot AddStockCore(int productId, int ownerId, long qty, long unitCostCents, MovementType type,
                                     string? reason, int? supplierId = null, Delivery? delivery = null, DeliveryItem? deliveryItem = null)
    {
        if (qty <= 0) throw new DomainException("La cantidad debe ser mayor a cero.");
        if (unitCostCents < 0) throw new DomainException("El costo no puede ser negativo.");
        var now = clock.Now;
        var lot = new InventoryLot
        {
            ProductId = productId, OwnerId = ownerId, SupplierId = supplierId,
            UnitCostCents = unitCostCents, QtyReceived = qty, QtyRemaining = qty,
            ReceivedAt = now, Source = type, DeliveryItem = deliveryItem
        };
        db.InventoryLots.Add(lot);
        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = productId, OwnerId = ownerId, Lot = lot, Type = type, Quantity = qty,
            UnitCostCents = unitCostCents, Reason = reason, Delivery = delivery,
            UserId = user.UserId, CreatedAt = now
        });
        return lot;
    }

    /// <summary>
    /// Takes <paramref name="qty"/> from the owner's lots, oldest first. Writes one outbound movement per lot.
    /// Throws if the owner does not have enough. Does not save.
    /// </summary>
    public async Task<List<(InventoryLot Lot, long Qty)>> ConsumeFifoCoreAsync(int productId, int ownerId, long qty,
        MovementType type, string? reason, long? unitPriceCents = null, Sale? sale = null)
    {
        if (qty <= 0) throw new DomainException("La cantidad debe ser mayor a cero.");
        var lots = await db.InventoryLots
            .Where(l => l.ProductId == productId && l.OwnerId == ownerId && l.QtyRemaining > 0)
            .OrderBy(l => l.ReceivedAt).ThenBy(l => l.Id)
            .ToListAsync();
        // Include lots created earlier in this same unit of work (not yet saved).
        foreach (var pending in db.ChangeTracker.Entries<InventoryLot>()
                     .Where(e => e.State == EntityState.Added && e.Entity.ProductId == productId
                              && e.Entity.OwnerId == ownerId && e.Entity.QtyRemaining > 0)
                     .Select(e => e.Entity))
            if (!lots.Contains(pending)) lots.Add(pending);
        lots = lots.Where(l => l.QtyRemaining > 0).OrderBy(l => l.ReceivedAt).ToList();   // stable: keeps Id order

        long available = lots.Sum(l => l.QtyRemaining);
        if (available < qty)
        {
            var info = await db.Products.Where(p => p.Id == productId).Select(p => new { p.Name, p.Unit }).FirstAsync();
            var owner = await db.Owners.Where(o => o.Id == ownerId).Select(o => o.Name).FirstAsync();
            throw new DomainException($"Inventario insuficiente de \"{info.Name}\" para {owner}: " +
                                      $"hay {Qty.Format(available, info.Unit)}, se necesitan {Qty.Format(qty, info.Unit)}.");
        }

        var taken = new List<(InventoryLot, long)>();
        long remaining = qty;
        var now = clock.Now;
        foreach (var lot in lots)
        {
            if (remaining == 0) break;
            long take = Math.Min(remaining, lot.QtyRemaining);
            lot.QtyRemaining -= take;
            remaining -= take;
            taken.Add((lot, take));
            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = productId, OwnerId = ownerId, Lot = lot, Type = type, Quantity = -take,
                UnitCostCents = lot.UnitCostCents, UnitPriceCents = unitPriceCents, Reason = reason,
                UserId = user.UserId, CreatedAt = now, Sale = sale
            });
        }
        return taken;
    }

    // ------------------------------------------------------------------ public operations (transactional)

    public async Task AddStockAsync(int productId, int ownerId, long qty, long unitCostCents, MovementType type, string? reason, int? supplierId = null)
    {
        if (type is not (MovementType.InitialStock or MovementType.Restock or MovementType.Return or MovementType.Correction))
            throw new DomainException("Tipo de entrada no válido.");
        if (type is MovementType.Correction or MovementType.Return && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Indica el motivo del ajuste.");
        await EnsureActiveAsync(productId, ownerId);

        using var _ = await gate.EnterAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        AddStockCore(productId, ownerId, qty, unitCostCents, type, TextNorm.Clean(reason), supplierId);
        if (type is MovementType.Correction or MovementType.Return)
            audit.Add("Ajuste de inventario (entrada)", "Product", productId,
                $"{await DescribeAsync(productId, ownerId, qty)} a {Money.Format(unitCostCents)}. Motivo: {reason}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task RemoveStockAsync(int productId, int ownerId, long qty, MovementType type, string reason)
    {
        if (type is not (MovementType.Damaged or MovementType.Correction))
            throw new DomainException("Tipo de salida no válido.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Indica el motivo del ajuste.");

        using var _ = await gate.EnterAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await ConsumeFifoCoreAsync(productId, ownerId, qty, type, reason.Trim());
        audit.Add(type == MovementType.Damaged ? "Merma registrada" : "Ajuste de inventario (salida)", "Product", productId,
            $"{await DescribeAsync(productId, ownerId, qty)}. Motivo: {reason}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Physical count: sets an owner's stock to <paramref name="counted"/> with a correction movement.</summary>
    public async Task<long> AdjustToCountAsync(int productId, int ownerId, long counted, string reason)
    {
        if (counted < 0) throw new DomainException("El conteo no puede ser negativo.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Indica el motivo del ajuste.");

        using var _ = await gate.EnterAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        long current = await GetStockAsync(productId, ownerId);
        long diff = counted - current;
        if (diff == 0) return 0;

        if (diff > 0)
        {
            // New units valued at the owner's latest cost, or the product's reference cost.
            long cost = await db.InventoryLots.Where(l => l.ProductId == productId && l.OwnerId == ownerId)
                                .OrderByDescending(l => l.ReceivedAt).Select(l => (long?)l.UnitCostCents).FirstOrDefaultAsync()
                        ?? await db.Products.Where(p => p.Id == productId).Select(p => p.PurchasePriceCents).FirstAsync();
            AddStockCore(productId, ownerId, diff, cost, MovementType.Correction, "Conteo físico: " + reason.Trim());
        }
        else
        {
            await ConsumeFifoCoreAsync(productId, ownerId, -diff, MovementType.Correction, "Conteo físico: " + reason.Trim());
        }
        var unit = await db.Products.Where(p => p.Id == productId).Select(p => p.Unit).FirstAsync();
        audit.Add("Ajuste por conteo físico", "Product", productId,
            $"{await DescribeAsync(productId, ownerId, null)}: {Qty.Format(current, unit)} → {Qty.Format(counted, unit)}. Motivo: {reason}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return diff;
    }

    /// <summary>One owner sells/hands stock to another at an agreed price per unit (feature from StorePOS 1).</summary>
    public async Task TransferAsync(int productId, int fromOwnerId, int toOwnerId, long qty, long unitPriceCents, string? reason)
    {
        if (fromOwnerId == toOwnerId) throw new DomainException("Elige dueños distintos.");
        await EnsureActiveAsync(productId, toOwnerId);

        using var _ = await gate.EnterAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var fromName = await db.Owners.Where(o => o.Id == fromOwnerId).Select(o => o.Name).FirstAsync();
        var toName = await db.Owners.Where(o => o.Id == toOwnerId).Select(o => o.Name).FirstAsync();
        var note = $"Traspaso {fromName} → {toName}" + (string.IsNullOrWhiteSpace(reason) ? "" : ": " + reason.Trim());
        await ConsumeFifoCoreAsync(productId, fromOwnerId, qty, MovementType.TransferOut, note, unitPriceCents);
        AddStockCore(productId, toOwnerId, qty, unitPriceCents, MovementType.TransferIn, note);
        audit.Add("Traspaso entre dueños", "Product", productId,
            $"{await DescribeAsync(productId, fromOwnerId, qty)} → {toName} a {Money.Format(unitPriceCents)} c/u");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task EnsureActiveAsync(int productId, int ownerId)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId)) throw new DomainException("Producto no encontrado.");
        var owner = await db.Owners.Where(o => o.Id == ownerId).Select(o => new { o.IsActive }).FirstOrDefaultAsync()
                    ?? throw new DomainException("Dueño no encontrado.");
        if (!owner.IsActive) throw new DomainException("El dueño está inactivo.");
    }

    private async Task<string> DescribeAsync(int productId, int ownerId, long? qty)
    {
        var p = await db.Products.Where(x => x.Id == productId).Select(x => new { x.Name, x.Unit }).FirstAsync();
        var o = await db.Owners.Where(x => x.Id == ownerId).Select(x => x.Name).FirstAsync();
        return qty is null ? $"{p.Name} ({o})" : $"{Qty.Format(qty.Value, p.Unit)} de {p.Name} ({o})";
    }
}
