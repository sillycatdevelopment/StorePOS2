namespace StorePos.Core.Models;

public class CashRegister
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A cash day (session) of one register: opened with a starting amount, closed with a count.
/// While Open the totals are computed live; on close they are frozen in these columns.
/// </summary>
public class DailyClosing
{
    public int Id { get; set; }
    public int CashRegisterId { get; set; }
    public CashRegister? CashRegister { get; set; }
    public DateOnly BusinessDate { get; set; }
    public ClosingStatus Status { get; set; } = ClosingStatus.Open;
    public DateTime OpenedAt { get; set; }
    public int? OpenedByUserId { get; set; }
    public User? OpenedByUser { get; set; }
    public long OpeningCashCents { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }
    public User? ClosedByUser { get; set; }

    // Frozen at close
    public int SaleCount { get; set; }
    public long TotalSalesCents { get; set; }
    public long CashSalesCents { get; set; }
    public long NonCashSalesCents { get; set; }
    public long DepositsCents { get; set; }
    public long WithdrawalsCents { get; set; }
    public long ExpensesCents { get; set; }
    public long CashExpensesCents { get; set; }
    public long RefundsCents { get; set; }
    public long OtherInCents { get; set; }
    public long OtherOutCents { get; set; }
    public long GrossProfitCents { get; set; }
    public long ExpectedCashCents { get; set; }
    public long CountedCashCents { get; set; }
    public long DifferenceCents { get; set; }
    public string? Notes { get; set; }

    public List<CashMovement> Movements { get; set; } = new();
    public List<DailyClosingPayment> Payments { get; set; } = new();
}

/// <summary>Sales per payment method, frozen at close.</summary>
public class DailyClosingPayment
{
    public int Id { get; set; }
    public int DailyClosingId { get; set; }
    public DailyClosing? DailyClosing { get; set; }
    public int PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string MethodName { get; set; } = "";
    public bool IsCash { get; set; }
    public long AmountCents { get; set; }
    public int SaleCount { get; set; }
}

public class CashMovement
{
    public int Id { get; set; }
    public int DailyClosingId { get; set; }
    public DailyClosing? DailyClosing { get; set; }
    public CashMovementType Type { get; set; }
    /// <summary>Always positive; the type gives the sign.</summary>
    public long AmountCents { get; set; }
    /// <summary>False for e.g. an expense paid by card/transfer: counts as expense, not drawer cash.</summary>
    public bool AffectsCash { get; set; } = true;
    public string Description { get; set; } = "";
    public int? SaleId { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; }
}
