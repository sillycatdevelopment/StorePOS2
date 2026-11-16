using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;

namespace StorePos.Core.Services;

public class SalesService(StoreDbContext db, InventoryService inventory, CashService cash, SettingsService settings,
                          AuditService audit, ICurrentUser user, IClock clock, WriteGate gate)
{
    // ------------------------------------------------------------------ POS lookups

    public async Task<PosProduct?> LookupBarcodeAsync(string code)
    {
        var c = code.Trim();
        if (c.Length == 0) return null;
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Barcode == c && x.IsActive);
        return p is null ? null : (await ToPosAsync(new[] { p })).Single();
    }

    public async Task<List<PosProduct>> SearchAsync(string text, int max = 20)
    {
        var t = text.Trim();
        if (t.Length == 0) return new();
        var like = "%" + TextNorm.EscapeLike(TextNorm.Normalize(t)) + "%";
        var products = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && (p.Barcode == t || EF.Functions.Like(p.SearchName, like, "\\")))
            .OrderBy(p => p.SearchName).Take(max).ToListAsync();
        return await ToPosAsync(products);
    }

    private async Task<List<PosProduct>> ToPosAsync(IReadOnlyList<Product> products)
    {
        var ids = products.Select(p => p.Id).ToList();
        var stock = await db.InventoryLots.AsNoTracking()
            .Where(l => ids.Contains(l.ProductId) && l.QtyRemaining > 0)
            .GroupBy(l => new { l.ProductId, l.OwnerId, l.Owner!.Name })
            .Select(g => new { g.Key.ProductId, g.Key.OwnerId, g.Key.Name, Qty = g.Sum(l => l.QtyRemaining) })
            .ToListAsync();
        var mode = await settings.GetAllocationModeAsync();
        return products.Select(p =>
        {
            var owners = stock.Where(s => s.ProductId == p.Id).OrderBy(s => s.Name)
                              .Select(s => new PosOwnerStock(s.OwnerId, s.Name, s.Qty)).ToList();
            return new PosProduct(p.Id, p.Name, p.Barcode, p.Unit, p.SalePriceCents, owners,
                                  NeedsOwnerChoice: mode == AllocationMode.Manual && owners.Count > 1);
        }).ToList();
    }

    // ------------------------------------------------------------------ complete sale

    /// <summary>
    /// Records a sale atomically: prices and costs come from the database, stock is taken from
    /// owners' lots (FIFO inside each owner), and sale, lines, lot links, movements and audit rows
    /// are committed together or not at all.
    /// </summary>
    public async Task<int> CompleteSaleAsync(SaleRequest req)
    {
        if (req.Lines is null || req.Lines.Count == 0) throw new DomainException("La venta no tiene productos.");
        if (req.Lines.Any(l => l.Quantity <= 0)) throw new DomainException("Todas las cantidades deben ser mayores a cero.");
        if (req.DiscountCents < 0 || req.Lines.Any(l => l.DiscountCents < 0)) throw new DomainException("Descuento no válido.");
        bool hasDiscount = req.DiscountCents > 0 || req.Lines.Any(l => l.DiscountCents > 0);
        if (hasDiscount && !user.Role.AtLeast(UserRole.Manager))
            throw new DomainException("Solo un encargado o administrador puede aplicar descuentos.");
        if (req.AuthorizeNoStock && !user.Role.AtLeast(UserRole.Manager))
            throw new DomainException("Solo un encargado o administrador puede autorizar ventas sin existencia.");

        using var _ = await gate.EnterAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var session = await cash.GetOpenSessionAsync() ?? throw new DomainException("La caja está cerrada. Abre la caja antes de vender.");
        var method = await db.PaymentMethods.FirstOrDefaultAsync(m => m.Id == req.PaymentMethodId && m.IsActive)
                     ?? throw new DomainException("Selecciona una forma de pago válida.");

        var productIds = req.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.Include(p => p.Category).Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var ownerNames = await db.Owners.ToDictionaryAsync(o => o.Id, o => o.Name);
        var mode = await settings.GetAllocationModeAsync();
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);

        var sale = new Sale
        {
            CreatedAt = now, BusinessDate = today, UserId = user.UserId, DailyClosingId = session.Id,
            PaymentMethodId = method.Id, PaymentMethodName = method.Name, PaymentIsCash = method.IsCash,
            Notes = TextNorm.Clean(req.Notes)
        };
        db.Sales.Add(sale);

        // Merge repeated scans of the same product/owner into one line.
        var lines = req.Lines.GroupBy(l => new { l.ProductId, l.OwnerId })
            .Select(g => new SaleLineRequest { ProductId = g.Key.ProductId, OwnerId = g.Key.OwnerId,
                                               Quantity = g.Sum(x => x.Quantity), DiscountCents = g.Sum(x => x.DiscountCents) })
            .ToList();

        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product) || !product.IsActive)
                throw new DomainException("Uno de los productos no existe o está inactivo.");

            var split = await AllocateAsync(product, line.Quantity, line.OwnerId, mode, req.AuthorizeNoStock);
            var shares = SplitProportional(line.DiscountCents, split.Select(s => s.Qty).ToList());

            for (int i = 0; i < split.Count; i++)
            {
                var (ownerId, qty) = split[i];
                long gross = Money.LineAmount(product.SalePriceCents, qty, product.Unit);
                long disc = Math.Min(shares[i], gross);
                var item = new SaleItem
                {
                    Sale = sale, ProductId = product.Id, OwnerId = ownerId,
                    ProductName = product.Name, Barcode = product.Barcode,
                    CategoryId = product.CategoryId, CategoryName = product.Category?.Name,
                    SupplierId = product.PreferredSupplierId, OwnerName = ownerNames[ownerId],
                    Unit = product.Unit, Quantity = qty, UnitPriceCents = product.SalePriceCents,
                    DiscountCents = disc, LineTotalCents = gross - disc,
                    BusinessDate = today, YearMonth = today.Year * 100 + today.Month
                };

                var taken = await inventory.ConsumeFifoCoreAsync(product.Id, ownerId, qty, MovementType.Sale,
                                                                 null, product.SalePriceCents, sale);
                long cost = 0;
                foreach (var (lot, q) in taken)
                {
                    item.Lots.Add(new SaleItemLot { Lot = lot, Quantity = q, UnitCostCents = lot.UnitCostCents });
                    cost += Money.LineAmount(lot.UnitCostCents, q, product.Unit);
                }
                item.CostCents = cost;
                item.UnitCostCents = (long)Math.Round(cost * (decimal)Qty.Scale(product.Unit) / qty, MidpointRounding.AwayFromZero);
                sale.Items.Add(item);
            }
        }

        // Whole-ticket discount, spread over lines in proportion to their amount (keeps per-owner profit exact).
        if (req.DiscountCents > 0)
        {
            long remainingTotal = sale.Items.Sum(i => i.LineTotalCents);
            if (req.DiscountCents > remainingTotal) throw new DomainException("El descuento no puede ser mayor al total.");
            var shares = SplitProportional(req.DiscountCents, sale.Items.Select(i => i.LineTotalCents).ToList());
            for (int i = 0; i < sale.Items.Count; i++)
            {
                sale.Items[i].DiscountCents += shares[i];
                sale.Items[i].LineTotalCents -= shares[i];
            }
        }

        sale.SubtotalCents = sale.Items.Sum(i => i.LineTotalCents + i.DiscountCents);
        sale.DiscountCents = sale.Items.Sum(i => i.DiscountCents);
        sale.TotalCents = sale.SubtotalCents - sale.DiscountCents;
        sale.CostCents = sale.Items.Sum(i => i.CostCents);
        sale.ProfitCents = sale.TotalCents - sale.CostCents;

        if (method.IsCash)
        {
            long received = req.CashReceivedCents == 0 ? sale.TotalCents : req.CashReceivedCents;
            if (received < sale.TotalCents)
                throw new DomainException($"El efectivo recibido ({Money.Format(received)}) es menor al total ({Money.Format(sale.TotalCents)}).");
            sale.CashReceivedCents = received;
            sale.ChangeCents = received - sale.TotalCents;
        }
        else
        {
            sale.CashReceivedCents = sale.TotalCents;
            sale.ChangeCents = 0;
        }

        await db.SaveChangesAsync();   // assigns sale.Id

        if (sale.DiscountCents > 0)
            audit.Add("Descuento aplicado", "Sale", sale.Id, $"Venta #{sale.Id}: descuento {Money.Format(sale.DiscountCents)} sobre {Money.Format(sale.SubtotalCents)}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return sale.Id;
    }

    /// <summary>Decides which owner(s) the quantity comes from. Never mixes owners silently in Manual mode.</summary>
    private async Task<List<(int OwnerId, long Qty)>> AllocateAsync(Product product, long qty, int? requestedOwner,
                                                                    AllocationMode mode, bool authorizeNoStock)
    {
        var stock = await db.InventoryLots
            .Where(l => l.ProductId == product.Id && l.QtyRemaining > 0)
            .GroupBy(l => l.OwnerId)
            .Select(g => new { OwnerId = g.Key, Qty = g.Sum(l => l.QtyRemaining), Oldest = g.Min(l => l.ReceivedAt) })
            .ToListAsync();

        // Lots already consumed earlier in this same sale are tracked but not saved yet: use in-memory balances.
        foreach (var entry in db.ChangeTracker.Entries<InventoryLot>().Where(e => e.Entity.ProductId == product.Id && e.State == EntityState.Modified))
        {
            var s = stock.FirstOrDefault(x => x.OwnerId == entry.Entity.OwnerId);
            if (s is null) continue;
            long delta = entry.Entity.QtyRemaining - (long)entry.OriginalValues[nameof(InventoryLot.QtyRemaining)]!;
            stock[stock.IndexOf(s)] = s with { Qty = s.Qty + delta };
        }
        stock = stock.Where(s => s.Qty > 0).ToList();

        var result = new List<(int, long)>();
        long remaining = qty;

        if (requestedOwner is int chosen)
        {
            var s = stock.FirstOrDefault(x => x.OwnerId == chosen);
            long take = Math.Min(remaining, s?.Qty ?? 0);
            if (take > 0) { result.Add((chosen, take)); remaining -= take; }
            if (remaining > 0) await CoverShortageAsync(product, chosen, remaining, authorizeNoStock, result);
            return result;
        }

        IEnumerable<int> order;
        switch (mode)
        {
            case AllocationMode.Manual:
                if (stock.Count > 1)
                    throw new DomainException($"\"{product.Name}\" tiene inventario de varios dueños. Selecciona de qué dueño se vende.");
                order = stock.Select(s => s.OwnerId);
                break;
            case AllocationMode.OldestFirst:
                order = stock.OrderBy(s => s.Oldest).Select(s => s.OwnerId);
                break;
            default:
                order = stock.OrderByDescending(s => s.OwnerId == product.PriorityOwnerId)
                             .ThenByDescending(s => s.Qty).Select(s => s.OwnerId);
                break;
        }

        foreach (var ownerId in order)
        {
            if (remaining == 0) break;
            long take = Math.Min(remaining, stock.First(s => s.OwnerId == ownerId).Qty);
            result.Add((ownerId, take));
            remaining -= take;
        }

        if (remaining > 0)
        {
            int? shortageOwner = result.Count > 0 ? result[^1].Item1 : product.PriorityOwnerId;
            if (shortageOwner is null)
            {
                var active = await db.Owners.Where(o => o.IsActive).Select(o => o.Id).Take(2).ToListAsync();
                if (active.Count == 1) shortageOwner = active[0];
            }
            if (shortageOwner is null)
            {
                if (!authorizeNoStock) throw new DomainException($"No hay existencia de \"{product.Name}\".");
                throw new DomainException($"Selecciona a qué dueño se le atribuye la venta sin existencia de \"{product.Name}\".");
            }
            await CoverShortageAsync(product, shortageOwner.Value, remaining, authorizeNoStock, result);
        }
        return result;
    }

    private Task CoverShortageAsync(Product product, int ownerId, long shortage, bool authorized, List<(int, long)> result)
    {
        if (!authorized)
            throw new DomainException($"Inventario insuficiente de \"{product.Name}\": faltan {Qty.Format(shortage, product.Unit)}. " +
                                      "Un encargado puede autorizar la venta sin existencia.");
        // Keep lots non-negative: register the missing units as an audited correction, then sell them normally.
        inventory.AddStockCore(product.Id, ownerId, shortage, product.PurchasePriceCents, MovementType.Correction,
                               "Venta autorizada sin existencia");
        audit.Add("Venta sin existencia autorizada", "Product", product.Id,
                  $"{Qty.Format(shortage, product.Unit)} de {product.Name} registrados a {Money.Format(product.PurchasePriceCents)} c/u");
        int idx = result.FindIndex(r => r.Item1 == ownerId);
        if (idx >= 0) result[idx] = (ownerId, result[idx].Item2 + shortage);
        else result.Add((ownerId, shortage));
        return Task.CompletedTask;
    }

    /// <summary>Splits an amount over weights, distributing rounding remainders so the parts add up exactly.</summary>
    public static long[] SplitProportional(long amount, IReadOnlyList<long> weights)
    {
        var parts = new long[weights.Count];
        if (amount == 0 || weights.Count == 0) return parts;
        long total = weights.Sum();
        if (total <= 0) { parts[0] = amount; return parts; }
        long assigned = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            parts[i] = amount * weights[i] / total;
            assigned += parts[i];
        }
        long left = amount - assigned;
        foreach (var i in Enumerable.Range(0, weights.Count).OrderByDescending(i => amount * weights[i] % total).ThenByDescending(i => weights[i]))
        {
            if (left == 0) break;
            parts[i]++;
            left--;
        }
        return parts;
    }

    // ------------------------------------------------------------------ cancel

    public async Task CancelSaleAsync(int saleId, string reason)
    {
        if (!user.Role.AtLeast(UserRole.Manager)) throw new DomainException("Solo un encargado o administrador puede cancelar ventas.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Indica el motivo de la cancelación.");

        using var _ = await gate.EnterAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var sale = await db.Sales.Include(s => s.DailyClosing).Include(s => s.Items).ThenInclude(i => i.Lots).ThenInclude(l => l.Lot)
                                 .FirstOrDefaultAsync(s => s.Id == saleId) ?? throw new DomainException("Venta no encontrada.");
        if (sale.Status == SaleStatus.Cancelled) throw new DomainException("La venta ya estaba cancelada.");

        var now = clock.Now;
        foreach (var item in sale.Items)
            foreach (var link in item.Lots)
            {
                link.Lot!.QtyRemaining += link.Quantity;
                db.InventoryMovements.Add(new InventoryMovement
                {
                    ProductId = item.ProductId, OwnerId = item.OwnerId, LotId = link.LotId,
                    Type = MovementType.SaleCancellation, Quantity = link.Quantity, UnitCostCents = link.UnitCostCents,
                    UnitPriceCents = item.UnitPriceCents, SaleId = sale.Id, Reason = reason.Trim(),
                    UserId = user.UserId, CreatedAt = now
                });
            }

        sale.Status = SaleStatus.Cancelled;
        sale.CancelledAt = now;
        sale.CancelledByUserId = user.UserId;
        sale.CancelReason = reason.Trim();

        string cashNote = "";
        if (sale.DailyClosing!.Status == ClosingStatus.Closed && sale.PaymentIsCash)
        {
            // The sale is already inside a closed day: the money leaves today's drawer as a refund.
            var open = await cash.GetOpenSessionAsync()
                       ?? throw new DomainException("La venta pertenece a un día ya cerrado. Abre la caja para registrar el reembolso en efectivo.");
            db.CashMovements.Add(new CashMovement
            {
                DailyClosingId = open.Id, Type = CashMovementType.Refund, AmountCents = sale.TotalCents, AffectsCash = true,
                Description = $"Reembolso por cancelación de venta #{sale.Id}", SaleId = sale.Id, UserId = user.UserId, CreatedAt = now
            });
            cashNote = " Reembolso en efectivo registrado en la caja actual.";
        }

        audit.Add("Venta cancelada", "Sale", sale.Id, $"Total {Money.Format(sale.TotalCents)}. Motivo: {reason.Trim()}.{cashNote}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    // ------------------------------------------------------------------ history

    public Task<PagedResult<SaleRow>> SearchSalesAsync(SaleFilter f, int page, int pageSize = 50)
    {
        var q = db.Sales.AsNoTracking().AsQueryable();
        if (f.From is { } from) q = q.Where(s => s.BusinessDate >= from);
        if (f.To is { } to) q = q.Where(s => s.BusinessDate <= to);
        if (f.Status is { } st) q = q.Where(s => s.Status == st);
        if (f.PaymentMethodId is { } pm) q = q.Where(s => s.PaymentMethodId == pm);
        if (f.OwnerId is { } ow) q = q.Where(s => s.Items.Any(i => i.OwnerId == ow));
        if (f.SaleId is { } id) q = q.Where(s => s.Id == id);
        if (!string.IsNullOrWhiteSpace(f.Product))
        {
            var like = "%" + TextNorm.EscapeLike(f.Product.Trim()) + "%";
            q = q.Where(s => s.Items.Any(i => EF.Functions.Like(i.ProductName, like, "\\") || i.Barcode == f.Product.Trim()));
        }
        return PagedResult<SaleRow>.CreateAsync(q.OrderByDescending(s => s.Id).Select(s => new SaleRow
        {
            Id = s.Id, CreatedAt = s.CreatedAt, PaymentMethodName = s.PaymentMethodName, TotalCents = s.TotalCents,
            ProfitCents = s.ProfitCents, Status = s.Status, ItemCount = s.Items.Count,
            UserName = s.User != null ? s.User.DisplayName : null
        }), page, pageSize);
    }

    public Task<Sale?> GetSaleAsync(int id) =>
        db.Sales.AsNoTracking().Include(s => s.Items).Include(s => s.User).Include(s => s.CancelledByUser)
          .AsSplitQuery().FirstOrDefaultAsync(s => s.Id == id);

    public Task<List<SaleRow>> GetLatestAsync(int count) =>
        db.Sales.AsNoTracking().OrderByDescending(s => s.Id).Take(count).Select(s => new SaleRow
        {
            Id = s.Id, CreatedAt = s.CreatedAt, PaymentMethodName = s.PaymentMethodName, TotalCents = s.TotalCents,
            ProfitCents = s.ProfitCents, Status = s.Status, ItemCount = s.Items.Count,
            UserName = s.User != null ? s.User.DisplayName : null
        }).ToListAsync();

    public Task<List<PaymentMethod>> GetPaymentMethodsAsync(bool includeInactive = false) =>
        db.PaymentMethods.AsNoTracking().Where(m => includeInactive || m.IsActive).OrderBy(m => m.SortOrder).ThenBy(m => m.Name).ToListAsync();

    public async Task SavePaymentMethodAsync(int? id, string name, bool isCash, bool isActive, int sortOrder)
    {
        if (!user.Role.AtLeast(UserRole.Administrator)) throw new DomainException("Solo un administrador puede cambiar formas de pago.");
        var clean = TextNorm.Clean(name) ?? throw new DomainException("El nombre es obligatorio.");
        if (await db.PaymentMethods.AnyAsync(m => m.Name == clean && m.Id != id)) throw new DomainException("Ya existe esa forma de pago.");
        PaymentMethod m;
        if (id is > 0) m = await db.PaymentMethods.FindAsync(id.Value) ?? throw new DomainException("No encontrada.");
        else db.PaymentMethods.Add(m = new PaymentMethod());
        m.Name = clean; m.IsCash = isCash; m.IsActive = isActive; m.SortOrder = sortOrder;
        audit.Add("Forma de pago guardada", "PaymentMethod", id, $"{clean} (efectivo: {(isCash ? "sí" : "no")}, activa: {(isActive ? "sí" : "no")})");
        await db.SaveChangesAsync();
    }
}
