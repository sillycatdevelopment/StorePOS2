namespace StorePos.Core.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Owner
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Contact { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<SupplierSchedule> Schedules { get; set; } = new();
}

/// <summary>One row per weekday the supplier normally visits.</summary>
public class SupplierSchedule
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public DayOfWeek Day { get; set; }
}

/// <summary>
/// A product is only the catalog entry. Who owns how many units (and at what cost)
/// lives in <see cref="InventoryLot"/>; a product never stores a stock number.
/// </summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Lowercase, accent-free copy of Name for fast LIKE searches.</summary>
    public string SearchName { get; set; } = "";
    public string? Barcode { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public string? Description { get; set; }
    public UnitType Unit { get; set; } = UnitType.Piece;
    /// <summary>Reference purchase cost per piece/kg; default cost for new stock.</summary>
    public long PurchasePriceCents { get; set; }
    /// <summary>Selling price per piece/kg.</summary>
    public long SalePriceCents { get; set; }
    /// <summary>Minimum stock, in base units (pieces or grams).</summary>
    public long MinStock { get; set; }
    public int? PreferredSupplierId { get; set; }
    public Supplier? PreferredSupplier { get; set; }
    /// <summary>Owner whose stock sells first in Automatic allocation mode.</summary>
    public int? PriorityOwnerId { get; set; }
    public Owner? PriorityOwner { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
