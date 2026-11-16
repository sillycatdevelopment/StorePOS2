using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;

namespace StorePos.Core.Services;

/// <summary>Cash day: opening amount, cash movements, live summary and the frozen daily closing.</summary>
public class CashService(StoreDbContext db, AuditService audit, ICurrentUser user, IClock clock, WriteGate gate)
{
    public async Task<int> DefaultRegisterIdAsync() =>
        await db.CashRegisters.Where(r => r.IsActive).OrderBy(r => r.Id).Select(r => r.Id).FirstOrDefaultAsync() is int id and > 0
            ? id : throw new DomainException("No hay cajas registradas.");

    public async Task<DailyClosing?> GetOpenSessionAsync(int? registerId = null)
    {
        int reg = registerId ?? await DefaultRegisterIdAsync();
        return await db.DailyClosings.AsNoTracking().Include(d => d.OpenedByUser)
                       .FirstOrDefaultAsync(d => d.CashRegisterId == reg && d.Status == ClosingStatus.Open);
    }

    public async Task<int> OpenAsync(long openingCashCents, int? registerId = null)
    {
        if (openingCashCents < 0) throw new DomainException("El fondo inicial no puede ser negativo.");
        using var _ = await gate.EnterAsync();
        int reg = registerId ?? await DefaultRegisterIdAsync();
        if (await db.DailyClosings.AnyAsync(d => d.CashRegisterId == reg && d.Status == ClosingStatus.Open))
            throw new DomainException("La caja ya está abierta.");
        var now = clock.Now;
        var day = new DailyClosing
        {
            CashRegisterId = reg, BusinessDate = DateOnly.FromDateTime(now), OpenedAt = now,
            OpenedByUserId = user.UserId, OpeningCashCents = openingCashCents
        };
        db.DailyClosings.Add(day);
        await db.SaveChangesAsync();
        audit.Add("Apertura de caja", "DailyClosing", day.Id, $"Fondo inicial {Money.Format(openingCashCents)}");
        await db.SaveChangesAsync();
        return day.Id;
    }

    public async Task AddMovementAsync(CashMovementType type, long amountCents, bool affectsCash, string description)
    {
        if (amountCents <= 0) throw new DomainException("El monto debe ser mayor a cero.");
        var desc = TextNorm.Clean(description) ?? throw new DomainException("Escribe una descripción del movimiento.");
        if (type is CashMovementType.Withdrawal or CashMovementType.OtherOut && !user.Role.AtLeast(UserRole.Manager))
            throw new DomainException("Solo un encargado o administrador puede retirar efectivo de la caja.");
        // Only expenses and refunds can be paid by something other than drawer cash.
        if (type is not (CashMovementType.Expense or CashMovementType.Refund)) affectsCash = true;

        using var _ = await gate.EnterAsync();
        var session = await GetOpenSessionAsync() ?? throw new DomainException("La caja está cerrada.");
        db.CashMovements.Add(new CashMovement
        {
            DailyClosingId = session.Id, Type = type, AmountCents = amountCents, AffectsCash = affectsCash,
            Description = desc, UserId = user.UserId, CreatedAt = clock.Now
        });
        audit.Add("Movimiento de caja", "DailyClosing", session.Id,
                  $"{DisplayNames.Of(type)} {Money.Format(amountCents)}{(affectsCash ? "" : " (no efectivo)")}: {desc}");
        await db.SaveChangesAsync();
    }

    /// <summary>Live totals for an open day, or the frozen values for a closed one.</summary>
    public async Task<CashSummary> GetSummaryAsync(int closingId)
    {
        var day = await db.DailyClosings.AsNoTracking()
            .Include(d => d.OpenedByUser).Include(d => d.ClosedByUser).Include(d => d.CashRegister)
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == closingId) ?? throw new DomainException("Corte no encontrado.");

        var movements = await db.CashMovements.AsNoTracking().Include(m => m.User)
            .Where(m => m.DailyClosingId == closingId).OrderBy(m => m.Id).ToListAsync();

        var summary = new CashSummary { Closing = day, Movements = movements };

        if (day.Status == ClosingStatus.Closed)
        {
            summary.ByMethod = day.Payments.OrderByDescending(p => p.IsCash).ThenBy(p => p.MethodName)
                .Select(p => new MethodTotal(p.PaymentMethodId, p.MethodName, p.IsCash, p.AmountCents, p.SaleCount)).ToList();
            summary.SaleCount = day.SaleCount;
            summary.TotalSalesCents = day.TotalSalesCents;
            summary.CashSalesCents = day.CashSalesCents;
            summary.DepositsCents = day.DepositsCents;
            summary.WithdrawalsCents = day.WithdrawalsCents;
            summary.ExpensesCents = day.ExpensesCents;
            summary.CashExpensesCents = day.CashExpensesCents;
            summary.RefundsCents = day.RefundsCents;
            summary.OtherInCents = day.OtherInCents;
            summary.OtherOutCents = day.OtherOutCents;
            summary.GrossProfitCents = day.GrossProfitCents;
            summary.ExpectedCashCents = day.ExpectedCashCents;
            summary.CancelledCount = await db.Sales.CountAsync(s => s.DailyClosingId == closingId && s.Status == SaleStatus.Cancelled);
            return summary;
        }

        var byMethod = await db.Sales.AsNoTracking()
            .Where(s => s.DailyClosingId == closingId && s.Status == SaleStatus.Completed)
            .GroupBy(s => new { s.PaymentMethodId, s.PaymentMethodName, s.PaymentIsCash })
            .Select(g => new { g.Key.PaymentMethodId, g.Key.PaymentMethodName, g.Key.PaymentIsCash,
                               Total = g.Sum(s => s.TotalCents), Profit = g.Sum(s => s.ProfitCents), Count = g.Count() })
            .ToListAsync();

