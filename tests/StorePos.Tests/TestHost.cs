using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StorePos.Core;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;

namespace StorePos.Tests;

public class FakeClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 11, 16, 10, 0, 0);
    public DateOnly Today => DateOnly.FromDateTime(Now);
}

public class FakeUser : ICurrentUser
{
    public int? UserId { get; set; }
    public string UserName { get; set; } = "prueba";
    public UserRole Role { get; set; } = UserRole.Administrator;
    public bool IsAuthenticated => true;
}

/// <summary>A real SQLite database in memory: same SQL, constraints and transactions as production.</summary>
public sealed class TestHost : IAsyncDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ServiceProvider _root;
    private IServiceScope _scope;
    public FakeClock Clock { get; } = new();
    public FakeUser User { get; } = new();

    private TestHost()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddStorePosCore(o => o.UseSqlite(_conn).AddInterceptors(new SqlitePragmaInterceptor()));
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<ICurrentUser>(User);
        _root = services.BuildServiceProvider();
        _scope = _root.CreateScope();
    }

    public static async Task<TestHost> CreateAsync()
    {
        var h = new TestHost();
        await DbSetup.InitializeAsync(h.Db, h.Clock.Now);
        return h;
    }

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();
    public StoreDbContext Db => Get<StoreDbContext>();

    /// <summary>Fresh scope = fresh DbContext, like a new HTTP request (no stale tracked entities).</summary>
    public void NewScope() { _scope.Dispose(); _scope = _root.CreateScope(); }

    public async Task<int> OwnerAsync(string name) => await Get<OwnerService>().SaveAsync(null, name, null, null, true);

    public async Task<int> ProductAsync(string name, decimal salePrice, decimal cost = 0, string? barcode = null,
                                        UnitType unit = UnitType.Piece, long minStock = 0, int? supplierId = null, int? priorityOwner = null) =>
        await Get<ProductService>().SaveAsync(new StorePos.Core.ViewModels.ProductInput
        {
            Name = name, Barcode = barcode, Unit = unit, SalePriceCents = Money.ToCents(salePrice),
            PurchasePriceCents = Money.ToCents(cost), MinStock = minStock, PreferredSupplierId = supplierId, PriorityOwnerId = priorityOwner
        });

    public Task StockAsync(int productId, int ownerId, long qty, decimal unitCost) =>
        Get<InventoryService>().AddStockAsync(productId, ownerId, qty, Money.ToCents(unitCost), MovementType.Restock, null);

    public async Task<int> OpenCashAsync(decimal opening = 0) => await Get<CashService>().OpenAsync(Money.ToCents(opening));

    public int MethodId(string name) => Db.PaymentMethods.Single(m => m.Name == name).Id;

    public async ValueTask DisposeAsync()
    {
        _scope.Dispose();
        await _root.DisposeAsync();
        await _conn.DisposeAsync();
    }
}
