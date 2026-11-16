using Microsoft.EntityFrameworkCore;
using StorePos.Core.Models;

namespace StorePos.Core.Data;

public class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierSchedule> SupplierSchedules => Set<SupplierSchedule>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<PackLink> PackLinks => Set<PackLink>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SaleItemLot> SaleItemLots => Set<SaleItemLot>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<DailyClosing> DailyClosings => Set<DailyClosing>();
    public DbSet<DailyClosingPayment> DailyClosingPayments => Set<DailyClosingPayment>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryItem> DeliveryItems => Set<DeliveryItem>();
    public DbSet<StockingList> StockingLists => Set<StockingList>();
    public DbSet<StockingListItem> StockingListItems => Set<StockingListItem>();
    public DbSet<CustomerRequest> CustomerRequests => Set<CustomerRequest>();
    public DbSet<CustomerRequestEvent> CustomerRequestEvents => Set<CustomerRequestEvent>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Owner>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Contact).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(1000);
        });

        b.Entity<Supplier>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Contact).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasMany(x => x.Schedules).WithOne(x => x.Supplier!).HasForeignKey(x => x.SupplierId);
        });

        b.Entity<SupplierSchedule>(e => e.HasIndex(x => new { x.SupplierId, x.Day }).IsUnique());

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.SearchName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Barcode).HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasIndex(x => x.Barcode).IsUnique();          // SQLite allows many NULLs
            e.HasIndex(x => x.SearchName);
            e.HasIndex(x => new { x.IsActive, x.CategoryId });
            e.HasIndex(x => x.PreferredSupplierId);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Product_Prices", "PurchasePriceCents >= 0 AND SalePriceCents >= 0");
                t.HasCheckConstraint("CK_Product_MinStock", "MinStock >= 0");
            });
        });

        b.Entity<InventoryLot>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.OwnerId, x.QtyRemaining });
            e.HasIndex(x => new { x.ProductId, x.ReceivedAt });
            e.ToTable(t => t.HasCheckConstraint("CK_Lot_Qty", "QtyRemaining >= 0 AND QtyRemaining <= QtyReceived AND UnitCostCents >= 0"));
        });

        b.Entity<InventoryMovement>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => new { x.ProductId, x.CreatedAt });
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.SaleId);
        });

        b.Entity<PackLink>(e =>
        {
            e.HasOne(x => x.UnitProduct).WithMany().HasForeignKey(x => x.UnitProductId);
            e.HasOne(x => x.PackProduct).WithMany().HasForeignKey(x => x.PackProductId);
            e.HasOne(x => x.UnitOwner).WithMany().HasForeignKey(x => x.UnitOwnerId);
            e.HasIndex(x => x.UnitProductId).IsUnique();
            e.HasIndex(x => x.PackProductId);
            e.ToTable(t => t.HasCheckConstraint("CK_PackLink", "UnitsPerPack >= 2 AND UnitProductId <> PackProductId"));
        });

        b.Entity<PaymentMethod>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Sale>(e =>
        {
            e.Property(x => x.PaymentMethodName).HasMaxLength(60);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => x.BusinessDate);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.DailyClosingId, x.Status });
            e.HasMany(x => x.Items).WithOne(x => x.Sale!).HasForeignKey(x => x.SaleId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasOne(x => x.CancelledByUser).WithMany().HasForeignKey(x => x.CancelledByUserId);
            e.ToTable(t => t.HasCheckConstraint("CK_Sale_Total", "TotalCents >= 0 AND DiscountCents >= 0"));
        });

        b.Entity<SaleItem>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(150);
            e.Property(x => x.OwnerName).HasMaxLength(100);
            e.Property(x => x.CategoryName).HasMaxLength(80);
            e.Property(x => x.Barcode).HasMaxLength(64);
            e.HasIndex(x => new { x.BusinessDate, x.ProductId });
            e.HasIndex(x => new { x.ProductId, x.YearMonth });
            e.HasIndex(x => new { x.OwnerId, x.BusinessDate });
            e.HasMany(x => x.Lots).WithOne(x => x.SaleItem!).HasForeignKey(x => x.SaleItemId);
            e.ToTable(t => t.HasCheckConstraint("CK_SaleItem_Qty", "Quantity > 0"));
        });

        b.Entity<CashRegister>(e => e.Property(x => x.Name).HasMaxLength(60));

        b.Entity<DailyClosing>(e =>
        {
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.CashRegisterId, x.Status });
            e.HasIndex(x => x.BusinessDate);
            e.HasOne(x => x.OpenedByUser).WithMany().HasForeignKey(x => x.OpenedByUserId);
            e.HasOne(x => x.ClosedByUser).WithMany().HasForeignKey(x => x.ClosedByUserId);
            e.HasMany(x => x.Movements).WithOne(x => x.DailyClosing!).HasForeignKey(x => x.DailyClosingId);
            e.HasMany(x => x.Payments).WithOne(x => x.DailyClosing!).HasForeignKey(x => x.DailyClosingId);
        });

        b.Entity<CashMovement>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(300);
            e.ToTable(t => t.HasCheckConstraint("CK_CashMovement_Amount", "AmountCents > 0"));
        });

        b.Entity<Delivery>(e =>
        {
            e.Property(x => x.InvoiceNumber).HasMaxLength(60);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.SupplierId, x.ReceivedAt });
            e.HasIndex(x => x.ReceivedAt);
            e.HasMany(x => x.Items).WithOne(x => x.Delivery!).HasForeignKey(x => x.DeliveryId);
        });

        b.Entity<DeliveryItem>(e => e.ToTable(t => t.HasCheckConstraint("CK_DeliveryItem_Qty", "Quantity > 0")));

        b.Entity<StockingList>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => x.Status);
            e.HasMany(x => x.Items).WithOne(x => x.StockingList!).HasForeignKey(x => x.StockingListId);
        });

        b.Entity<StockingListItem>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(150);
            e.Property(x => x.Reason).HasMaxLength(300);
        });

        b.Entity<CustomerRequest>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(150).IsRequired();
            e.Property(x => x.NormalizedName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Barcode).HasMaxLength(64);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.NormalizedName, x.Status });
            e.HasIndex(x => x.Barcode);
            e.HasMany(x => x.Events).WithOne(x => x.CustomerRequest!).HasForeignKey(x => x.CustomerRequestId);
        });

        b.Entity<CustomerRequestEvent>(e => e.Property(x => x.Notes).HasMaxLength(500));

        b.Entity<User>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(50).IsRequired().UseCollation("NOCASE");
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.HasIndex(x => x.Username).IsUnique();
        });

        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(100);
            e.Property(x => x.Action).HasMaxLength(80);
            e.Property(x => x.EntityType).HasMaxLength(60);
            e.Property(x => x.EntityId).HasMaxLength(40);
            e.Property(x => x.Details).HasMaxLength(2000);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        b.Entity<AppSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(80);
            e.Property(x => x.Value).HasMaxLength(1000);
        });

        // Financial history must never disappear through a cascade.
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
