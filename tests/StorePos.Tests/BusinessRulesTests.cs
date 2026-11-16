using Microsoft.EntityFrameworkCore;
using StorePos.Core.Models;
using StorePos.Core.Reporting;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;

namespace StorePos.Tests;

public class MoneyAndQuantityTests
{
    [Theory]
    [InlineData("18", 1800)]
    [InlineData("18.5", 1850)]
    [InlineData("$1,234.56", 123456)]
    [InlineData("0.005", 1)]          // away from zero
    public void Money_parses_to_centavos(string text, long cents)
    {
        Assert.True(Money.TryParse(text, out var c));
        Assert.Equal(cents, c);
    }

    [Fact]
    public void Kilogram_line_amount_rounds_to_the_centavo()
    {
        // $18.50/kg × 250 g = $4.625 → $4.63
        Assert.Equal(463, Money.LineAmount(1850, 250, UnitType.Kilogram));
        Assert.Equal(5400, Money.LineAmount(1800, 3, UnitType.Piece));
    }

    [Fact]
    public void Piece_quantities_must_be_whole()
    {
        Assert.Throws<DomainException>(() => Qty.FromDisplay(1.5m, UnitType.Piece));
        Assert.Equal(1500, Qty.FromDisplay(1.5m, UnitType.Kilogram));
    }

    [Fact]
    public void Proportional_split_always_adds_up_exactly()
    {
        var parts = SalesService.SplitProportional(1000, new long[] { 333, 333, 334 });
        Assert.Equal(1000, parts.Sum());
        var parts2 = SalesService.SplitProportional(7, new long[] { 1, 1, 1 });
        Assert.Equal(7, parts2.Sum());
    }

    [Fact]
    public void Text_normalization_ignores_case_accents_and_spacing()
    {
        Assert.Equal("cafe nino", TextNorm.Normalize("  Café   NIÑO "));
    }
}

public class OwnershipAndSalesTests
{
    [Fact]
    public async Task Same_product_two_owners_profits_stay_separate()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("Dueño A"), b = await h.OwnerAsync("Dueño B");
        int coke = await h.ProductAsync("Coca-Cola 600 ml", 18m, 12m, "7501055300075");
        await h.StockAsync(coke, a, 12, 12m);
        await h.StockAsync(coke, b, 8, 13m);
        await h.OpenCashAsync();