        summary.ByMethod = byMethod.OrderByDescending(m => m.PaymentIsCash).ThenBy(m => m.PaymentMethodName)
            .Select(m => new MethodTotal(m.PaymentMethodId, m.PaymentMethodName, m.PaymentIsCash, m.Total, m.Count)).ToList();
        summary.SaleCount = byMethod.Sum(m => m.Count);
        summary.TotalSalesCents = byMethod.Sum(m => m.Total);
        summary.CashSalesCents = byMethod.Where(m => m.PaymentIsCash).Sum(m => m.Total);
        summary.GrossProfitCents = byMethod.Sum(m => m.Profit);
        summary.CancelledCount = await db.Sales.CountAsync(s => s.DailyClosingId == closingId && s.Status == SaleStatus.Cancelled);

        long SumOf(CashMovementType t, bool cashOnly = true) =>
            movements.Where(m => m.Type == t && (!cashOnly || m.AffectsCash)).Sum(m => m.AmountCents);

        summary.DepositsCents = SumOf(CashMovementType.Deposit);
        summary.WithdrawalsCents = SumOf(CashMovementType.Withdrawal);
        summary.ExpensesCents = SumOf(CashMovementType.Expense, cashOnly: false);
        summary.CashExpensesCents = SumOf(CashMovementType.Expense);
        summary.RefundsCents = SumOf(CashMovementType.Refund);
        summary.OtherInCents = SumOf(CashMovementType.OtherIn);
        summary.OtherOutCents = SumOf(CashMovementType.OtherOut);
        summary.ExpectedCashCents = ExpectedCash(day.OpeningCashCents, summary.CashSalesCents, summary.DepositsCents,
            summary.OtherInCents, summary.WithdrawalsCents, summary.CashExpensesCents, summary.RefundsCents, summary.OtherOutCents);
        return summary;
    }

    /// <summary>Opening + cash sales + deposits + other-in − withdrawals − cash expenses − cash refunds − other-out.</summary>
    public static long ExpectedCash(long opening, long cashSales, long deposits, long otherIn,
                                    long withdrawals, long cashExpenses, long refunds, long otherOut) =>
        opening + cashSales + deposits + otherIn - withdrawals - cashExpenses - refunds - otherOut;

    public async Task CloseAsync(int closingId, long countedCashCents, string? notes)
    {
        if (countedCashCents < 0) throw new DomainException("El efectivo contado no puede ser negativo.");
        using var _ = await gate.EnterAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var summary = await GetSummaryAsync(closingId);
        if (summary.Closing.Status == ClosingStatus.Closed) throw new DomainException("Este día ya fue cerrado.");
        long diff = countedCashCents - summary.ExpectedCashCents;
        var cleanNotes = TextNorm.Clean(notes);
        if (diff != 0 && cleanNotes is null)
            throw new DomainException($"Hay una diferencia de {Money.Format(diff)}. Escribe una explicación para cerrar.");

        var day = await db.DailyClosings.FirstAsync(d => d.Id == closingId);
        day.Status = ClosingStatus.Closed;
        day.ClosedAt = clock.Now;
        day.ClosedByUserId = user.UserId;
        day.SaleCount = summary.SaleCount;
        day.TotalSalesCents = summary.TotalSalesCents;
        day.CashSalesCents = summary.CashSalesCents;
        day.NonCashSalesCents = summary.TotalSalesCents - summary.CashSalesCents;
        day.DepositsCents = summary.DepositsCents;
        day.WithdrawalsCents = summary.WithdrawalsCents;
        day.ExpensesCents = summary.ExpensesCents;
        day.CashExpensesCents = summary.CashExpensesCents;
        day.RefundsCents = summary.RefundsCents;
        day.OtherInCents = summary.OtherInCents;
        day.OtherOutCents = summary.OtherOutCents;
        day.GrossProfitCents = summary.GrossProfitCents;
        day.ExpectedCashCents = summary.ExpectedCashCents;
        day.CountedCashCents = countedCashCents;
        day.DifferenceCents = diff;
        day.Notes = cleanNotes;
        foreach (var m in summary.ByMethod)
            db.DailyClosingPayments.Add(new DailyClosingPayment
            {
                DailyClosingId = day.Id, PaymentMethodId = m.PaymentMethodId, MethodName = m.Name,
                IsCash = m.IsCash, AmountCents = m.AmountCents, SaleCount = m.SaleCount
            });

        audit.Add("Corte de caja", "DailyClosing", day.Id,
            $"Esperado {Money.Format(day.ExpectedCashCents)}, contado {Money.Format(countedCashCents)}, diferencia {Money.Format(diff)}" +
            (cleanNotes is null ? "" : $". Nota: {cleanNotes}"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public Task<PagedResult<DailyClosing>> GetHistoryAsync(DateOnly? from, DateOnly? to, int page, int pageSize = 50)
    {
        var q = db.DailyClosings.AsNoTracking().Include(d => d.OpenedByUser).Include(d => d.ClosedByUser).AsQueryable();
        if (from is { } f) q = q.Where(d => d.BusinessDate >= f);
        if (to is { } t) q = q.Where(d => d.BusinessDate <= t);
        return PagedResult<DailyClosing>.CreateAsync(q.OrderByDescending(d => d.Id), page, pageSize);
    }
}
