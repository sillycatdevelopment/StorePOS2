using StorePos.Core.Models;
using StorePos.Core.Reporting;

namespace StorePos.Core.ViewModels;

// ---------------------------------------------------------------- catalog

public class ProductRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Barcode { get; init; }
    public UnitType Unit { get; init; }
    public string? CategoryName { get; init; }
    public string? SupplierName { get; init; }
    public long PurchasePriceCents { get; init; }
    public long SalePriceCents { get; init; }
    public long MinStock { get; init; }
    public bool IsActive { get; init; }
    public long Stock { get; init; }
}

public class ProductInput
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public string? Barcode { get; set; }
    public int? CategoryId { get; set; }
    public string? Description { get; set; }
    public UnitType Unit { get; set; }
    public long PurchasePriceCents { get; set; }
    public long SalePriceCents { get; set; }
    public long MinStock { get; set; }
    public int? PreferredSupplierId { get; set; }
    public int? PriorityOwnerId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class OwnerStats
{
    public Owner Owner { get; init; } = null!;
    public int ProductsInStock { get; init; }
    public long InventoryCostCents { get; init; }
    public long InventoryRetailCents { get; init; }
    public long RevenueCents { get; init; }
    public long CostCents { get; init; }
    public int SaleLines { get; init; }
    /// <summary>Stock this owner sold to other owners (transfers, boxes bought to sell individually).</summary>
    public long TransferRevenueCents { get; init; }
    public long TransferCostCents { get; init; }
    public long ProfitCents => RevenueCents - CostCents + TransferRevenueCents - TransferCostCents;
}

public record UpcomingDelivery(DateOnly Date, int SupplierId, string SupplierName, bool ReceivedToday);

// ---------------------------------------------------------------- inventory

public enum StockStatus { Ok, Approaching, Low, Out }
public enum StockFilter { All, InStock, Out, Low, LowOrOut, Approaching }

public record OwnerStock(int OwnerId, string OwnerName, long Qty, long CostValueCents, DateTime OldestLot);
public record OwnerQty(string OwnerName, long Qty);

public class InventoryFilter
{
    public string? Text { get; set; }
    public int? CategoryId { get; set; }
    public int? OwnerId { get; set; }
    public int? SupplierId { get; set; }
    public StockFilter Status { get; set; }
}

public class InventoryRow
{
    public int ProductId { get; init; }
    public string Name { get; init; } = "";
    public string? Barcode { get; init; }
    public UnitType Unit { get; init; }
    public string? CategoryName { get; init; }
    public string? SupplierName { get; init; }
    public long Stock { get; init; }
    public long MinStock { get; init; }
    public long SalePriceCents { get; init; }
    public long PurchasePriceCents { get; init; }
    public long CostValueCents { get; init; }
    public long RetailValueCents { get; init; }
    public List<OwnerQty> Owners { get; init; } = new();
    public StockStatus Status { get; init; }
    public string StatusText => Status switch
    {
        StockStatus.Out => "Agotado",
        StockStatus.Low => "Bajo",
        StockStatus.Approaching => "Cerca del mínimo",
        _ => "OK"
    };
}

public class MovementRow
{
    public long Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public MovementType Type { get; init; }
    public long Quantity { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = "";
    public UnitType Unit { get; init; }
    public string OwnerName { get; init; } = "";
    public long UnitCostCents { get; init; }
    public long? UnitPriceCents { get; init; }
    public string? Reason { get; init; }
    public int? SaleId { get; init; }
    public int? DeliveryId { get; init; }
    public string? UserName { get; init; }
}

// ---------------------------------------------------------------- sales

public record PosOwnerStock(int OwnerId, string Name, long Qty);

public record PosProduct(int Id, string Name, string? Barcode, UnitType Unit, long PriceCents,
                         List<PosOwnerStock> Owners, bool NeedsOwnerChoice, long PackUnits = 0);

public class SaleLineRequest
{
    public int ProductId { get; set; }
    /// <summary>Base units (pieces or grams).</summary>
    public long Quantity { get; set; }
    /// <summary>Owner chosen by the cashier; null lets the allocation mode decide.</summary>
    public int? OwnerId { get; set; }
    public long DiscountCents { get; set; }
}

public class SaleRequest
{
    public List<SaleLineRequest> Lines { get; set; } = new();
    public int PaymentMethodId { get; set; }
    public long CashReceivedCents { get; set; }
    public long DiscountCents { get; set; }
    public string? Notes { get; set; }
    public bool AuthorizeNoStock { get; set; }
}

/// <summary>Quick product registration from the POS. Quantities in base units (pieces or grams).</summary>
public class QuickProductRequest
{
    public string Name { get; set; } = "";
    public string? Barcode { get; set; }
    public int? CategoryId { get; set; }
    public UnitType Unit { get; set; }
    public long SalePriceCents { get; set; }
    public long PurchasePriceCents { get; set; }
    public long MinStock { get; set; }
    public int? OwnerId { get; set; }
    /// <summary>Units on the shelf right now, including the ones being sold (0 = unknown).</summary>
    public long ShelfQty { get; set; }
}

public class SaleFilter
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public SaleStatus? Status { get; set; }
    public int? PaymentMethodId { get; set; }
    public int? OwnerId { get; set; }
    public int? SaleId { get; set; }
    public string? Product { get; set; }
}