        int saleId = await h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"),
            Lines = { new SaleLineRequest { ProductId = coke, Quantity = 5, OwnerId = a } }
        });
        h.NewScope();

        var inv = h.Get<InventoryService>();
        Assert.Equal(7, await inv.GetStockAsync(coke, a));
        Assert.Equal(8, await inv.GetStockAsync(coke, b));

        var sale = await h.Get<SalesService>().GetSaleAsync(saleId);
        var item = Assert.Single(sale!.Items);
        Assert.Equal(a, item.OwnerId);
        Assert.Equal("Dueño A", item.OwnerName);
        Assert.Equal(9000, sale.TotalCents);
        Assert.Equal(6000, sale.CostCents);
        Assert.Equal(3000, sale.ProfitCents);

        var stats = await h.Get<OwnerService>().GetStatsAsync(h.Clock.Today, h.Clock.Today);
        Assert.Equal(3000, stats.Single(s => s.Owner.Id == a).ProfitCents);
        Assert.Equal(0, stats.Single(s => s.Owner.Id == b).RevenueCents);
        Assert.Equal(8 * 1300, stats.Single(s => s.Owner.Id == b).InventoryCostCents);
    }

    [Fact]
    public async Task Manual_mode_refuses_to_mix_owners_silently()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A"), b = await h.OwnerAsync("B");
        int p = await h.ProductAsync("Galletas", 20m);
        await h.StockAsync(p, a, 5, 10m);
        await h.StockAsync(p, b, 5, 11m);
        await h.OpenCashAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"),
            Lines = { new SaleLineRequest { ProductId = p, Quantity = 1 } }
        }));
        Assert.Contains("varios dueños", ex.Message);
    }

    [Fact]
    public async Task Single_owner_in_stock_is_used_automatically_in_manual_mode()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A"); await h.OwnerAsync("B");
        int p = await h.ProductAsync("Pan", 10m);
        await h.StockAsync(p, a, 5, 6m);
        await h.OpenCashAsync();
        int id = await h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"), Lines = { new SaleLineRequest { ProductId = p, Quantity = 2 } }
        });
        h.NewScope();
        Assert.Equal(a, (await h.Get<SalesService>().GetSaleAsync(id))!.Items.Single().OwnerId);
    }

    [Fact]
    public async Task Fifo_within_owner_gives_exact_cost()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("Leche", 30m);
        await h.StockAsync(p, a, 10, 10m);
        h.Clock.Now = h.Clock.Now.AddMinutes(1);
        await h.StockAsync(p, a, 10, 12m);
        await h.OpenCashAsync();

        int id = await h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"), Lines = { new SaleLineRequest { ProductId = p, Quantity = 15, OwnerId = a } }
        });
        h.NewScope();
        var item = (await h.Get<SalesService>().GetSaleAsync(id))!.Items.Single();
        Assert.Equal(10 * 1000 + 5 * 1200, item.CostCents);
        Assert.Equal(2, await h.Db.SaleItemLots.CountAsync(l => l.SaleItemId == item.Id));
        var lots = await h.Db.InventoryLots.Where(l => l.ProductId == p).OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(0, lots[0].QtyRemaining);
        Assert.Equal(5, lots[1].QtyRemaining);
    }

    [Fact]
    public async Task Automatic_mode_uses_priority_owner_then_splits_one_line_per_owner()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A"), b = await h.OwnerAsync("B");
        int p = await h.ProductAsync("Refresco", 18m, priorityOwner: b);
        await h.StockAsync(p, a, 12, 12m);
        await h.StockAsync(p, b, 8, 13m);
        await h.Get<SettingsService>().SetManyAsync(new Dictionary<string, string> { [SettingKeys.AllocationMode] = "Automatic" });
        await h.OpenCashAsync();

        int id = await h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"), Lines = { new SaleLineRequest { ProductId = p, Quantity = 10 } }
        });
        h.NewScope();
        var items = (await h.Get<SalesService>().GetSaleAsync(id))!.Items;
        Assert.Equal(8, items.Single(i => i.OwnerId == b).Quantity);
        Assert.Equal(2, items.Single(i => i.OwnerId == a).Quantity);
        Assert.Equal(8 * 1300 + 2 * 1200, items.Sum(i => i.CostCents));
    }

    [Fact]
    public async Task Insufficient_stock_is_rejected_and_nothing_is_written()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("Chicles", 5m);
        await h.StockAsync(p, a, 3, 2m);
        await h.OpenCashAsync();

        await Assert.ThrowsAsync<DomainException>(() => h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"), Lines = { new SaleLineRequest { ProductId = p, Quantity = 4, OwnerId = a } }
        }));
        h.NewScope();
        Assert.Equal(0, await h.Db.Sales.CountAsync());
        Assert.Equal(3, await h.Get<InventoryService>().GetStockAsync(p));
        Assert.Equal(0, await h.Db.InventoryMovements.CountAsync(m => m.Type == MovementType.Sale));
    }

    [Fact]
    public async Task Manager_can_authorize_a_sale_without_stock_and_it_is_audited()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("Hielo", 25m, 15m);
        await h.OpenCashAsync();
        await h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"), AuthorizeNoStock = true,
            Lines = { new SaleLineRequest { ProductId = p, Quantity = 2, OwnerId = a } }
        });
        h.NewScope();
        Assert.Equal(0, await h.Get<InventoryService>().GetStockAsync(p));   // never negative
        Assert.True(await h.Db.AuditLogs.AnyAsync(x => x.Action == "Venta sin existencia autorizada"));
    }

    [Fact]
    public async Task Cashier_cannot_discount_or_cancel()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("Agua", 10m);
        await h.StockAsync(p, a, 5, 5m);
        await h.OpenCashAsync();
        h.User.Role = UserRole.Cashier;
        await Assert.ThrowsAsync<DomainException>(() => h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"), DiscountCents = 100, Lines = { new SaleLineRequest { ProductId = p, Quantity = 1 } }
        }));
        int id = await h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"), Lines = { new SaleLineRequest { ProductId = p, Quantity = 1 } }
        });
        await Assert.ThrowsAsync<DomainException>(() => h.Get<SalesService>().CancelSaleAsync(id, "error"));
    }

    [Fact]
    public async Task Ticket_discount_is_spread_over_owners_and_totals_match()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A"), b = await h.OwnerAsync("B");
        int p1 = await h.ProductAsync("P1", 10m), p2 = await h.ProductAsync("P2", 30m);
        await h.StockAsync(p1, a, 10, 5m);
        await h.StockAsync(p2, b, 10, 20m);
        await h.OpenCashAsync();
        int id = await h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Tarjeta"), DiscountCents = 1000,
            Lines = { new SaleLineRequest { ProductId = p1, Quantity = 1 }, new SaleLineRequest { ProductId = p2, Quantity = 1 } }
        });
        h.NewScope();
        var s = (await h.Get<SalesService>().GetSaleAsync(id))!;
        Assert.Equal(4000, s.SubtotalCents);
        Assert.Equal(1000, s.DiscountCents);
        Assert.Equal(3000, s.TotalCents);
        Assert.Equal(s.TotalCents, s.Items.Sum(i => i.LineTotalCents));
        Assert.Equal(250, s.Items.Single(i => i.OwnerId == a).DiscountCents);   // 1/4 of the ticket
    }

    [Fact]
    public async Task Cannot_sell_with_closed_register()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("X", 1m);
        await h.StockAsync(p, a, 1, 1m);
        await Assert.ThrowsAsync<DomainException>(() => h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"), Lines = { new SaleLineRequest { ProductId = p, Quantity = 1 } }
        }));
    }

    [Fact]
    public async Task Cancelling_a_sale_returns_stock_to_the_same_lots()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("Jugo", 15m);
        await h.StockAsync(p, a, 4, 9m);
        await h.OpenCashAsync();
        int id = await h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"), Lines = { new SaleLineRequest { ProductId = p, Quantity = 3 } }
        });
        h.NewScope();
        await h.Get<SalesService>().CancelSaleAsync(id, "Cliente se arrepintió");
        h.NewScope();
        Assert.Equal(4, await h.Get<InventoryService>().GetStockAsync(p));
        var sale = (await h.Get<SalesService>().GetSaleAsync(id))!;
        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.Equal(1, await h.Db.InventoryMovements.CountAsync(m => m.Type == MovementType.SaleCancellation && m.SaleId == id));
        await Assert.ThrowsAsync<DomainException>(() => h.Get<SalesService>().CancelSaleAsync(id, "otra vez"));
    }

    [Fact]
    public async Task Kilogram_products_sell_by_grams()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("Queso", 180m, 120m, unit: UnitType.Kilogram);
        await h.StockAsync(p, a, 2000, 120m);    // 2 kg
        await h.OpenCashAsync();
        int id = await h.Get<SalesService>().CompleteSaleAsync(new SaleRequest
        {
            PaymentMethodId = h.MethodId("Efectivo"), Lines = { new SaleLineRequest { ProductId = p, Quantity = 350 } }
        });
        h.NewScope();
        var s = (await h.Get<SalesService>().GetSaleAsync(id))!;
        Assert.Equal(6300, s.TotalCents);   // 0.35 × 180
        Assert.Equal(4200, s.CostCents);    // 0.35 × 120
        Assert.Equal(1650, await h.Get<InventoryService>().GetStockAsync(p));
    }
}

