using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Reporting;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;

namespace StorePos.Core.Services;

public class DeliveryService(StoreDbContext db, InventoryService inventory, AuditService audit, ICurrentUser user, IClock clock, WriteGate gate)
{
    public async Task<int> RegisterAsync(DeliveryInput input)
    {
        using var _ = await gate.EnterAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var delivery = await RegisterCoreAsync(input);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return delivery.Id;
    }

    /// <summary>Creates the delivery, its lines, one inventory lot + Restock movement per line. Saves, no transaction.</summary>
    internal async Task<Delivery> RegisterCoreAsync(DeliveryInput input)
    {
        var lines = input.Items.Where(i => i.Quantity > 0).ToList();
        if (lines.Count == 0) throw new DomainException("La entrega no tiene productos.");
        if (lines.Any(i => i.UnitCostCents < 0)) throw new DomainException("Los costos no pueden ser negativos.");

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == input.SupplierId)
                       ?? throw new DomainException("Selecciona el proveedor.");
        var ownerIds = lines.Select(l => l.OwnerId).Distinct().ToList();
        var activeOwners = await db.Owners.Where(o => ownerIds.Contains(o.Id) && o.IsActive).Select(o => o.Id).ToListAsync();
        if (activeOwners.Count != ownerIds.Count) throw new DomainException("Cada producto recibido debe tener un dueño activo.");
        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        if (products.Count != productIds.Count) throw new DomainException("Uno de los productos no existe.");

        var delivery = new Delivery
        {
            SupplierId = supplier.Id, ReceivedAt = clock.Now, InvoiceNumber = TextNorm.Clean(input.InvoiceNumber),
            Notes = TextNorm.Clean(input.Notes), StockingListId = input.StockingListId, UserId = user.UserId
        };
        db.Deliveries.Add(delivery);

        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            var item = new DeliveryItem
            {
                Delivery = delivery, ProductId = product.Id, OwnerId = line.OwnerId, Quantity = line.Quantity,
                UnitCostCents = line.UnitCostCents,
                LineCostCents = Money.LineAmount(line.UnitCostCents, line.Quantity, product.Unit)
            };
            delivery.Items.Add(item);
            inventory.AddStockCore(product.Id, line.OwnerId, line.Quantity, line.UnitCostCents, MovementType.Restock,
                                   $"Entrega de {supplier.Name}", supplier.Id, delivery, item);

            if (input.UpdateReferenceCost && product.PurchasePriceCents != line.UnitCostCents)
            {
                audit.Add("Precio modificado", "Product", product.Id,
                    $"{product.Name}: costo de referencia {Money.Format(product.PurchasePriceCents)} → {Money.Format(line.UnitCostCents)} (entrega de {supplier.Name})");
                product.PurchasePriceCents = line.UnitCostCents;
                product.UpdatedAt = clock.Now;
            }
        }
        delivery.TotalCostCents = delivery.Items.Sum(i => i.LineCostCents);

        // Customer requests for these products are now in stock.
        var requests = await db.CustomerRequests
            .Where(r => r.ProductId != null && productIds.Contains(r.ProductId.Value)
                     && (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Ordered)).ToListAsync();
        foreach (var r in requests) r.Status = RequestStatus.Stocked;

        await db.SaveChangesAsync();
        return delivery;
    }

    public Task<PagedResult<DeliveryRow>> SearchAsync(int? supplierId, DateOnly? from, DateOnly? to, int page, int pageSize = 50)
    {
        var q = db.Deliveries.AsNoTracking().AsQueryable();
        if (supplierId is not null) q = q.Where(d => d.SupplierId == supplierId);
        if (from is { } f) { var d0 = f.ToDateTime(TimeOnly.MinValue); q = q.Where(d => d.ReceivedAt >= d0); }
        if (to is { } t) { var d1 = t.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(d => d.ReceivedAt < d1); }
        return PagedResult<DeliveryRow>.CreateAsync(q.OrderByDescending(d => d.Id).Select(d => new DeliveryRow
        {
            Id = d.Id, ReceivedAt = d.ReceivedAt, SupplierName = d.Supplier!.Name, InvoiceNumber = d.InvoiceNumber,
            ItemCount = d.Items.Count, TotalCostCents = d.TotalCostCents, UserName = d.User != null ? d.User.DisplayName : null
        }), page, pageSize);
    }

    public Task<Delivery?> GetAsync(int id) =>
        db.Deliveries.AsNoTracking().Include(d => d.Supplier).Include(d => d.User)
          .Include(d => d.Items).ThenInclude(i => i.Product).Include(d => d.Items).ThenInclude(i => i.Owner)
          .AsSplitQuery().FirstOrDefaultAsync(d => d.Id == id);
}

