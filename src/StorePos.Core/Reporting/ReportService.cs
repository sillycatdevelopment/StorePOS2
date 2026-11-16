using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;

namespace StorePos.Core.Reporting;

public enum PeriodPreset { Today, Yesterday, ThisWeek, ThisMonth, LastMonth, ThisYear, Custom }

public static class Periods
{
    /// <summary>Resolves a preset to an inclusive date range. Weeks start on Monday (Mexican convention).</summary>
    public static (DateOnly From, DateOnly To) Resolve(PeriodPreset preset, DateOnly today, DateOnly? from = null, DateOnly? to = null)
    {
        switch (preset)
        {
            case PeriodPreset.Today: return (today, today);
            case PeriodPreset.Yesterday: return (today.AddDays(-1), today.AddDays(-1));
            case PeriodPreset.ThisWeek: return (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today);
            case PeriodPreset.ThisMonth: return (new DateOnly(today.Year, today.Month, 1), today);
            case PeriodPreset.LastMonth:
                var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
                return (first, first.AddMonths(1).AddDays(-1));
            case PeriodPreset.ThisYear: return (new DateOnly(today.Year, 1, 1), today);
            default:
                var f = from ?? today;
                var t = to ?? today;
                return f <= t ? (f, t) : (t, f);
        }
    }
}

/// <summary>Sales and profit reports. All sums run in SQLite on integer centavos.</summary>
public class ReportService(StoreDbContext db)
{
    private IQueryable<SaleItem> Items(DateOnly from, DateOnly to, int? ownerId) =>
        db.SaleItems.AsNoTracking().Where(i => i.BusinessDate >= from && i.BusinessDate <= to
            && i.Sale!.Status == SaleStatus.Completed && (ownerId == null || i.OwnerId == ownerId));

    public async Task<PeriodSummary> GetSummaryAsync(DateOnly from, DateOnly to, int? ownerId = null)
    {
        var agg = await Items(from, to, ownerId).GroupBy(_ => 1).Select(g => new
        {
            Revenue = g.Sum(i => i.LineTotalCents), Cost = g.Sum(i => i.CostCents),
            Discount = g.Sum(i => i.DiscountCents), Lines = g.Count()
        }).FirstOrDefaultAsync();
        int sales = await Items(from, to, ownerId).Select(i => i.SaleId).Distinct().CountAsync();
        int cancelled = await db.Sales.CountAsync(s => s.BusinessDate >= from && s.BusinessDate <= to && s.Status == SaleStatus.Cancelled);
        return new PeriodSummary(from, to, agg?.Revenue ?? 0, agg?.Cost ?? 0, agg?.Discount ?? 0, sales, cancelled);
    }

    public async Task<List<GroupTotal>> ByDayAsync(DateOnly from, DateOnly to, int? ownerId = null)
    {
        var rows = await Items(from, to, ownerId).GroupBy(i => i.BusinessDate)
            .Select(g => new { Day = g.Key, Revenue = g.Sum(i => i.LineTotalCents), Cost = g.Sum(i => i.CostCents),
                               Sales = g.Select(i => i.SaleId).Distinct().Count() })
            .OrderBy(x => x.Day).ToListAsync();
        return rows.Select(r => new GroupTotal(r.Day.ToString("dd/MM/yyyy"), r.Revenue, r.Cost, r.Sales, 0) { Date = r.Day }).ToList();
    }

    public async Task<List<GroupTotal>> ByMonthAsync(DateOnly from, DateOnly to, int? ownerId = null)
    {
        var rows = await Items(from, to, ownerId).GroupBy(i => i.YearMonth)
            .Select(g => new { Ym = g.Key, Revenue = g.Sum(i => i.LineTotalCents), Cost = g.Sum(i => i.CostCents),
                               Sales = g.Select(i => i.SaleId).Distinct().Count() })
            .OrderBy(x => x.Ym).ToListAsync();
        return rows.Select(r => new GroupTotal($"{TrendService.MonthName(r.Ym % 100)} {r.Ym / 100}", r.Revenue, r.Cost, r.Sales, 0)
                                { Date = new DateOnly(r.Ym / 100, r.Ym % 100, 1) }).ToList();
    }

    public async Task<List<GroupTotal>> ByYearAsync(DateOnly from, DateOnly to, int? ownerId = null)
    {
        var months = await ByMonthAsync(from, to, ownerId);
        return months.GroupBy(m => m.Date!.Value.Year)
            .Select(g => new GroupTotal(g.Key.ToString(), g.Sum(x => x.RevenueCents), g.Sum(x => x.CostCents), g.Sum(x => x.SaleCount), 0)
                         { Date = new DateOnly(g.Key, 1, 1) })
            .ToList();
    }