public class CashRegisterTests
{
    [Fact]
    public async Task Expected_cash_excludes_card_sales_and_applies_movements()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("P", 100m);
        await h.StockAsync(p, a, 10, 60m);
        int day = await h.OpenCashAsync(500m);
        var sales = h.Get<SalesService>();
        await sales.CompleteSaleAsync(new SaleRequest { PaymentMethodId = h.MethodId("Efectivo"), CashReceivedCents = 20000, Lines = { new SaleLineRequest { ProductId = p, Quantity = 1 } } });
        await sales.CompleteSaleAsync(new SaleRequest { PaymentMethodId = h.MethodId("Tarjeta"), Lines = { new SaleLineRequest { ProductId = p, Quantity = 2 } } });
        var cash = h.Get<CashService>();
        await cash.AddMovementAsync(CashMovementType.Expense, 3000, true, "Garrafón");
        await cash.AddMovementAsync(CashMovementType.Expense, 1000, false, "Pago con tarjeta");
        await cash.AddMovementAsync(CashMovementType.Withdrawal, 5000, true, "Retiro");
        await cash.AddMovementAsync(CashMovementType.Deposit, 2000, true, "Cambio");
        h.NewScope();

        var s = await h.Get<CashService>().GetSummaryAsync(day);
        Assert.Equal(30000, s.TotalSalesCents);
        Assert.Equal(10000, s.CashSalesCents);
        Assert.Equal(4000, s.ExpensesCents);
        // 500 + 100 + 20 − 50 − 30 = 540
        Assert.Equal(54000, s.ExpectedCashCents);
        Assert.Equal(30000 - 18000, s.GrossProfitCents);
    }

    [Fact]
    public async Task Closing_with_a_difference_requires_a_note_and_freezes_totals()
    {
        await using var h = await TestHost.CreateAsync();
        int day = await h.OpenCashAsync(100m);
        var cash = h.Get<CashService>();
        await Assert.ThrowsAsync<DomainException>(() => cash.CloseAsync(day, 9000, null));
        h.NewScope();
        await h.Get<CashService>().CloseAsync(day, 9000, "Faltaron $10, se dio mal un cambio");
        h.NewScope();
        var s = await h.Get<CashService>().GetSummaryAsync(day);
        Assert.Equal(ClosingStatus.Closed, s.Closing.Status);
        Assert.Equal(-1000, s.Closing.DifferenceCents);
        await Assert.ThrowsAsync<DomainException>(() => h.Get<CashService>().CloseAsync(day, 10000, null));
    }

    [Fact]
    public async Task Cancelling_a_cash_sale_from_a_closed_day_refunds_from_todays_drawer()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("P", 50m);
        await h.StockAsync(p, a, 5, 30m);
        int day1 = await h.OpenCashAsync();
        int saleId = await h.Get<SalesService>().CompleteSaleAsync(new SaleRequest { PaymentMethodId = h.MethodId("Efectivo"), Lines = { new SaleLineRequest { ProductId = p, Quantity = 1 } } });
        await h.Get<CashService>().CloseAsync(day1, 5000, null);
        h.NewScope();
        h.Clock.Now = h.Clock.Now.AddDays(1);
        int day2 = await h.OpenCashAsync(200m);
        await h.Get<SalesService>().CancelSaleAsync(saleId, "Producto en mal estado");
        h.NewScope();
        var s = await h.Get<CashService>().GetSummaryAsync(day2);
        Assert.Equal(5000, s.RefundsCents);
        Assert.Equal(20000 - 5000, s.ExpectedCashCents);
        var closed = await h.Get<CashService>().GetSummaryAsync(day1);
        Assert.Equal(5000, closed.TotalSalesCents);   // the frozen closing is not rewritten
    }
}

