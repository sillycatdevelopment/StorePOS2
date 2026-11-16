namespace StorePos.Core.Models;

public class PaymentMethod
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>True only for physical cash. Card/transfer sales never count as drawer cash.</summary>
    public bool IsCash { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class Sale
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateOnly BusinessDate { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public int DailyClosingId { get; set; }
    public DailyClosing? DailyClosing { get; set; }
    public int PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string PaymentMethodName { get; set; } = "";
    public bool PaymentIsCash { get; set; }
    public long SubtotalCents { get; set; }
    public long DiscountCents { get; set; }
    public long TotalCents { get; set; }
    public long CostCents { get; set; }
    public long ProfitCents { get; set; }
    public long CashReceivedCents { get; set; }
    public long ChangeCents { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public DateTime? CancelledAt { get; set; }
    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancelReason { get; set; }
    public string? Notes { get; set; }
    public List<SaleItem> Items { get; set; } = new();
}

/// <summary>
/// One product sold from one owner's stock. All descriptive fields are snapshots so the
/// history stays correct even if the product, owner or category is later edited.
/// </summary>
public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int OwnerId { get; set; }
    public Owner? Owner { get; set; }
    public string ProductName { get; set; } = "";
    public string? Barcode { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int? SupplierId { get; set; }
    public string OwnerName { get; set; } = "";
    public UnitType Unit { get; set; }
    public long Quantity { get; set; }
    public long UnitPriceCents { get; set; }
    public long DiscountCents { get; set; }
    public long LineTotalCents { get; set; }
    /// <summary>Weighted cost per piece/kg of the lots actually consumed.</summary>
    public long UnitCostCents { get; set; }
    public long CostCents { get; set; }
    public DateOnly BusinessDate { get; set; }
    /// <summary>yyyyMM, e.g. 202611. Denormalized for fast monthly/seasonal queries.</summary>
    public int YearMonth { get; set; }
    public List<SaleItemLot> Lots { get; set; } = new();
}

/// <summary>Exactly which lots a sale line consumed, so cost is exact and cancellations are reversible.</summary>
public class SaleItemLot
{
    public int Id { get; set; }
    public int SaleItemId { get; set; }
    public SaleItem? SaleItem { get; set; }
    public int LotId { get; set; }
    public InventoryLot? Lot { get; set; }
    public long Quantity { get; set; }
    public long UnitCostCents { get; set; }
}