public class CustomerRequestService(StoreDbContext db, ICurrentUser user, IClock clock)
{
    /// <summary>Registers one customer asking for a product. Repeated names/barcodes are aggregated.</summary>
    public async Task<CustomerRequest> AddAsync(string productName, string? barcode, int? categoryId, long quantity, string? notes)
    {
        var name = TextNorm.Clean(productName) ?? throw new DomainException("Escribe el nombre del producto solicitado.");
        var norm = TextNorm.Normalize(name);
        var code = TextNorm.Clean(barcode);
        var now = clock.Now;

        var existing = await db.CustomerRequests
            .Where(r => (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Ordered)
                     && (r.NormalizedName == norm || (code != null && r.Barcode == code)))
            .OrderBy(r => r.Id).FirstOrDefaultAsync();

        if (existing is null)
        {
            var productId = await db.Products
                .Where(p => (code != null && p.Barcode == code) || p.SearchName == norm)
                .Select(p => (int?)p.Id).FirstOrDefaultAsync();
            existing = new CustomerRequest
            {
                ProductName = name, NormalizedName = norm, Barcode = code, CategoryId = categoryId, ProductId = productId,
                FirstRequestedAt = now, Notes = TextNorm.Clean(notes)
            };
            db.CustomerRequests.Add(existing);
        }
        else
        {
            existing.Barcode ??= code;
            existing.CategoryId ??= categoryId;
            if (TextNorm.Clean(notes) is { } n)
                existing.Notes = existing.Notes is null ? n : (existing.Notes + " | " + n)[..Math.Min(1000, existing.Notes.Length + 3 + n.Length)];
        }

        existing.RequestCount++;
        existing.TotalQuantity += Math.Max(0, quantity);
        existing.LastRequestedAt = now;
        existing.Events.Add(new CustomerRequestEvent { RequestedAt = now, Quantity = Math.Max(0, quantity), Notes = TextNorm.Clean(notes), UserId = user.UserId });
        await db.SaveChangesAsync();
        return existing;
    }

    public Task<PagedResult<CustomerRequest>> SearchAsync(RequestStatus? status, string? text, int page, int pageSize = 50)
    {
        var q = db.CustomerRequests.AsNoTracking().Include(r => r.Category).Include(r => r.Product).AsQueryable();
        if (status is not null) q = q.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var like = "%" + TextNorm.EscapeLike(TextNorm.Normalize(text)) + "%";
            q = q.Where(r => EF.Functions.Like(r.NormalizedName, like, "\\"));
        }
        return PagedResult<CustomerRequest>.CreateAsync(
            q.OrderBy(r => r.Status).ThenByDescending(r => r.RequestCount).ThenByDescending(r => r.LastRequestedAt), page, pageSize);
    }

    public async Task SetStatusAsync(int id, RequestStatus status, int? productId = null)
    {
        var r = await db.CustomerRequests.FindAsync(id) ?? throw new DomainException("Solicitud no encontrada.");
        r.Status = status;
        if (productId is not null) r.ProductId = productId;
        await db.SaveChangesAsync();
    }

    public Task<int> CountPendingAsync() => db.CustomerRequests.CountAsync(r => r.Status == RequestStatus.Pending);

    public Task<List<CustomerRequest>> GetTopPendingAsync(int count) =>
        db.CustomerRequests.AsNoTracking().Where(r => r.Status == RequestStatus.Pending)
          .OrderByDescending(r => r.RequestCount).ThenByDescending(r => r.LastRequestedAt).Take(count).ToListAsync();
}