public class InventoryAndPurchasingTests
{
    [Fact]
    public async Task Every_stock_change_writes_a_movement()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("P", 10m, 6m);
        var inv = h.Get<InventoryService>();
        await inv.AddStockAsync(p, a, 10, 600, MovementType.InitialStock, null);
        await inv.RemoveStockAsync(p, a, 2, MovementType.Damaged, "Latas golpeadas");
        await inv.AdjustToCountAsync(p, a, 7, "Conteo mensual");
        h.NewScope();
        Assert.Equal(7, await h.Get<InventoryService>().GetStockAsync(p));
        var sum = await h.Db.InventoryMovements.Where(m => m.ProductId == p).SumAsync(m => m.Quantity);
        Assert.Equal(7, sum);
        await Assert.ThrowsAsync<DomainException>(() => h.Get<InventoryService>().RemoveStockAsync(p, a, 1, MovementType.Damaged, ""));
    }

    [Fact]
    public async Task Transfer_moves_stock_between_owners_at_agreed_price()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("Mamá"), b = await h.OwnerAsync("Yo");
        int p = await h.ProductAsync("Sabritas", 20m);
        await h.StockAsync(p, a, 10, 12m);
        await h.Get<InventoryService>().TransferAsync(p, a, b, 4, 1300, null);
        h.NewScope();
        var inv = h.Get<InventoryService>();
        Assert.Equal(6, await inv.GetStockAsync(p, a));
        Assert.Equal(4, await inv.GetStockAsync(p, b));
        Assert.Equal(1300, (await h.Db.InventoryLots.SingleAsync(l => l.OwnerId == b)).UnitCostCents);
    }

    [Fact]
    public async Task Delivery_creates_lots_per_owner_and_marks_requests_stocked()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A"), b = await h.OwnerAsync("B");
        int s = await h.Get<SupplierService>().SaveAsync(null, "Bimbo", null, null, null, true, new[] { DayOfWeek.Monday });
        int p = await h.ProductAsync("Gansito", 18m, 11m, barcode: "123", supplierId: s);
        await h.Get<CustomerRequestService>().AddAsync("Gansito", "123", null, 2, null);
        int id = await h.Get<DeliveryService>().RegisterAsync(new DeliveryInput
        {
            SupplierId = s, UpdateReferenceCost = true,
            Items = { new DeliveryLineInput { ProductId = p, OwnerId = a, Quantity = 10, UnitCostCents = 1150 },
                      new DeliveryLineInput { ProductId = p, OwnerId = b, Quantity = 5, UnitCostCents = 1150 } }
        });
        h.NewScope();
        Assert.Equal(15, await h.Get<InventoryService>().GetStockAsync(p));
        Assert.Equal(2, await h.Db.InventoryLots.CountAsync(l => l.DeliveryItemId != null));
        Assert.Equal(1150, (await h.Db.Products.FindAsync(p))!.PurchasePriceCents);
        Assert.Equal(RequestStatus.Stocked, (await h.Db.CustomerRequests.SingleAsync()).Status);
        Assert.Equal(17250, (await h.Db.Deliveries.FindAsync(id))!.TotalCostCents);
    }

    [Fact]
    public async Task Customer_requests_are_aggregated_by_name()
    {
        await using var h = await TestHost.CreateAsync();
        var svc = h.Get<CustomerRequestService>();
        await svc.AddAsync("Squirt 2 L", null, null, 1, null);
        await svc.AddAsync("  squirt  2 l ", null, null, 2, "otra clienta");
        h.NewScope();
        var r = await h.Db.CustomerRequests.SingleAsync();
        Assert.Equal(2, r.RequestCount);
        Assert.Equal(3, r.TotalQuantity);
        Assert.Equal(2, await h.Db.CustomerRequestEvents.CountAsync());
    }

    [Fact]
    public void Reorder_quantity_matches_the_documented_example()
    {
        // stock 5, 4/day, next delivery in 3 days, weekly cycle, safety stock 5 (min 5, 0 safety days)
        Assert.Equal(33, StockingService.SuggestQuantity(5, 4, 3, 7, 5, 0));
        // Plenty of stock: nothing to buy
        Assert.Equal(0, StockingService.SuggestQuantity(100, 2, 2, 7, 5, 2));
        // No sales history: only top up to the minimum
        Assert.Equal(4, StockingService.SuggestQuantity(1, 0, 3, 7, 5, 2));
    }

    [Fact]
    public async Task Stocking_list_does_not_touch_inventory_until_received()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int s = await h.Get<SupplierService>().SaveAsync(null, "Coca-Cola", null, null, null, true, new[] { DayOfWeek.Tuesday });
        int p = await h.ProductAsync("Coca 600", 18m, 12m, minStock: 12, supplierId: s, priorityOwner: a);
        var stocking = h.Get<StockingService>();
        int listId = await stocking.GenerateListAsync(null, StockingGroupBy.Supplier, null, null, false);
        h.NewScope();
        Assert.Equal(0, await h.Get<InventoryService>().GetStockAsync(p));
        var list = (await h.Get<StockingService>().GetAsync(listId))!;
        var item = list.Items.Single();
        Assert.Equal(12, item.SuggestedQty);
        await h.Get<StockingService>().ReceiveAsync(listId, new[] { new ReceiveLine { ItemId = item.Id, Quantity = 10, UnitCostCents = 1250 } }, "F-1", false);
        h.NewScope();
        Assert.Equal(10, await h.Get<InventoryService>().GetStockAsync(p, a));
        Assert.Equal(StockingListStatus.Received, (await h.Get<StockingService>().GetAsync(listId))!.Status);
    }

    [Fact]
    public void Seasonality_needs_twelve_full_months()
    {
        var monthly = new Dictionary<int, long>();
        var first = new DateOnly(2025, 6, 1);
        var (ok, _) = TrendService.Seasonality(monthly, first, new DateOnly(2026, 1, 15));
        Assert.False(ok);

        for (var m = new DateOnly(2024, 11, 1); m < new DateOnly(2026, 11, 1); m = m.AddMonths(1))
            monthly[m.Year * 100 + m.Month] = m.Month is 1 or 2 ? 50 : m.Month == 12 ? 200 : 100;
        var (ok2, idx) = TrendService.Seasonality(monthly, new DateOnly(2024, 11, 1), new DateOnly(2026, 11, 16));
        Assert.True(ok2);
        Assert.True(idx[0] < 0.8);   // January sells less
        Assert.True(idx[11] > 1.2);  // December sells more
    }
}

