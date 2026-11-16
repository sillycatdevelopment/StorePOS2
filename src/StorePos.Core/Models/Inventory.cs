namespace StorePos.Core.Models;

/// <summary>
/// A batch of one product owned by one owner, bought at one unit cost.
/// Stock of an owner = sum of QtyRemaining over their lots. Consumed FIFO.
/// </summary>
public class InventoryLot
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int OwnerId { get; set; }
    public Owner? Owner { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int? DeliveryItemId { get; set; }
    public DeliveryItem? DeliveryItem { get; set; }
    /// <summary>Cost per piece / per kg.</summary>
    public long UnitCostCents { get; set; }
    public long QtyReceived { get; set; }
    public long QtyRemaining { get; set; }
    public DateTime ReceivedAt { get; set; }
    public MovementType Source { get; set; }
}

/// <summary>Immutable journal of every stock change. Quantity is signed (+ in, − out).</summary>
public class InventoryMovement
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int OwnerId { get; set; }
    public Owner? Owner { get; set; }
    public int? LotId { get; set; }
    public InventoryLot? Lot { get; set; }
    public MovementType Type { get; set; }
    public long Quantity { get; set; }
    public long UnitCostCents { get; set; }
    public long? UnitPriceCents { get; set; }
    public string? Reason { get; set; }
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int? DeliveryId { get; set; }
    public Delivery? Delivery { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Links a product sold individually (e.g. "Cigarro Marlboro suelto") to the box/pack it comes from
/// (e.g. "Cajetilla Marlboro 20"). When the individual pieces run out, a box can be opened (automatically
/// in the POS, or by hand): the box leaves inventory and <see cref="UnitsPerPack"/> pieces enter.
/// If the pieces belong to a different owner than the box, that owner "buys" the box at its sale price.
/// </summary>
public class PackLink
{
    public int Id { get; set; }
    /// <summary>The product sold individually. A product can come from only one box.</summary>
    public int UnitProductId { get; set; }
    public Product? UnitProduct { get; set; }
    /// <summary>The box/pack product.</summary>
    public int PackProductId { get; set; }
    public Product? PackProduct { get; set; }
    public int UnitsPerPack { get; set; }
    /// <summary>Who owns (sells) the individual pieces. Null = the same owner as the box that is opened.</summary>
    public int? UnitOwnerId { get; set; }
    public Owner? UnitOwner { get; set; }
    /// <summary>Open a box automatically when a sale needs more pieces than there are.</summary>
    public bool AutoOpen { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
