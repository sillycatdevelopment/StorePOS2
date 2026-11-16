namespace StorePos.Core.Models;

/// <summary>How a product is measured. Quantities are stored in base units: pieces or grams.</summary>
public enum UnitType { Piece = 0, Kilogram = 1 }

public enum MovementType
{
    InitialStock = 0,
    Restock = 1,
    Sale = 2,
    SaleCancellation = 3,
    Return = 4,
    Damaged = 5,
    Correction = 6,
    TransferOut = 7,
    TransferIn = 8
}

public enum AllocationMode { Manual = 0, OldestFirst = 1, Automatic = 2 }

public enum SaleStatus { Completed = 0, Cancelled = 1 }

public enum ClosingStatus { Open = 0, Closed = 1 }

public enum CashMovementType
{
    Deposit = 0,     // +
    Withdrawal = 1,  // −
    Expense = 2,     // − (only when AffectsCash)
    Refund = 3,      // −
    OtherIn = 4,     // +
    OtherOut = 5     // −
}

public enum StockingListStatus { Draft = 0, Purchased = 1, Received = 2, Cancelled = 3 }

public enum StockingGroupBy { None = 0, Supplier = 1, Owner = 2 }

public enum RequestStatus { Pending = 0, Ordered = 1, Stocked = 2, NotPurchasing = 3 }

public enum UserRole { Cashier = 0, Manager = 1, Administrator = 2 }
