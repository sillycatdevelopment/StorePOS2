using Microsoft.Extensions.DependencyInjection;
using StorePos.Core.Data;
using StorePos.Core.Reporting;
using StorePos.Core.Services;
using StorePos.Core.Utilities;

namespace StorePos.Core;

public static class ServiceRegistration
{
    /// <summary>Registers the database and all business services. The host must register an <see cref="ICurrentUser"/>.</summary>
    public static IServiceCollection AddStorePosCore(this IServiceCollection services, string connectionString) =>
        services.AddStorePosCore(o => DbSetup.Configure(o, connectionString));

    /// <summary>Overload used by tests (e.g. an in-memory SQLite connection).</summary>
    public static IServiceCollection AddStorePosCore(this IServiceCollection services, Action<Microsoft.EntityFrameworkCore.DbContextOptionsBuilder> configureDb)
    {
        services.AddDbContext<StoreDbContext>(configureDb);
        services.AddSingleton<IClock, StoreClock>();
        services.AddSingleton<WriteGate>();
        services.AddSingleton<SettingsCache>();

        services.AddScoped<AuditService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<ProductService>();
        services.AddScoped<OwnerService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<CashService>();
        services.AddScoped<SalesService>();
        services.AddScoped<DeliveryService>();
        services.AddScoped<CustomerRequestService>();
        services.AddScoped<StockingService>();
        services.AddScoped<TrendService>();
        services.AddScoped<ReportService>();
        services.AddScoped<ExcelExportService>();
        services.AddScoped<UserService>();
        services.AddScoped<BackupService>();
        services.AddScoped<DeletionService>();
        return services;
    }
}