    public async Task<List<GroupTotal>> ByOwnerAsync(DateOnly from, DateOnly to)
    {
        var rows = await Items(from, to, null).GroupBy(i => i.OwnerId)
            .Select(g => new { OwnerId = g.Key, Revenue = g.Sum(i => i.LineTotalCents), Cost = g.Sum(i => i.CostCents),
                               Sales = g.Select(i => i.SaleId).Distinct().Count(), Units = g.Sum(i => i.Quantity) })
            .ToListAsync();
        var names = await db.Owners.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Name);
        return rows.Select(r => new GroupTotal(names.GetValueOrDefault(r.OwnerId, "?"), r.Revenue, r.Cost, r.Sales, r.Units) { Id = r.OwnerId })
                   .OrderByDescending(r => r.RevenueCents).ToList();
    }

    public async Task<List<GroupTotal>> ByCategoryAsync(DateOnly from, DateOnly to, int? ownerId = null)
    {
        var rows = await Items(from, to, ownerId).GroupBy(i => i.CategoryName)
            .Select(g => new { Name = g.Key, Revenue = g.Sum(i => i.LineTotalCents), Cost = g.Sum(i => i.CostCents),
                               Sales = g.Select(i => i.SaleId).Distinct().Count() })
            .ToListAsync();
        return rows.Select(r => new GroupTotal(r.Name ?? "Sin categoría", r.Revenue, r.Cost, r.Sales, 0))
                   .OrderByDescending(r => r.RevenueCents).ToList();
    }

    public async Task<List<GroupTotal>> BySupplierAsync(DateOnly from, DateOnly to, int? ownerId = null)
    {
        var rows = await Items(from, to, ownerId).GroupBy(i => i.SupplierId)
            .Select(g => new { SupplierId = g.Key, Revenue = g.Sum(i => i.LineTotalCents), Cost = g.Sum(i => i.CostCents),
                               Sales = g.Select(i => i.SaleId).Distinct().Count() })
            .ToListAsync();
        var names = await db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name);
        return rows.Select(r => new GroupTotal(r.SupplierId is int id ? names.GetValueOrDefault(id, "?") : "Sin proveedor",
                                               r.Revenue, r.Cost, r.Sales, 0) { Id = r.SupplierId })
                   .OrderByDescending(r => r.RevenueCents).ToList();
    }

    public async Task<List<GroupTotal>> ByPaymentMethodAsync(DateOnly from, DateOnly to, int? ownerId = null)
    {
        var rows = await Items(from, to, ownerId).GroupBy(i => i.Sale!.PaymentMethodName)
            .Select(g => new { Name = g.Key, Revenue = g.Sum(i => i.LineTotalCents), Cost = g.Sum(i => i.CostCents),
                               Sales = g.Select(i => i.SaleId).Distinct().Count() })
            .ToListAsync();
        return rows.Select(r => new GroupTotal(r.Name, r.Revenue, r.Cost, r.Sales, 0)).OrderByDescending(r => r.RevenueCents).ToList();
    }

    /// <summary>Products ranked by units. Worst sellers include active products with zero sales in the period.</summary>
    public async Task<List<ProductSales>> ProductRankingAsync(DateOnly from, DateOnly to, int? ownerId, bool best, int take = 20)
    {
        var sold = await Items(from, to, ownerId).GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.LineTotalCents), Cost = g.Sum(i => i.CostCents) })
            .ToDictionaryAsync(x => x.ProductId);

        if (best)
        {
            var ids = sold.Values.OrderByDescending(x => x.Revenue).Take(take * 3).Select(x => x.ProductId).ToList();
            var info = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            return ids.Where(info.ContainsKey).Select(id => new ProductSales(id, info[id].Name, info[id].Unit, sold[id].Units, sold[id].Revenue, sold[id].Cost))
                      .OrderByDescending(x => x.Units / (double)Qty.Scale(x.Unit)).ThenByDescending(x => x.RevenueCents).Take(take).ToList();
        }

        var products = await db.Products.AsNoTracking().Where(p => p.IsActive).Select(p => new { p.Id, p.Name, p.Unit }).ToListAsync();
        return products.Select(p => sold.TryGetValue(p.Id, out var s)
                ? new ProductSales(p.Id, p.Name, p.Unit, s.Units, s.Revenue, s.Cost)
                : new ProductSales(p.Id, p.Name, p.Unit, 0, 0, 0))
            .OrderBy(x => x.Units / (double)Qty.Scale(x.Unit)).ThenBy(x => x.Name).Take(take).ToList();
    }

    /// <summary>Every sale line in the range (for Excel detail sheets).</summary>
    public Task<List<SaleItem>> GetItemsAsync(DateOnly from, DateOnly to, int? ownerId) =>
        Items(from, to, ownerId).Include(i => i.Sale).OrderBy(i => i.SaleId).ThenBy(i => i.Id).ToListAsync();

    public Task<List<Sale>> GetSalesAsync(DateOnly from, DateOnly to) =>
        db.Sales.AsNoTracking().Include(s => s.User).Where(s => s.BusinessDate >= from && s.BusinessDate <= to)
          .OrderBy(s => s.Id).ToListAsync();
}
