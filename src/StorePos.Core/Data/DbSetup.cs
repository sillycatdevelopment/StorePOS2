using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StorePos.Core.Models;
using StorePos.Core.Services;

namespace StorePos.Core.Data;

/// <summary>Per-connection SQLite tuning: safe and fast on an HDD.</summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string Sql = "PRAGMA foreign_keys=ON; PRAGMA synchronous=NORMAL; PRAGMA temp_store=MEMORY; PRAGMA cache_size=-8000;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = Sql;
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public static class DbSetup
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlite(connectionString, o => o.CommandTimeout(30))
               .AddInterceptors(new SqlitePragmaInterceptor());

    /// <summary>
    /// Creates/upgrades the schema and seeds required reference rows (idempotent).
    /// Uses EF migrations when the project contains them (recommended: run setup.ps1 once);
    /// otherwise falls back to EnsureCreated so the app still starts.
    /// </summary>
    public static async Task InitializeAsync(StoreDbContext db, DateTime now)
    {
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (db.Database.IsSqlite())
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

        if (!await db.PaymentMethods.AnyAsync())
        {
            db.PaymentMethods.AddRange(
                new PaymentMethod { Name = "Efectivo", IsCash = true, SortOrder = 1 },
                new PaymentMethod { Name = "Tarjeta", IsCash = false, SortOrder = 2 },
                new PaymentMethod { Name = "Transferencia", IsCash = false, SortOrder = 3 },
                new PaymentMethod { Name = "Otro", IsCash = false, SortOrder = 4 });
        }

        if (!await db.CashRegisters.AnyAsync())
            db.CashRegisters.Add(new CashRegister { Name = "Caja principal" });

        if (!await db.Categories.AnyAsync())
            db.Categories.Add(new Category { Name = "General" });

        foreach (var (key, value) in SettingsService.Defaults)
            if (!await db.AppSettings.AnyAsync(s => s.Key == key))
                db.AppSettings.Add(new AppSetting { Key = key, Value = value });

        await db.SaveChangesAsync();
    }
}

/// <summary>Used only by `dotnet ef migrations add` at design time.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<StoreDbContext>
{
    public StoreDbContext CreateDbContext(string[] args)
    {
        var b = new DbContextOptionsBuilder<StoreDbContext>();
        b.UseSqlite("Data Source=design.db");
        return new StoreDbContext(b.Options);
    }
}
