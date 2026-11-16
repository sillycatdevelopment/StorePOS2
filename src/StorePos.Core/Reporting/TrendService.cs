using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;

namespace StorePos.Core.Reporting;

public enum TrendDirection { Unknown, Up, Stable, Down }

public enum StockAdvice { InsufficientData, Increase, Maintain, Reduce, Stop }

/// <summary>
/// Demand analysis from sale history. Deliberately simple and explainable:
/// recent window vs. previous window, plus monthly seasonal indexes when ≥ 12 months exist.
/// Never presents a trend or seasonal factor when the data is too thin.
/// </summary>
public class TrendService(StoreDbContext db)
{
    public const int MinDaysForTrend = 14;
    public const int MinUnitsForTrend = 5;   // in display units (pieces / kg)
    public const double TrendThreshold = 0.20;

    private static readonly string[] MonthNames = { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
    public static string MonthName(int m) => MonthNames[m - 1];

    private record Raw(int ProductId, string Name, UnitType Unit, long MinStock, DateTime FirstSeen);

    /// <summary>Per-product demand figures used by trends, dashboard and reorder suggestions.</summary>
    public async Task<Dictionary<int, ProductDemand>> GetDemandAsync(DateOnly today, int window = 28)
    {
        var recentFrom = today.AddDays(-window + 1);
        var prevFrom = today.AddDays(-2 * window + 1);
        var last7From = today.AddDays(-6);
        var seasonFrom = new DateOnly(today.Year - 3, today.Month, 1);
        int seasonFromYm = seasonFrom.Year * 100 + seasonFrom.Month;

        var products = await db.Products.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Name, p.Unit, p.MinStock, p.CreatedAt,
                               FirstLot = db.InventoryLots.Where(l => l.ProductId == p.Id).Min(l => (DateTime?)l.ReceivedAt) })
            .ToListAsync();