/// <summary>Reorder suggestions and stocking lists (shopping lists). Nothing here adds stock by itself.</summary>
public class StockingService(StoreDbContext db, SettingsService settings, TrendService trends, DeliveryService deliveries,
                             AuditService audit, ICurrentUser user, IClock clock, WriteGate gate)
{
    /// <summary>
    /// suggested = ceil(avgDaily × cycle + safety − max(0, stock − avgDaily × daysToNext)),
    /// where safety = max(minStock, ceil(avgDaily × safetyDays)). Pure function, unit-tested.
    /// </summary>
    public static long SuggestQuantity(long stock, double avgDaily, int daysToNext, double cycleDays, long minStock, int safetyDays)
    {
        long safety = Math.Max(minStock, (long)Math.Ceiling(avgDaily * safetyDays));
        if (avgDaily <= 0) return Math.Max(0, minStock - Math.Max(0, stock));
        double atArrival = Math.Max(0, stock - avgDaily * daysToNext);
        double need = avgDaily * cycleDays + safety - atArrival;
        return Math.Max(0, (long)Math.Ceiling(need - 1e-9));
    }

    public async Task<List<ReorderSuggestion>> GetSuggestionsAsync(bool onlyNeeded = true)
    {
        var today = clock.Today;
        int historyDays = Math.Max(7, await settings.GetIntAsync(SettingKeys.HistoryDays));
        int safetyDays = Math.Max(0, await settings.GetIntAsync(SettingKeys.SafetyDays));
        int defaultCycle = Math.Max(1, await settings.GetIntAsync(SettingKeys.DefaultCycleDays));
        int margin = await settings.GetIntAsync(SettingKeys.LowStockMarginPercent);

        var products = await db.Products.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Name, p.Unit, p.MinStock, p.PurchasePriceCents, p.PreferredSupplierId,
                               SupplierName = p.PreferredSupplier != null ? p.PreferredSupplier.Name : null, p.PriorityOwnerId, p.CreatedAt })
            .ToListAsync();
        var stock = await db.InventoryLots.AsNoTracking().GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(l => l.QtyRemaining), FirstSeen = g.Min(l => l.ReceivedAt) })
            .ToDictionaryAsync(x => x.ProductId);
        // Owner of each product's most recent lot: default owner for the reorder line.
        var latestLotIds = await db.InventoryLots.AsNoTracking().GroupBy(l => l.ProductId).Select(g => g.Max(l => l.Id)).ToListAsync();
        var lastOwner = await db.InventoryLots.AsNoTracking().Where(l => latestLotIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.ProductId, l => l.OwnerId);
        var demand = await trends.GetDemandAsync(today, historyDays);
        var schedules = (await db.SupplierSchedules.AsNoTracking().ToListAsync())
            .GroupBy(s => s.SupplierId).ToDictionary(g => g.Key, g => g.Select(s => s.Day).ToList());
        var requests = await db.CustomerRequests.AsNoTracking()
            .Where(r => r.ProductId != null && (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Ordered))
            .GroupBy(r => r.ProductId!.Value)
            .Select(g => new { ProductId = g.Key, Count = g.Sum(r => r.RequestCount), Qty = g.Sum(r => r.TotalQuantity) })
            .ToDictionaryAsync(x => x.ProductId);

        var result = new List<ReorderSuggestion>();
        foreach (var p in products)
        {
            long qty = stock.TryGetValue(p.Id, out var s) ? s.Qty : 0;
            demand.TryGetValue(p.Id, out var d);
            double avg = d?.AdjustedDailyAverage ?? 0;
            var days = p.PreferredSupplierId is int sid && schedules.TryGetValue(sid, out var ds) ? ds : new List<DayOfWeek>();
            int daysToNext = SupplierService.DaysToNextDelivery(days, today) ?? defaultCycle;
            double cycle = SupplierService.CycleDays(days) ?? defaultCycle;
            requests.TryGetValue(p.Id, out var req);

            long suggested = SuggestQuantity(qty, avg, daysToNext, cycle, p.MinStock, safetyDays);
            if (avg <= 0 && req is not null) suggested = Math.Max(suggested, Math.Max(req.Qty, Qty.Scale(p.Unit)) - Math.Max(0, qty));
            if (p.Unit == UnitType.Kilogram && suggested > 0) suggested = (long)Math.Ceiling(suggested / 500.0) * 500;   // round up to ½ kg

            var reasons = new List<string>();
            var status = InventoryService.StatusOf(qty, p.MinStock, margin);
            if (status == StockStatus.Out) reasons.Add("Sin existencia");
            else if (status == StockStatus.Low) reasons.Add("Bajo el mínimo");
            else if (status == StockStatus.Approaching) reasons.Add("Cerca del mínimo");
            double? cover = avg > 0 ? qty / avg : null;
            if (cover is not null && qty > 0 && cover < daysToNext) reasons.Add($"Se agota en ~{cover:0.#} días, antes de la próxima entrega ({daysToNext} d)");
            if (d is { Trend: TrendDirection.Up }) reasons.Add("Demanda en aumento");
            if (req is not null) reasons.Add($"Solicitado por clientes ({req.Count})");

            bool needed = reasons.Count > 0 && suggested > 0 || status == StockStatus.Out;
            if (onlyNeeded && !needed) continue;

            result.Add(new ReorderSuggestion
            {
                ProductId = p.Id, Name = p.Name, Unit = p.Unit, SupplierId = p.PreferredSupplierId, SupplierName = p.SupplierName,
                OwnerId = p.PriorityOwnerId ?? (lastOwner.TryGetValue(p.Id, out var lo) ? (int?)lo : null),
                CurrentStock = qty, MinStock = p.MinStock, AvgDailySales = avg, SeasonalFactor = d?.SeasonalFactor ?? 1,
                Reliable = d?.Reliable ?? false, DaysToNextDelivery = daysToNext, DaysOfCover = cover,
                SuggestedQty = suggested, EstimatedUnitCostCents = p.PurchasePriceCents,
                RequestCount = req?.Count ?? 0, Reasons = reasons
            });
        }
        return result.OrderBy(r => r.CurrentStock > 0).ThenBy(r => r.DaysOfCover ?? double.MaxValue).ThenBy(r => r.Name).ToList();
    }

    public async Task<int> GenerateListAsync(string? name, StockingGroupBy groupBy, int? supplierId, int? ownerId, bool includeRequests)
    {
        var suggestions = (await GetSuggestionsAsync()).Where(s => s.SuggestedQty > 0
            && (supplierId == null || s.SupplierId == supplierId) && (ownerId == null || s.OwnerId == ownerId)).ToList();

        var list = new StockingList
        {
            Name = TextNorm.Clean(name) ?? $"Lista automática {clock.Now:dd/MM/yyyy HH:mm}",
            IsAutoGenerated = true, GroupBy = groupBy, CreatedAt = clock.Now, CreatedByUserId = user.UserId
        };
        foreach (var s in suggestions)
            list.Items.Add(new StockingListItem
            {
                ProductId = s.ProductId, Description = s.Name, SupplierId = s.SupplierId, OwnerId = s.OwnerId,
                CurrentStock = s.CurrentStock, AvgDailySales = Math.Round(s.AvgDailySales, 3), DaysToNextDelivery = s.DaysToNextDelivery,
                SuggestedQty = s.SuggestedQty, Quantity = s.SuggestedQty, EstimatedUnitCostCents = s.EstimatedUnitCostCents,
                Reason = string.Join("; ", s.Reasons) is { Length: > 300 } r ? r[..300] : string.Join("; ", s.Reasons)
            });

        if (includeRequests && supplierId is null)
        {
            var unmatched = await db.CustomerRequests.AsNoTracking()
                .Where(r => r.ProductId == null && r.Status == RequestStatus.Pending).OrderByDescending(r => r.RequestCount).ToListAsync();
            foreach (var r in unmatched)
                list.Items.Add(new StockingListItem
                {
                    CustomerRequestId = r.Id, Description = r.ProductName, Quantity = Math.Max(1, r.TotalQuantity), SuggestedQty = Math.Max(1, r.TotalQuantity),
                    Reason = $"Producto nuevo solicitado por clientes ({r.RequestCount})"
                });
        }

        if (list.Items.Count == 0) throw new DomainException("No hay productos que necesiten resurtirse con esos filtros.");
        db.StockingLists.Add(list);
        await db.SaveChangesAsync();
        return list.Id;
    }

    public async Task<int> CreateEmptyAsync(string name, StockingGroupBy groupBy)
    {
        var list = new StockingList
        {
            Name = TextNorm.Clean(name) ?? $"Lista {clock.Now:dd/MM/yyyy}", GroupBy = groupBy,
            CreatedAt = clock.Now, CreatedByUserId = user.UserId
        };
        db.StockingLists.Add(list);
        await db.SaveChangesAsync();
        return list.Id;
    }

    public Task<StockingList?> GetAsync(int id) =>
        db.StockingLists.AsNoTracking()
          .Include(l => l.Items).ThenInclude(i => i.Product)
          .Include(l => l.Items).ThenInclude(i => i.Supplier)
          .Include(l => l.Items).ThenInclude(i => i.Owner)
          .Include(l => l.CreatedByUser)
          .AsSplitQuery().FirstOrDefaultAsync(l => l.Id == id);

    public Task<PagedResult<StockingList>> SearchAsync(StockingListStatus? status, int page, int pageSize = 50)
    {
        var q = db.StockingLists.AsNoTracking().Include(l => l.Items).AsQueryable();
        if (status is not null) q = q.Where(l => l.Status == status);
        return PagedResult<StockingList>.CreateAsync(q.OrderByDescending(l => l.Id), page, pageSize);
    }

    private async Task<StockingList> LoadEditableAsync(int id)
    {
        var list = await db.StockingLists.Include(l => l.Items).FirstOrDefaultAsync(l => l.Id == id)
                   ?? throw new DomainException("Lista no encontrada.");
        if (list.Status is StockingListStatus.Received or StockingListStatus.Cancelled)
            throw new DomainException("La lista ya fue recibida o cancelada y no se puede modificar.");
        return list;
    }

    public async Task UpdateItemsAsync(int listId, string? name, string? notes, StockingGroupBy groupBy, IEnumerable<StockingItemEdit> edits)
    {
        var list = await LoadEditableAsync(listId);
        list.Name = TextNorm.Clean(name) ?? list.Name;
        list.Notes = TextNorm.Clean(notes);
        list.GroupBy = groupBy;
        foreach (var e in edits)
        {
            var item = list.Items.FirstOrDefault(i => i.Id == e.ItemId);
            if (item is null) continue;
            if (e.Remove) { db.StockingListItems.Remove(item); continue; }
            if (e.Quantity < 0 || e.EstimatedUnitCostCents < 0) throw new DomainException("Cantidades y costos no pueden ser negativos.");
            item.Quantity = e.Quantity;
            item.EstimatedUnitCostCents = e.EstimatedUnitCostCents;
            item.OwnerId = e.OwnerId;
            item.SupplierId = e.SupplierId;
        }
        await db.SaveChangesAsync();
    }

    public async Task AddItemAsync(int listId, int productId, long quantity)
    {
        var list = await LoadEditableAsync(listId);
        if (quantity <= 0) throw new DomainException("La cantidad debe ser mayor a cero.");
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId) ?? throw new DomainException("Producto no encontrado.");
        var stock = await db.InventoryLots.Where(l => l.ProductId == productId).SumAsync(l => l.QtyRemaining);
        list.Items.Add(new StockingListItem
        {
            ProductId = p.Id, Description = p.Name, SupplierId = p.PreferredSupplierId, OwnerId = p.PriorityOwnerId,
            CurrentStock = stock, Quantity = quantity, EstimatedUnitCostCents = p.PurchasePriceCents, Reason = "Agregado manualmente"
        });
        await db.SaveChangesAsync();
    }

    public async Task SetStatusAsync(int listId, StockingListStatus status)
    {
        if (status is StockingListStatus.Received or StockingListStatus.Draft)
            throw new DomainException("Usa la opción de recibir mercancía.");
        var list = await LoadEditableAsync(listId);
        list.Status = status;
        if (status == StockingListStatus.Purchased)
        {
            list.PurchasedAt = clock.Now;
            var reqIds = list.Items.Where(i => i.CustomerRequestId != null).Select(i => i.CustomerRequestId!.Value).ToList();
            var prodIds = list.Items.Where(i => i.ProductId != null && i.Quantity > 0).Select(i => i.ProductId!.Value).ToList();
            var reqs = await db.CustomerRequests.Where(r => r.Status == RequestStatus.Pending &&
                         (reqIds.Contains(r.Id) || (r.ProductId != null && prodIds.Contains(r.ProductId.Value)))).ToListAsync();
            foreach (var r in reqs) r.Status = RequestStatus.Ordered;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Converts what actually arrived into inventory: one delivery per supplier, lots per owner.
    /// Only lines with a product, supplier, owner and quantity &gt; 0 are received.
    /// </summary>
    public async Task<List<int>> ReceiveAsync(int listId, IEnumerable<ReceiveLine> received, string? invoiceNumber, bool updateReferenceCost)
    {
        using var _ = await gate.EnterAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var list = await LoadEditableAsync(listId);
        var lines = received.Where(r => r.Quantity > 0).ToList();
        if (lines.Count == 0) throw new DomainException("Indica al menos una cantidad recibida.");

        var resolved = new List<(int SupplierId, DeliveryLineInput Line)>();
        foreach (var r in lines)
        {
            var item = list.Items.FirstOrDefault(i => i.Id == r.ItemId) ?? throw new DomainException("Renglón no encontrado.");
            if (item.ProductId is null)
                throw new DomainException($"\"{item.Description}\" todavía no es un producto. Regístralo en Productos y agrégalo a la lista.");
            int supplierId = r.SupplierId ?? item.SupplierId ?? throw new DomainException($"Asigna el proveedor de \"{item.Description}\".");
            int ownerId = r.OwnerId ?? item.OwnerId ?? throw new DomainException($"Asigna el dueño de \"{item.Description}\".");
            resolved.Add((supplierId, new DeliveryLineInput { ProductId = item.ProductId.Value, OwnerId = ownerId, Quantity = r.Quantity, UnitCostCents = r.UnitCostCents }));
        }

        var ids = new List<int>();
        foreach (var group in resolved.GroupBy(x => x.SupplierId))
        {
            var d = await deliveries.RegisterCoreAsync(new DeliveryInput
            {
                SupplierId = group.Key, InvoiceNumber = invoiceNumber, StockingListId = list.Id,
                Notes = $"Recibido de la lista \"{list.Name}\"", UpdateReferenceCost = updateReferenceCost,
                Items = group.Select(g => g.Line).ToList()
            });
            ids.Add(d.Id);
        }

        list.Status = StockingListStatus.Received;
        list.ReceivedAt = clock.Now;
        audit.Add("Lista de surtido recibida", "StockingList", list.Id, $"{list.Name}: {lines.Count} renglones, entregas #{string.Join(", #", ids)}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ids;
    }
}