public class SaleRow
{
    public int Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public string PaymentMethodName { get; init; } = "";
    public long TotalCents { get; init; }
    public long ProfitCents { get; init; }
    public SaleStatus Status { get; init; }
    public int ItemCount { get; init; }
    public string? UserName { get; init; }
}

// ---------------------------------------------------------------- cash

public record MethodTotal(int PaymentMethodId, string Name, bool IsCash, long AmountCents, int SaleCount);

public class CashSummary
{
    public DailyClosing Closing { get; set; } = null!;
    public List<CashMovement> Movements { get; set; } = new();
    public List<MethodTotal> ByMethod { get; set; } = new();
    public int SaleCount { get; set; }
    public int CancelledCount { get; set; }
    public long TotalSalesCents { get; set; }
    public long CashSalesCents { get; set; }
    public long NonCashSalesCents => TotalSalesCents - CashSalesCents;
    public long DepositsCents { get; set; }
    public long WithdrawalsCents { get; set; }
    public long ExpensesCents { get; set; }
    public long CashExpensesCents { get; set; }
    public long RefundsCents { get; set; }
    public long OtherInCents { get; set; }
    public long OtherOutCents { get; set; }
    public long GrossProfitCents { get; set; }
    public long ExpectedCashCents { get; set; }
}

// ---------------------------------------------------------------- purchasing

public class DeliveryLineInput
{
    public int ProductId { get; set; }
    public int OwnerId { get; set; }
    public long Quantity { get; set; }
    public long UnitCostCents { get; set; }
}

public class DeliveryInput
{
    public int SupplierId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Notes { get; set; }
    public int? StockingListId { get; set; }
    public bool UpdateReferenceCost { get; set; }
    public List<DeliveryLineInput> Items { get; set; } = new();
}

public class DeliveryRow
{
    public int Id { get; init; }
    public DateTime ReceivedAt { get; init; }
    public string SupplierName { get; init; } = "";
    public string? InvoiceNumber { get; init; }
    public int ItemCount { get; init; }
    public long TotalCostCents { get; init; }
    public string? UserName { get; init; }
}

public class ReorderSuggestion
{
    public int ProductId { get; init; }
    public string Name { get; init; } = "";
    public UnitType Unit { get; init; }
    public int? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public int? OwnerId { get; init; }
    public long CurrentStock { get; init; }
    public long MinStock { get; init; }
    /// <summary>Base units per day (pieces or grams), season-adjusted.</summary>
    public double AvgDailySales { get; init; }
    public double SeasonalFactor { get; init; }
    public bool Reliable { get; init; }
    public int DaysToNextDelivery { get; init; }
    public double? DaysOfCover { get; init; }
    public long SuggestedQty { get; init; }
    public long EstimatedUnitCostCents { get; init; }
    public int RequestCount { get; init; }
    public List<string> Reasons { get; init; } = new();
}

public class StockingItemEdit
{
    public int ItemId { get; set; }
    public long Quantity { get; set; }
    public long EstimatedUnitCostCents { get; set; }
    public int? OwnerId { get; set; }
    public int? SupplierId { get; set; }
    public bool Remove { get; set; }
}

public class ReceiveLine
{
    public int ItemId { get; set; }
    public long Quantity { get; set; }
    public long UnitCostCents { get; set; }
    public int? OwnerId { get; set; }
    public int? SupplierId { get; set; }
}

// ---------------------------------------------------------------- analytics

public class ProductDemand
{
    public int ProductId { get; init; }
    public string Name { get; init; } = "";
    public UnitType Unit { get; init; }
    public int DaysOfHistory { get; init; }
    public long UnitsLast7 { get; init; }
    public long UnitsLastWindow { get; init; }
    public long UnitsPreviousWindow { get; init; }
    /// <summary>Base units per day over the recent window.</summary>
    public double DailyAverage { get; init; }
    public double AdjustedDailyAverage { get; init; }
    public double SeasonalFactor { get; init; } = 1;
    public bool Reliable { get; init; }
    public TrendDirection Trend { get; init; }
    public double? ChangeRatio { get; init; }
    public bool SeasonalReliable { get; init; }
    public double[] SeasonalIndex { get; init; } = Enumerable.Repeat(1.0, 12).ToArray();
    public DateOnly? LastSale { get; init; }
}

public record ProductTrendRow(ProductDemand Demand, long Stock, StockAdvice Advice);

public record PeriodSummary(DateOnly From, DateOnly To, long RevenueCents, long CostCents, long DiscountCents, int SaleCount, int CancelledCount)
{
    public long ProfitCents => RevenueCents - CostCents;
    public long AverageTicketCents => SaleCount == 0 ? 0 : RevenueCents / SaleCount;
}

public record GroupTotal(string Label, long RevenueCents, long CostCents, int SaleCount, long Units)
{
    public DateOnly? Date { get; init; }
    public int? Id { get; init; }
    public long ProfitCents => RevenueCents - CostCents;
}

public record ProductSales(int ProductId, string Name, UnitType Unit, long Units, long RevenueCents, long CostCents)
{
    public long ProfitCents => RevenueCents - CostCents;
}
