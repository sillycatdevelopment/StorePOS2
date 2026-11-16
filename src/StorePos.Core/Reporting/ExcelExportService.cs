using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;

namespace StorePos.Core.Reporting;

public enum ColKind { Text, Int, Money, Qty, Date, DateTime, Percent }

/// <summary>Column of an exported sheet. Money values are passed as long centavos.</summary>
public record Col<T>(string Header, Func<T, object?> Value, ColKind Kind = ColKind.Text, bool Total = false);

/// <summary>Excel exports built with ClosedXML: clear headers, MXN currency format, dates, totals, filters.</summary>
public class ExcelExportService(StoreDbContext db, ReportService reports, InventoryService inventory,
                                CashService cash, StockingService stocking, SettingsService settings, OwnerService owners)
{
    private const string MoneyFmt = "\"$\"#,##0.00;-\"$\"#,##0.00";
    private const string DateFmt = "dd/mm/yyyy";
    private const string DateTimeFmt = "dd/mm/yyyy hh:mm";

    // ------------------------------------------------------------------ generic sheet writer

    public static IXLWorksheet AddSheet<T>(XLWorkbook wb, string name, IReadOnlyList<T> rows, IReadOnlyList<Col<T>> cols, string? title = null)
    {
        var ws = wb.Worksheets.Add(SafeName(wb, name));
        int r = 1;
        if (title is not null)
        {
            ws.Cell(r, 1).Value = title;
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Font.FontSize = 13;
            r += 2;
        }
        int headerRow = r;
        for (int c = 0; c < cols.Count; c++)
            ws.Cell(r, c + 1).Value = cols[c].Header;
        var header = ws.Range(r, 1, r, cols.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        foreach (var row in rows)
        {
            r++;
            for (int c = 0; c < cols.Count; c++)
                Write(ws.Cell(r, c + 1), cols[c].Value(row), cols[c].Kind);
        }
        int lastData = r;

        if (rows.Count > 0 && cols.Any(c => c.Total))
        {
            r++;
            ws.Cell(r, 1).Value = "TOTAL";
            for (int c = 0; c < cols.Count; c++)
            {
                if (!cols[c].Total) continue;
                var cell = ws.Cell(r, c + 1);
                cell.FormulaA1 = $"SUM({ws.Cell(headerRow + 1, c + 1).Address.ToStringRelative()}:{ws.Cell(lastData, c + 1).Address.ToStringRelative()})";
                ApplyFormat(cell, cols[c].Kind);
            }
            var totals = ws.Range(r, 1, r, cols.Count);
            totals.Style.Font.Bold = true;
            totals.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        }

        if (rows.Count > 0) ws.Range(headerRow, 1, lastData, cols.Count).SetAutoFilter();
        ws.SheetView.FreezeRows(headerRow);
        ws.Columns(1, cols.Count).AdjustToContents(headerRow, Math.Min(lastData, headerRow + 300));
        foreach (var col in ws.Columns(1, cols.Count)) if (col.Width > 60) col.Width = 60;
        return ws;
    }

    /// <summary>Two-column "concept / value" sheet for summaries.</summary>
    public static void AddKeyValueSheet(XLWorkbook wb, string name, string title, IEnumerable<(string Label, object Value, ColKind Kind)> rows)
    {
        var ws = wb.Worksheets.Add(SafeName(wb, name));
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        int r = 3;
        foreach (var (label, value, kind) in rows)
        {
            ws.Cell(r, 1).Value = label;
            ws.Cell(r, 1).Style.Font.Bold = true;
            Write(ws.Cell(r, 2), value, kind);
            r++;
        }
        ws.Columns(1, 2).AdjustToContents();
    }

    private static void Write(IXLCell cell, object? value, ColKind kind)
    {
        switch (value)
        {
            case null: break;
            case long cents when kind == ColKind.Money: cell.Value = (double)Money.ToPesos(cents); break;
            case DateOnly d: cell.Value = d.ToDateTime(TimeOnly.MinValue); break;
            case DateTime dt: cell.Value = dt; break;
            case string s: cell.Value = s; break;
            case bool b: cell.Value = b ? "Sí" : "No"; break;
            case decimal m: cell.Value = (double)m; break;
            case double dbl: cell.Value = dbl; break;
            case int i: cell.Value = (double)i; break;
            case long l: cell.Value = (double)l; break;
            default: cell.Value = value.ToString(); break;
        }
        ApplyFormat(cell, kind);
    }

    private static void ApplyFormat(IXLCell cell, ColKind kind)
    {
        switch (kind)
        {
            case ColKind.Money: cell.Style.NumberFormat.Format = MoneyFmt; break;
            case ColKind.Date: cell.Style.NumberFormat.Format = DateFmt; break;
            case ColKind.DateTime: cell.Style.NumberFormat.Format = DateTimeFmt; break;
            case ColKind.Qty: cell.Style.NumberFormat.Format = "#,##0.###"; break;
            case ColKind.Int: cell.Style.NumberFormat.Format = "#,##0"; break;
            case ColKind.Percent: cell.Style.NumberFormat.Format = "0.0%"; break;
        }
    }

    private static string SafeName(XLWorkbook wb, string name)
    {
        var clean = new string(name.Where(c => !"[]:*?/\\".Contains(c)).ToArray());
        if (clean.Length > 31) clean = clean[..31];
        var candidate = clean;
        for (int i = 2; wb.Worksheets.Contains(candidate); i++)
            candidate = (clean.Length > 27 ? clean[..27] : clean) + " " + i;
        return candidate;
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string Range(DateOnly from, DateOnly to) =>
        from == to ? from.ToString("dd/MM/yyyy") : $"{from:dd/MM/yyyy} al {to:dd/MM/yyyy}";

    private async Task<string> OwnerLabel(int? ownerId) =>
        ownerId is null ? "Todos los dueños" : await db.Owners.Where(o => o.Id == ownerId).Select(o => o.Name).FirstOrDefaultAsync() ?? "?";

    private static readonly Col<GroupTotal>[] GroupCols =
    {
        new("Periodo / grupo", g => g.Label),
        new("Ventas", g => g.SaleCount, ColKind.Int, true),
        new("Ingresos", g => g.RevenueCents, ColKind.Money, true),
        new("Costo", g => g.CostCents, ColKind.Money, true),
        new("Ganancia bruta", g => g.ProfitCents, ColKind.Money, true),
        new("Margen", g => g.RevenueCents == 0 ? 0 : g.ProfitCents / (double)g.RevenueCents, ColKind.Percent),
    };

    private static readonly Col<SaleItem>[] ItemCols =
    {
        new("Venta #", i => i.SaleId, ColKind.Int),
        new("Fecha", i => i.Sale!.CreatedAt, ColKind.DateTime),
        new("Producto", i => i.ProductName),
        new("Código", i => i.Barcode),
        new("Categoría", i => i.CategoryName),
        new("Dueño", i => i.OwnerName),
        new("Cantidad", i => Qty.ToDisplay(i.Quantity, i.Unit), ColKind.Qty),
        new("Unidad", i => Qty.UnitName(i.Unit)),
        new("Precio unitario", i => i.UnitPriceCents, ColKind.Money),
        new("Descuento", i => i.DiscountCents, ColKind.Money, true),
        new("Importe", i => i.LineTotalCents, ColKind.Money, true),
        new("Costo", i => i.CostCents, ColKind.Money, true),
        new("Ganancia", i => i.LineTotalCents - i.CostCents, ColKind.Money, true),
        new("Forma de pago", i => i.Sale!.PaymentMethodName),
    };

    // ------------------------------------------------------------------ exports

    /// <summary>Sales for a range: summary, grouping by day/month/year, tickets, lines, owners, products, payment methods.</summary>
    public async Task<byte[]> SalesAsync(DateOnly from, DateOnly to, int? ownerId, string grouping)
    {
        var store = await settings.GetAsync(SettingKeys.StoreName);
        var ownerLabel = await OwnerLabel(ownerId);
        using var wb = new XLWorkbook();

        var s = await reports.GetSummaryAsync(from, to, ownerId);
        var summaryRows = new List<(string, object, ColKind)>
        {
            ("Tienda", store, ColKind.Text), ("Periodo", Range(from, to), ColKind.Text), ("Dueño", ownerLabel, ColKind.Text),
            ("Ventas completadas", s.SaleCount, ColKind.Int), ("Ventas canceladas", s.CancelledCount, ColKind.Int),
            ("Ingresos", s.RevenueCents, ColKind.Money), ("Descuentos", s.DiscountCents, ColKind.Money),
            ("Costo de lo vendido", s.CostCents, ColKind.Money), ("Ganancia bruta", s.ProfitCents, ColKind.Money),
            ("Ticket promedio", s.AverageTicketCents, ColKind.Money),
        };
        AddKeyValueSheet(wb, "Resumen", "Reporte de ventas", summaryRows);

        var grouped = grouping switch
        {
            "month" => (Name: "Por mes", Rows: await reports.ByMonthAsync(from, to, ownerId)),
            "year" => (Name: "Por año", Rows: await reports.ByYearAsync(from, to, ownerId)),
            _ => (Name: "Por día", Rows: await reports.ByDayAsync(from, to, ownerId))
        };
        AddSheet(wb, grouped.Name, grouped.Rows, GroupCols);

        if (ownerId is null)
        {
            var sales = await reports.GetSalesAsync(from, to);
            AddSheet(wb, "Tickets", sales, new Col<Sale>[]
            {
                new("Venta #", x => x.Id, ColKind.Int), new("Fecha", x => x.CreatedAt, ColKind.DateTime),
                new("Estado", x => x.Status == SaleStatus.Completed ? "Completada" : "Cancelada"),
                new("Forma de pago", x => x.PaymentMethodName),
                new("Subtotal", x => x.SubtotalCents, ColKind.Money), new("Descuento", x => x.DiscountCents, ColKind.Money),
                new("Total", x => x.Status == SaleStatus.Completed ? x.TotalCents : 0L, ColKind.Money, true),
                new("Costo", x => x.Status == SaleStatus.Completed ? x.CostCents : 0L, ColKind.Money, true),
                new("Ganancia", x => x.Status == SaleStatus.Completed ? x.ProfitCents : 0L, ColKind.Money, true),
                new("Cajero", x => x.User?.DisplayName), new("Motivo de cancelación", x => x.CancelReason),
            });
        }

        AddSheet(wb, "Detalle", await reports.GetItemsAsync(from, to, ownerId), ItemCols);
        if (ownerId is null) AddSheet(wb, "Por dueño", await reports.ByOwnerAsync(from, to), GroupCols);
        AddSheet(wb, "Por producto", await reports.ProductRankingAsync(from, to, ownerId, true, int.MaxValue / 4), ProductCols);
        AddSheet(wb, "Por forma de pago", await reports.ByPaymentMethodAsync(from, to, ownerId), GroupCols);
        return Save(wb);
    }

    private static readonly Col<ProductSales>[] ProductCols =
    {
        new("Producto", p => p.Name),
        new("Unidades", p => Qty.ToDisplay(p.Units, p.Unit), ColKind.Qty),
        new("Unidad", p => Qty.UnitName(p.Unit)),
        new("Ingresos", p => p.RevenueCents, ColKind.Money, true),
        new("Costo", p => p.CostCents, ColKind.Money, true),
        new("Ganancia", p => p.ProfitCents, ColKind.Money, true),
    };

    public async Task<byte[]> ProfitAsync(DateOnly from, DateOnly to, int? ownerId)
    {
        using var wb = new XLWorkbook();
        var title = $"Ganancias {Range(from, to)} — {await OwnerLabel(ownerId)}";
        if (ownerId is null) AddSheet(wb, "Por dueño", await reports.ByOwnerAsync(from, to), GroupCols, title);
        AddSheet(wb, "Por producto", await reports.ProductRankingAsync(from, to, ownerId, true, int.MaxValue / 4), ProductCols, title);
        AddSheet(wb, "Por categoría", await reports.ByCategoryAsync(from, to, ownerId), GroupCols, title);
        AddSheet(wb, "Por proveedor", await reports.BySupplierAsync(from, to, ownerId), GroupCols, title);
        AddSheet(wb, "Por mes", await reports.ByMonthAsync(from, to, ownerId), GroupCols, title);
        return Save(wb);
    }

    /// <summary>Everything about one owner: summary, sales detail, current inventory and lots.</summary>
    public async Task<byte[]> OwnerAsync(int ownerId, DateOnly from, DateOnly to)
    {
        var name = await OwnerLabel(ownerId);
        using var wb = new XLWorkbook();
        var s = await reports.GetSummaryAsync(from, to, ownerId);
        var (cost, retail) = await inventory.GetInventoryValueAsync(ownerId);
        var stats = (await owners.GetStatsAsync(from, to)).FirstOrDefault(x => x.Owner.Id == ownerId);
        AddKeyValueSheet(wb, "Resumen", $"Reporte del dueño {name}", new (string, object, ColKind)[]
        {
            ("Dueño", name, ColKind.Text), ("Periodo", Range(from, to), ColKind.Text),
            ("Ingresos", s.RevenueCents, ColKind.Money), ("Costo", s.CostCents, ColKind.Money),
            ("Ganancia bruta en ventas", s.ProfitCents, ColKind.Money), ("Ventas en que participó", s.SaleCount, ColKind.Int),
            ("Vendido a otros dueños (cajas, traspasos)", stats?.TransferRevenueCents ?? 0L, ColKind.Money),
            ("Costo de lo vendido a otros dueños", stats?.TransferCostCents ?? 0L, ColKind.Money),
            ("Ganancia total", stats?.ProfitCents ?? s.ProfitCents, ColKind.Money),
            ("Inventario actual (costo)", cost, ColKind.Money), ("Inventario actual (precio de venta)", retail, ColKind.Money),
        });
        AddSheet(wb, "Ventas", await reports.GetItemsAsync(from, to, ownerId), ItemCols, $"Ventas de {name}, {Range(from, to)}");
        AddSheet(wb, "Por producto", await reports.ProductRankingAsync(from, to, ownerId, true, int.MaxValue / 4), ProductCols);
        await AddInventorySheetsAsync(wb, new InventoryFilter { OwnerId = ownerId, Status = StockFilter.InStock }, $"Inventario de {name}");
        return Save(wb);
    }

    private async Task AddInventorySheetsAsync(XLWorkbook wb, InventoryFilter filter, string title)
    {
        var rows = (await inventory.GetInventoryAsync(filter, 1, 100_000)).Items;
        AddSheet(wb, "Inventario", rows, new Col<InventoryRow>[]
        {
            new("Producto", r => r.Name), new("Código", r => r.Barcode), new("Categoría", r => r.CategoryName),
            new("Proveedor", r => r.SupplierName),
            new("Existencia", r => Qty.ToDisplay(r.Stock, r.Unit), ColKind.Qty), new("Unidad", r => Qty.UnitName(r.Unit)),
            new("Mínimo", r => Qty.ToDisplay(r.MinStock, r.Unit), ColKind.Qty),
            new("Estado", r => r.StatusText),
            new("Por dueño", r => string.Join(", ", r.Owners.Select(o => $"{o.OwnerName}: {Qty.Format(o.Qty, r.Unit)}"))),
            new("Precio compra", r => r.PurchasePriceCents, ColKind.Money), new("Precio venta", r => r.SalePriceCents, ColKind.Money),
            new("Valor a costo", r => r.CostValueCents, ColKind.Money, true), new("Valor a precio de venta", r => r.RetailValueCents, ColKind.Money, true),
        }, title);

        var ids = rows.Select(r => r.ProductId).ToList();
        var lots = await db.InventoryLots.AsNoTracking().Include(l => l.Product).Include(l => l.Owner).Include(l => l.Supplier)
            .Where(l => l.QtyRemaining > 0 && ids.Contains(l.ProductId) && (filter.OwnerId == null || l.OwnerId == filter.OwnerId))
            .OrderBy(l => l.Product!.SearchName).ThenBy(l => l.Owner!.Name).ThenBy(l => l.ReceivedAt).ToListAsync();
        AddSheet(wb, "Lotes por dueño", lots, new Col<InventoryLot>[]
        {
            new("Producto", l => l.Product!.Name), new("Dueño", l => l.Owner!.Name), new("Proveedor", l => l.Supplier?.Name),
            new("Recibido", l => l.ReceivedAt, ColKind.DateTime), new("Origen", l => DisplayNames.Of(l.Source)),
            new("Cantidad restante", l => Qty.ToDisplay(l.QtyRemaining, l.Product!.Unit), ColKind.Qty),
            new("Costo unitario", l => l.UnitCostCents, ColKind.Money),
            new("Valor a costo", l => Money.LineAmount(l.UnitCostCents, l.QtyRemaining, l.Product!.Unit), ColKind.Money, true),
        });
    }

    public async Task<byte[]> InventoryAsync(InventoryFilter filter, string title)
    {
        using var wb = new XLWorkbook();
        await AddInventorySheetsAsync(wb, filter, title);
        return Save(wb);
    }

    public async Task<byte[]> DeliveriesAsync(DateOnly from, DateOnly to, int? supplierId)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var items = await db.DeliveryItems.AsNoTracking()
            .Include(i => i.Delivery!).ThenInclude(d => d.Supplier).Include(i => i.Product).Include(i => i.Owner)
            .Where(i => i.Delivery!.ReceivedAt >= start && i.Delivery.ReceivedAt < end && (supplierId == null || i.Delivery.SupplierId == supplierId))
            .OrderBy(i => i.DeliveryId).ThenBy(i => i.Id).ToListAsync();
        using var wb = new XLWorkbook();
        var deliveries = items.GroupBy(i => i.DeliveryId).Select(g => g.First().Delivery!).ToList();
        AddSheet(wb, "Entregas", deliveries, new Col<Delivery>[]
        {
            new("Entrega #", d => d.Id, ColKind.Int), new("Fecha", d => d.ReceivedAt, ColKind.DateTime),
            new("Proveedor", d => d.Supplier!.Name), new("Factura / nota", d => d.InvoiceNumber),
            new("Renglones", d => items.Count(i => i.DeliveryId == d.Id), ColKind.Int),
            new("Costo total", d => d.TotalCostCents, ColKind.Money, true), new("Notas", d => d.Notes),
        }, $"Entregas de proveedores {Range(from, to)}");
        AddSheet(wb, "Detalle", items, new Col<DeliveryItem>[]
        {
            new("Entrega #", i => i.DeliveryId, ColKind.Int), new("Fecha", i => i.Delivery!.ReceivedAt, ColKind.DateTime),
            new("Proveedor", i => i.Delivery!.Supplier!.Name), new("Producto", i => i.Product!.Name), new("Dueño", i => i.Owner!.Name),
            new("Cantidad", i => Qty.ToDisplay(i.Quantity, i.Product!.Unit), ColKind.Qty), new("Unidad", i => Qty.UnitName(i.Product!.Unit)),
            new("Costo unitario", i => i.UnitCostCents, ColKind.Money), new("Importe", i => i.LineCostCents, ColKind.Money, true),
        });
        return Save(wb);
    }

    public async Task<byte[]> StockingListAsync(int listId)
    {
        var list = await stocking.GetAsync(listId) ?? throw new DomainException("Lista no encontrada.");
        using var wb = new XLWorkbook();
        var cols = new Col<StockingListItem>[]
        {
            new("Producto", i => i.Description), new("Proveedor", i => i.Supplier?.Name ?? "Sin proveedor"), new("Dueño", i => i.Owner?.Name),
            new("Existencia actual", i => i.Product is null ? null : Qty.ToDisplay(i.CurrentStock, i.Product.Unit), ColKind.Qty),
            new("Venta diaria prom.", i => i.Product is null ? null : Math.Round(i.AvgDailySales / Qty.Scale(i.Product.Unit), 2), ColKind.Qty),
            new("Días a próxima entrega", i => i.DaysToNextDelivery, ColKind.Int),
            new("Sugerido", i => i.Product is null ? i.SuggestedQty : Qty.ToDisplay(i.SuggestedQty, i.Product.Unit), ColKind.Qty),
            new("A comprar", i => i.Product is null ? i.Quantity : Qty.ToDisplay(i.Quantity, i.Product.Unit), ColKind.Qty),
            new("Unidad", i => i.Product is null ? "" : Qty.UnitName(i.Product.Unit)),
            new("Costo unitario est.", i => i.EstimatedUnitCostCents, ColKind.Money),
            new("Importe estimado", i => i.Product is null ? 0L : Money.LineAmount(i.EstimatedUnitCostCents, i.Quantity, i.Product.Unit), ColKind.Money, true),
            new("Motivo", i => i.Reason),
        };
        var title = $"{list.Name} — {DisplayNames.Of(list.Status)} — {list.CreatedAt:dd/MM/yyyy}";
        var items = list.Items.OrderBy(i => i.Description).ToList();
        if (list.GroupBy == StockingGroupBy.None)
            AddSheet(wb, "Lista", items, cols, title);
        else
            foreach (var g in items.GroupBy(i => list.GroupBy == StockingGroupBy.Supplier ? i.Supplier?.Name ?? "Sin proveedor" : i.Owner?.Name ?? "Sin dueño").OrderBy(g => g.Key))
                AddSheet(wb, g.Key, g.ToList(), cols, $"{title} — {g.Key}");
        return Save(wb);
    }

    public async Task<byte[]> ClosingsAsync(DateOnly from, DateOnly to)
    {
        var days = await db.DailyClosings.AsNoTracking().Include(d => d.OpenedByUser).Include(d => d.ClosedByUser)
            .Where(d => d.BusinessDate >= from && d.BusinessDate <= to && d.Status == ClosingStatus.Closed)
            .OrderBy(d => d.BusinessDate).ThenBy(d => d.Id).ToListAsync();
        var ids = days.Select(d => d.Id).ToList();
        using var wb = new XLWorkbook();
        AddSheet(wb, "Cortes", days, new Col<DailyClosing>[]
        {
            new("Corte #", d => d.Id, ColKind.Int), new("Fecha", d => d.BusinessDate, ColKind.Date),
            new("Apertura", d => d.OpenedAt, ColKind.DateTime), new("Cierre", d => d.ClosedAt, ColKind.DateTime),
            new("Fondo inicial", d => d.OpeningCashCents, ColKind.Money),
            new("Ventas", d => d.TotalSalesCents, ColKind.Money, true), new("Ventas en efectivo", d => d.CashSalesCents, ColKind.Money, true),
            new("Ventas no efectivo", d => d.NonCashSalesCents, ColKind.Money, true),
            new("Depósitos", d => d.DepositsCents + d.OtherInCents, ColKind.Money, true),
            new("Retiros", d => d.WithdrawalsCents + d.OtherOutCents, ColKind.Money, true),
            new("Gastos", d => d.ExpensesCents, ColKind.Money, true), new("Reembolsos", d => d.RefundsCents, ColKind.Money, true),
            new("Efectivo esperado", d => d.ExpectedCashCents, ColKind.Money), new("Efectivo contado", d => d.CountedCashCents, ColKind.Money),
            new("Diferencia", d => d.DifferenceCents, ColKind.Money, true), new("Ganancia bruta", d => d.GrossProfitCents, ColKind.Money, true),
            new("Abrió", d => d.OpenedByUser?.DisplayName), new("Cerró", d => d.ClosedByUser?.DisplayName), new("Notas", d => d.Notes),
        }, $"Cortes de caja {Range(from, to)}");

        var movements = await db.CashMovements.AsNoTracking().Include(m => m.User).Include(m => m.DailyClosing)
            .Where(m => ids.Contains(m.DailyClosingId)).OrderBy(m => m.Id).ToListAsync();
        AddSheet(wb, "Movimientos", movements, new Col<CashMovement>[]
        {
            new("Corte #", m => m.DailyClosingId, ColKind.Int), new("Fecha", m => m.CreatedAt, ColKind.DateTime),
            new("Tipo", m => DisplayNames.Of(m.Type)), new("Afecta efectivo", m => m.AffectsCash),
            new("Monto", m => m.AmountCents, ColKind.Money), new("Descripción", m => m.Description), new("Usuario", m => m.User?.DisplayName),
        });

        var payments = await db.DailyClosingPayments.AsNoTracking().Include(p => p.DailyClosing)
            .Where(p => ids.Contains(p.DailyClosingId)).OrderBy(p => p.DailyClosingId).ThenBy(p => p.MethodName).ToListAsync();
        AddSheet(wb, "Por forma de pago", payments, new Col<DailyClosingPayment>[]
        {
            new("Corte #", p => p.DailyClosingId, ColKind.Int), new("Fecha", p => p.DailyClosing!.BusinessDate, ColKind.Date),
            new("Forma de pago", p => p.MethodName), new("Es efectivo", p => p.IsCash),
            new("Ventas", p => p.SaleCount, ColKind.Int, true), new("Importe", p => p.AmountCents, ColKind.Money, true),
        });
        return Save(wb);
    }

    public async Task<byte[]> CustomerRequestsAsync(RequestStatus? status)
    {
        var rows = await db.CustomerRequests.AsNoTracking().Include(r => r.Category).Include(r => r.Product)
            .Where(r => status == null || r.Status == status)
            .OrderByDescending(r => r.RequestCount).ToListAsync();
        using var wb = new XLWorkbook();
        AddSheet(wb, "Solicitudes", rows, new Col<CustomerRequest>[]
        {
            new("Producto solicitado", r => r.ProductName), new("Código", r => r.Barcode), new("Categoría", r => r.Category?.Name),
            new("Producto en catálogo", r => r.Product?.Name), new("Estado", r => DisplayNames.Of(r.Status)),
            new("Veces solicitado", r => r.RequestCount, ColKind.Int, true), new("Cantidad total pedida", r => r.TotalQuantity, ColKind.Int, true),
            new("Primera solicitud", r => r.FirstRequestedAt, ColKind.DateTime), new("Última solicitud", r => r.LastRequestedAt, ColKind.DateTime),
            new("Notas", r => r.Notes),
        }, "Productos solicitados por clientes");
        return Save(wb);
    }
}