public class DeletionTests
{
    [Fact]
    public async Task Unused_catalog_records_can_be_deleted()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int s = await h.Get<SupplierService>().SaveAsync(null, "Bimbo", null, null, null, true, new[] { DayOfWeek.Monday });
        int p = await h.ProductAsync("Prueba", 10m, supplierId: s, priorityOwner: a);
        var del = h.Get<DeletionService>();
        await del.DeleteProductAsync(p);
        await del.DeleteOwnerAsync(a);
        await del.DeleteSupplierAsync(s);
        h.NewScope();
        Assert.False(await h.Db.Products.AnyAsync());
        Assert.False(await h.Db.Owners.AnyAsync());
        Assert.False(await h.Db.SupplierSchedules.AnyAsync());
    }

    [Fact]
    public async Task Records_with_sales_or_stock_cannot_be_deleted()
    {
        await using var h = await TestHost.CreateAsync();
        int a = await h.OwnerAsync("A");
        int p = await h.ProductAsync("Con historial", 10m);
        await h.StockAsync(p, a, 5, 6m);
        var del = h.Get<DeletionService>();
        await Assert.ThrowsAsync<DomainException>(() => del.DeleteProductAsync(p));
        await Assert.ThrowsAsync<DomainException>(() => del.DeleteOwnerAsync(a));
        h.NewScope();
        Assert.Equal(5, await h.Get<InventoryService>().GetStockAsync(p));
    }

    [Fact]
    public async Task Deleting_a_category_leaves_products_without_category()
    {
        await using var h = await TestHost.CreateAsync();
        var cat = await h.Db.Categories.FirstAsync();
        int p = await h.Get<ProductService>().SaveAsync(new ProductInput { Name = "X", SalePriceCents = 100, CategoryId = cat.Id });
        await h.Get<DeletionService>().DeleteCategoryAsync(cat.Id);
        h.NewScope();
        Assert.Null((await h.Db.Products.FindAsync(p))!.CategoryId);
    }
}
