using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Utilities;

namespace StorePos.Core.Services;

/// <summary>
/// Deleting records. Catalog records (products, owners, suppliers, categories, users, payment methods)
/// can be deleted only while nothing financial points to them; otherwise they must be deactivated.
/// Sales, inventory and cash closings are never deleted one by one (that would break totals and stock):
/// they are cleared together with <see cref="ResetOperationsAsync"/> or <see cref="ResetEverythingAsync"/>.
/// </summary>
public class DeletionService(StoreDbContext db, AuditService audit, ICurrentUser user, WriteGate gate,
                             BackupService backups, SettingsCache cache, IClock clock)
{
    private const string HasHistory =
        " ya tiene historial (ventas, inventario o entregas). Desactívalo, o usa Configuración → Borrar datos para limpiar las pruebas.";

    private void RequireManager()
    {
        if (!user.Role.AtLeast(UserRole.Manager)) throw new DomainException("Solo un encargado o administrador puede eliminar registros.");
    }

    private void RequireAdmin()
    {
        if (!user.Role.AtLeast(UserRole.Administrator)) throw new DomainException("Solo un administrador puede hacer esto.");
    }

    // ------------------------------------------------------------------ single records

    public async Task DeleteProductAsync(int id)
    {
        RequireManager();
        using var _ = await gate.EnterAsync();
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Producto no encontrado.");
        if (await db.InventoryLots.AnyAsync(x => x.ProductId == id) || await db.InventoryMovements.AnyAsync(x => x.ProductId == id)
            || await db.SaleItems.AnyAsync(x => x.ProductId == id) || await db.DeliveryItems.AnyAsync(x => x.ProductId == id))
            throw new DomainException($"\"{p.Name}\"" + HasHistory);

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.CustomerRequests.Where(r => r.ProductId == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.ProductId, (int?)null));
        await db.StockingListItems.Where(i => i.ProductId == id).ExecuteDeleteAsync();
        await db.Products.Where(x => x.Id == id).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();   // drop stale tracked copies of rows deleted directly in the database
        audit.Add("Producto eliminado", "Product", id, p.Name);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DeleteOwnerAsync(int id)
    {
        RequireManager();
        using var _ = await gate.EnterAsync();
        var o = await db.Owners.FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Dueño no encontrado.");
        if (await db.InventoryLots.AnyAsync(x => x.OwnerId == id) || await db.InventoryMovements.AnyAsync(x => x.OwnerId == id)
            || await db.SaleItems.AnyAsync(x => x.OwnerId == id) || await db.DeliveryItems.AnyAsync(x => x.OwnerId == id))
            throw new DomainException($"\"{o.Name}\"" + HasHistory);

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Products.Where(p => p.PriorityOwnerId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.PriorityOwnerId, (int?)null));
        await db.StockingListItems.Where(i => i.OwnerId == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.OwnerId, (int?)null));
        await db.Owners.Where(x => x.Id == id).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();   // drop stale tracked copies of rows deleted directly in the database
        audit.Add("Dueño eliminado", "Owner", id, o.Name);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DeleteSupplierAsync(int id)
    {
        RequireManager();
        using var _ = await gate.EnterAsync();
        var s0 = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Proveedor no encontrado.");
        if (await db.Deliveries.AnyAsync(d => d.SupplierId == id) || await db.InventoryLots.AnyAsync(l => l.SupplierId == id))
            throw new DomainException($"\"{s0.Name}\" ya tiene entregas registradas. Desactívalo, o usa Configuración → Borrar datos para limpiar las pruebas.");

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Products.Where(p => p.PreferredSupplierId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.PreferredSupplierId, (int?)null));
        await db.StockingListItems.Where(i => i.SupplierId == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.SupplierId, (int?)null));
        await db.SupplierSchedules.Where(x => x.SupplierId == id).ExecuteDeleteAsync();
        await db.Suppliers.Where(x => x.Id == id).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();   // drop stale tracked copies of rows deleted directly in the database
        audit.Add("Proveedor eliminado", "Supplier", id, s0.Name);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Products of the category become "Sin categoría". Past sales keep the category name they were sold with.</summary>
    public async Task DeleteCategoryAsync(int id)
    {
        RequireManager();
        var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Categoría no encontrada.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Products.Where(p => p.CategoryId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.CategoryId, (int?)null));
        await db.CustomerRequests.Where(r => r.CategoryId == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.CategoryId, (int?)null));
        await db.Categories.Where(x => x.Id == id).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();   // drop stale tracked copies of rows deleted directly in the database
        audit.Add("Categoría eliminada", "Category", id, c.Name);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DeletePaymentMethodAsync(int id)
    {
        RequireAdmin();
        var m = await db.PaymentMethods.FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Forma de pago no encontrada.");
        if (await db.Sales.AnyAsync(s => s.PaymentMethodId == id) || await db.DailyClosingPayments.AnyAsync(p => p.PaymentMethodId == id))
            throw new DomainException($"\"{m.Name}\" ya se usó en ventas. Desactívala, o usa Configuración → Borrar datos.");
        await db.PaymentMethods.Where(x => x.Id == id).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();   // drop stale tracked copies of rows deleted directly in the database
        audit.Add("Forma de pago eliminada", "PaymentMethod", id, m.Name);
        await db.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(int id)
    {
        RequireAdmin();
        if (id == user.UserId) throw new DomainException("No puedes eliminar tu propio usuario.");
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Usuario no encontrado.");
        if (u.Role == UserRole.Administrator && !await db.Users.AnyAsync(x => x.Id != id && x.IsActive && x.Role == UserRole.Administrator))
            throw new DomainException("Debe quedar al menos un administrador activo.");
        bool used = await db.Sales.AnyAsync(s => s.UserId == id || s.CancelledByUserId == id)
                    || await db.InventoryMovements.AnyAsync(m => m.UserId == id)
                    || await db.DailyClosings.AnyAsync(d => d.OpenedByUserId == id || d.ClosedByUserId == id)
                    || await db.CashMovements.AnyAsync(m => m.UserId == id)
                    || await db.Deliveries.AnyAsync(d => d.UserId == id)
                    || await db.StockingLists.AnyAsync(l => l.CreatedByUserId == id);
        if (used)
            throw new DomainException($"\"{u.Username}\" ya registró ventas o movimientos. Desactívalo, o usa Configuración → Borrar datos.");

        await using var tx = await db.Database.BeginTransactionAsync();
        // The audit trail keeps the user's name as text.
        await db.AuditLogs.Where(a => a.UserId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.UserId, (int?)null));
        await db.Users.Where(x => x.Id == id).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();   // drop stale tracked copies of rows deleted directly in the database
        audit.Add("Usuario eliminado", "User", id, u.Username);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Deletes a stocking list. Inventory already received from it stays (the delivery just loses the link).</summary>
    public async Task DeleteStockingListAsync(int id)
    {
        RequireManager();
        var l = await db.StockingLists.FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Lista no encontrada.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Deliveries.Where(d => d.StockingListId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.StockingListId, (int?)null));
        await db.StockingListItems.Where(i => i.StockingListId == id).ExecuteDeleteAsync();
        await db.StockingLists.Where(x => x.Id == id).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();   // drop stale tracked copies of rows deleted directly in the database
        audit.Add("Lista de surtido eliminada", "StockingList", id, l.Name);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DeleteCustomerRequestAsync(int id)
    {
        RequireManager();
        var r = await db.CustomerRequests.FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Solicitud no encontrada.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.StockingListItems.Where(i => i.CustomerRequestId == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.CustomerRequestId, (int?)null));
        await db.CustomerRequestEvents.Where(e => e.CustomerRequestId == id).ExecuteDeleteAsync();
        await db.CustomerRequests.Where(x => x.Id == id).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();   // drop stale tracked copies of rows deleted directly in the database
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    // ------------------------------------------------------------------ bulk resets (for test data)

    /// <summary>Everything that happened: sales, inventory, cash, deliveries, lists, requests, audit. Children first.</summary>
    private static readonly string[] OperationTables =
    {
        "SaleItemLots", "SaleItems", "InventoryMovements", "CashMovements", "Sales", "DailyClosingPayments", "DailyClosings",
        "InventoryLots", "DeliveryItems", "Deliveries", "StockingListItems", "StockingLists",
        "CustomerRequestEvents", "CustomerRequests", "AuditLogs"
    };

    /// <summary>The catalog and configuration, deleted after the operation tables.</summary>
    private static readonly string[] CatalogTables =
    {
        "SupplierSchedules", "Products", "Categories", "Owners", "Suppliers", "PaymentMethods", "CashRegisters", "Users", "AppSettings"
    };

    /// <summary>
    /// Clears all sales, stock, cash closings, deliveries, stocking lists, customer requests and the audit log,
    /// keeping products, owners, suppliers, categories, users and settings. Products end with zero stock.
    /// A backup is made first. Returns the backup file name.
    /// </summary>
    public async Task<string> ResetOperationsAsync()
    {
        RequireAdmin();
        var backup = await backups.CreateBackupAsync("antes_de_borrar");
        await ClearAsync(OperationTables);
        audit.Add("Datos de operación borrados", "Database", null, $"Ventas, inventario, cortes, entregas, listas y solicitudes. Respaldo previo: {backup.Name}");
        await db.SaveChangesAsync();
        return backup.Name;
    }

    /// <summary>Deletes absolutely everything (including users) and leaves a fresh database. A backup is made first.</summary>
    public async Task<string> ResetEverythingAsync()
    {
        RequireAdmin();
        var backup = await backups.CreateBackupAsync("antes_de_borrar");
        await ClearAsync(OperationTables.Concat(CatalogTables).ToArray());
        await DbSetup.InitializeAsync(db, clock.Now);   // re-create payment methods, register, default category, settings
        return backup.Name;
    }

    private async Task ClearAsync(string[] tables)
    {
        using (await gate.EnterAsync())
        {
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                foreach (var t in tables)
                {
                    var sql = "DELETE FROM \"" + t + "\";";   // fixed table names, never user input
                    await db.Database.ExecuteSqlRawAsync(sql);
                }
                // Restart numbering (folios, ids) at 1 for the cleared tables.
                try
                {
                    var names = string.Join(",", tables.Select(t => "'" + t + "'"));
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN (" + names + ");");
                }
                catch (SqliteException) { /* no AUTOINCREMENT tables yet */ }
                await tx.CommitAsync();
            }
            db.ChangeTracker.Clear();
            cache.Invalidate();
            try { await db.Database.ExecuteSqlRawAsync("VACUUM;"); }   // give the disk space back
            catch (SqliteException) { /* another device is reading right now; space is reused anyway */ }
        }
    }
}