        var windows = await db.SaleItems.AsNoTracking()
            .Where(i => i.BusinessDate >= prevFrom && i.BusinessDate <= today && i.Sale!.Status == SaleStatus.Completed)
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Recent = g.Sum(i => i.BusinessDate >= recentFrom ? i.Quantity : 0),
                Previous = g.Sum(i => i.BusinessDate < recentFrom ? i.Quantity : 0),
                Last7 = g.Sum(i => i.BusinessDate >= last7From ? i.Quantity : 0)
            }).ToDictionaryAsync(x => x.ProductId);

        var monthly = await db.SaleItems.AsNoTracking()
            .Where(i => i.YearMonth >= seasonFromYm && i.Sale!.Status == SaleStatus.Completed)
            .GroupBy(i => new { i.ProductId, i.YearMonth })
            .Select(g => new { g.Key.ProductId, g.Key.YearMonth, Qty = g.Sum(i => i.Quantity) })
            .ToListAsync();
        var monthlyByProduct = monthly.GroupBy(m => m.ProductId).ToDictionary(g => g.Key, g => g.ToDictionary(x => x.YearMonth, x => x.Qty));

        var lastSale = await db.SaleItems.AsNoTracking().Where(i => i.Sale!.Status == SaleStatus.Completed)
            .GroupBy(i => i.ProductId).Select(g => new { ProductId = g.Key, Last = g.Max(i => i.BusinessDate) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Last);

        var result = new Dictionary<int, ProductDemand>(products.Count);
        foreach (var p in products)
        {
            var firstSeen = DateOnly.FromDateTime(p.FirstLot is { } fl && fl < p.CreatedAt ? fl : p.CreatedAt);
            int daysOfHistory = Math.Max(1, today.DayNumber - firstSeen.DayNumber + 1);
            windows.TryGetValue(p.Id, out var w);
            long recent = w?.Recent ?? 0, previous = w?.Previous ?? 0, last7 = w?.Last7 ?? 0;
            double scale = Qty.Scale(p.Unit);

            int recentDays = Math.Min(window, daysOfHistory);
            double avg = recent / (double)recentDays;
            bool reliable = daysOfHistory >= MinDaysForTrend && (recent + previous) / scale >= MinUnitsForTrend;

            var trend = TrendDirection.Unknown;
            double? change = null;
            if (reliable && daysOfHistory >= 2 * window)
            {
                if (previous == 0) trend = recent > 0 ? TrendDirection.Up : TrendDirection.Stable;
                else
                {
                    change = (recent - previous) / (double)previous;
                    trend = change > TrendThreshold ? TrendDirection.Up : change < -TrendThreshold ? TrendDirection.Down : TrendDirection.Stable;
                }
            }

            var season = Seasonality(monthlyByProduct.GetValueOrDefault(p.Id), firstSeen, today);
            // Adjust today's rate from the season we measured in to the season we are buying for.
            double factor = 1;
            if (season.Reliable)
            {
                int measured = today.AddDays(-window / 2).Month, upcoming = today.AddDays(15).Month;
                factor = Math.Clamp(season.Index[upcoming - 1] / Math.Max(0.05, season.Index[measured - 1]), 0.5, 2.0);
            }

            result[p.Id] = new ProductDemand
            {
                ProductId = p.Id, Name = p.Name, Unit = p.Unit, DaysOfHistory = daysOfHistory,
                UnitsLast7 = last7, UnitsLastWindow = recent, UnitsPreviousWindow = previous,
                DailyAverage = avg, AdjustedDailyAverage = avg * factor, SeasonalFactor = factor,
                Reliable = reliable, Trend = trend, ChangeRatio = change,
                SeasonalReliable = season.Reliable, SeasonalIndex = season.Index,
                LastSale = lastSale.TryGetValue(p.Id, out var ls) ? ls : null
            };
        }
        return result;
    }

    /// <summary>
    /// Monthly seasonal index: average units in calendar month m ÷ average units per month.
    /// Requires 12 complete months of history; otherwise all indexes are 1 and Reliable = false.
    /// </summary>
    public static (bool Reliable, double[] Index) Seasonality(Dictionary<int, long>? monthly, DateOnly firstSeen, DateOnly today)
    {
        var index = Enumerable.Repeat(1.0, 12).ToArray();
        // Count only complete months between firstSeen and last month.
        var start = new DateOnly(firstSeen.Year, firstSeen.Month, 1);
        if (firstSeen.Day > 1) start = start.AddMonths(1);
        var end = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var months = new List<DateOnly>();
        for (var m = start; m <= end; m = m.AddMonths(1)) months.Add(m);
        if (months.Count > 36) months = months.Skip(months.Count - 36).ToList();
        if (months.Count < 12 || monthly is null) return (false, index);

        long total = 0;
        var sums = new double[12];
        var counts = new int[12];
        foreach (var m in months)
        {
            long q = monthly.GetValueOrDefault(m.Year * 100 + m.Month);
            total += q;
            sums[m.Month - 1] += q;
            counts[m.Month - 1]++;
        }
        double perMonth = total / (double)months.Count;
        if (perMonth <= 0) return (false, index);
        for (int i = 0; i < 12; i++)
            index[i] = counts[i] == 0 ? 1.0 : (sums[i] / counts[i]) / perMonth;
        return (true, index);
    }

    public static StockAdvice Advise(ProductDemand d, long stock, DateOnly today)
    {
        int sinceLastSale = d.LastSale is { } ls ? today.DayNumber - ls.DayNumber : int.MaxValue;
        if (d.DaysOfHistory >= 60 && sinceLastSale >= 60 && stock > 0) return StockAdvice.Stop;
        if (!d.Reliable) return StockAdvice.InsufficientData;
        double cover = d.AdjustedDailyAverage > 0 ? stock / d.AdjustedDailyAverage : double.MaxValue;
        if (d.Trend == TrendDirection.Down || cover > 45) return StockAdvice.Reduce;
        if (d.Trend == TrendDirection.Up || cover < 3) return StockAdvice.Increase;
        return StockAdvice.Maintain;
    }

    public static string Describe(StockAdvice a) => a switch
    {
        StockAdvice.Increase => "Aumentar existencia",
        StockAdvice.Maintain => "Mantener",
        StockAdvice.Reduce => "Reducir existencia",
        StockAdvice.Stop => "Considerar dejar de comprar",
        _ => "Datos insuficientes"
    };

    public static string Describe(TrendDirection t) => t switch
    {
        TrendDirection.Up => "↑ Aumenta",
        TrendDirection.Down => "↓ Disminuye",
        TrendDirection.Stable => "→ Estable",
        _ => "Datos insuficientes"
    };

    public static string SeasonNote(ProductDemand d)
    {
        if (!d.SeasonalReliable) return "Se necesitan 12 meses de historial";
        var low = Enumerable.Range(1, 12).Where(m => d.SeasonalIndex[m - 1] < 0.8).Select(MonthName).ToList();
        var high = Enumerable.Range(1, 12).Where(m => d.SeasonalIndex[m - 1] > 1.2).Select(MonthName).ToList();
        if (low.Count == 0 && high.Count == 0) return "Sin variación estacional marcada";
        return string.Join(" · ", new[] { high.Count > 0 ? "Alta: " + string.Join(", ", high) : null,
                                          low.Count > 0 ? "Baja: " + string.Join(", ", low) : null }.Where(x => x != null));
    }

    /// <summary>Trend table for all active products, with stock and recommendation.</summary>
    public async Task<List<ProductTrendRow>> GetTrendTableAsync(DateOnly today, int window = 28)
    {
        var demand = await GetDemandAsync(today, window);
        var stock = await db.InventoryLots.AsNoTracking().GroupBy(l => l.ProductId)
            .Select(g => new { g.Key, Qty = g.Sum(l => l.QtyRemaining) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        return demand.Values.Select(d =>
        {
            long s = stock.GetValueOrDefault(d.ProductId);
            return new ProductTrendRow(d, s, Advise(d, s, today));
        }).OrderBy(r => r.Demand.Name).ToList();
    }

    /// <summary>Monthly units for one product over the last <paramref name="months"/> months (oldest first).</summary>
    public async Task<List<(int Year, int Month, long Qty, long RevenueCents)>> GetMonthlySeriesAsync(int productId, DateOnly today, int months = 24)
    {
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        int fromYm = from.Year * 100 + from.Month;
        var rows = await db.SaleItems.AsNoTracking()
            .Where(i => i.ProductId == productId && i.YearMonth >= fromYm && i.Sale!.Status == SaleStatus.Completed)
            .GroupBy(i => i.YearMonth)
            .Select(g => new { Ym = g.Key, Qty = g.Sum(i => i.Quantity), Rev = g.Sum(i => i.LineTotalCents) })
            .ToDictionaryAsync(x => x.Ym);
        var list = new List<(int, int, long, long)>();
        for (var m = from; m <= today; m = m.AddMonths(1))
        {
            rows.TryGetValue(m.Year * 100 + m.Month, out var r);
            list.Add((m.Year, m.Month, r?.Qty ?? 0, r?.Rev ?? 0));
        }
        return list;
    }

    /// <summary>Units per weekday over the last 12 weeks (Sunday = 0).</summary>
    public async Task<long[]> GetWeekdayProfileAsync(int productId, DateOnly today)
    {
        var from = today.AddDays(-83);
        var daily = await db.SaleItems.AsNoTracking()
            .Where(i => i.ProductId == productId && i.BusinessDate >= from && i.Sale!.Status == SaleStatus.Completed)
            .GroupBy(i => i.BusinessDate).Select(g => new { Day = g.Key, Qty = g.Sum(i => i.Quantity) }).ToListAsync();
        var result = new long[7];
        foreach (var d in daily) result[(int)d.Day.DayOfWeek] += d.Qty;
        return result;
    }
}
