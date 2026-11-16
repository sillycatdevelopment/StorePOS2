# StorePOS 2.0 — Architecture (Phase 1)

## 1. Review of the previous StorePOS

The previous version (`sillycatdevelopment/StorePOS`) is a .NET 10 **console** app that stores
everything in a single JSON file (`InventoryData`: owners, products, movements).

Ideas kept (they were good):

| Old idea | How it lives on in 2.0 |
|---|---|
| Stock is derived from **movements**, never edited directly | `InventoryMovements` table; every change writes one. Lots carry the running balance for speed. |
| Each movement belongs to an **owner** | Every lot, movement and sale line has `OwnerId`. Owners are never merged. |
| Products sold **by piece or by kilogram** | `Product.Unit` (Piece / Kilogram). Quantities are integers in *base units*: pieces, or grams. |
| **Priority owner** per product | `Product.PriorityOwnerId`, used by the *Automatic* allocation mode. |
| Integer-unit allocation to avoid losing cents | All money is integer **centavos**, all quantities integer base units. |
| **Transfers** between owners (one owner buys another's stock) | `TransferOut` / `TransferIn` movements with an agreed price, creating a new lot for the buyer. |
| Accent/case-insensitive product search | `Product.SearchName` (normalized, indexed) queried with `LIKE`. |
| Atomic save (temp file + move) | Replaced by real database transactions. |
| Historical sales keep their price | Sale lines snapshot name, price, cost and owner. |

Things replaced:

* JSON storage → SQLite + EF Core, normalized schema with FKs and indexes.
* Weighted-average cost recomputed from the full history on every sale (O(n) per sale) →
  **inventory lots** with their own cost, consumed FIFO inside each owner.
* Silent proportional split between owners → configurable allocation (manual / oldest first /
  automatic), with manual being the default so owners are never mixed silently.
* Console UI → browser UI (Razor Pages, vanilla JS), reachable on the LAN with authentication.

## 2. Solution layout

```
StorePos2.slnx
src/StorePos.Core/          class library — no UI
  Models/                   entities + enums
  Data/                     StoreDbContext, EF configuration, seeding
  Services/                 business logic (inventory, sales, cash, suppliers, stocking, ...)
  Reporting/                reports, trend analysis, Excel exports (ClosedXML)
  Utilities/                Money, Qty, TextNorm, clock, write gate
  ViewModels/               DTOs returned by services to pages
src/StorePos.Web/           ASP.NET Core Razor Pages app (Program.cs, Pages/, wwwroot/)
tests/StorePos.Tests/       xUnit tests on an in-memory SQLite database
```

Dependencies (deliberately few): `Microsoft.EntityFrameworkCore.Sqlite`,
`Microsoft.EntityFrameworkCore.Design` (dev-time only, for migrations), `ClosedXML`.
Password hashing uses ASP.NET Core's built-in `PasswordHasher<T>`. No Node.js, no JS framework.

## 3. Precision strategy

* **Money**: `long` columns named `...Cents` (centavos). `$18.50` → `1850`. Exact, sortable and
  summable by SQLite (`SUM` on INTEGER), no floating point. Formatted as MXN (`es-MX`) only in the UI.
* **Quantities**: `long` in base units. Piece products: 1 = one piece. Kilogram products: 1 = one gram.
  Prices/costs are per piece or per kilogram, so `line = round(price × qty / scale)` with
  `scale = 1` or `1000` (banker's rounding is *not* used; `MidpointRounding.AwayFromZero`).
* **Dates**: stored as local store time (America/Mexico_City) plus a `BusinessDate` (DateOnly)
  for fast grouping; sale lines also store `YearMonth` (e.g. `202611`) for seasonality queries.

## 4. Database schema

```
Categories(Id, Name UNIQUE, IsActive)
Owners(Id, Name, Contact, Notes, IsActive, CreatedAt)
Suppliers(Id, Name, Contact, Phone, Notes, IsActive)
SupplierSchedules(Id, SupplierId→Suppliers, Day)              UNIQUE(SupplierId, Day)
Products(Id, Name, SearchName, Barcode UNIQUE NULL, CategoryId→, Description, Unit,
         PurchasePriceCents, SalePriceCents, MinStock, PreferredSupplierId→, PriorityOwnerId→,
         IsActive, CreatedAt, UpdatedAt)                       IX(SearchName), IX(CategoryId)
InventoryLots(Id, ProductId→, OwnerId→, SupplierId→, DeliveryItemId→, UnitCostCents,
              QtyReceived, QtyRemaining, ReceivedAt, Source)   IX(ProductId, OwnerId, QtyRemaining)
InventoryMovements(Id, ProductId→, OwnerId→, LotId→, Type, Quantity(signed), UnitCostCents,
                   UnitPriceCents, Reason, SaleId→, DeliveryId→, UserId→, CreatedAt)
                                                               IX(ProductId, CreatedAt), IX(OwnerId, CreatedAt)
PaymentMethods(Id, Name UNIQUE, IsCash, IsActive, SortOrder)
CashRegisters(Id, Name, IsActive)
DailyClosings(Id, CashRegisterId→, BusinessDate, Status, OpenedAt, OpenedByUserId→,
              OpeningCashCents, ClosedAt, ClosedByUserId→, totals..., Expected/Counted/Difference,
              Notes)                                           IX(CashRegisterId, Status)
DailyClosingPayments(Id, DailyClosingId→, PaymentMethodId→, MethodName, IsCash, AmountCents, SaleCount)
CashMovements(Id, DailyClosingId→, Type, AmountCents, AffectsCash, Description, SaleId→, UserId→, CreatedAt)
Sales(Id, CreatedAt, BusinessDate, UserId→, DailyClosingId→, PaymentMethodId→, PaymentMethodName,
      PaymentIsCash, SubtotalCents, DiscountCents, TotalCents, CostCents, ProfitCents,
      CashReceivedCents, ChangeCents, Status, CancelledAt, CancelledByUserId→, CancelReason, Notes)
                                                               IX(BusinessDate), IX(Status)
SaleItems(Id, SaleId→, ProductId→, OwnerId→, snapshots(ProductName, Barcode, CategoryId, CategoryName,
          SupplierId, OwnerName, Unit), Quantity, UnitPriceCents, DiscountCents, LineTotalCents,
          UnitCostCents, CostCents, BusinessDate, YearMonth)   IX(BusinessDate, ProductId), IX(ProductId, YearMonth), IX(OwnerId)
SaleItemLots(Id, SaleItemId→, LotId→, Quantity, UnitCostCents)
Deliveries(Id, SupplierId→, ReceivedAt, InvoiceNumber, Notes, TotalCostCents, StockingListId→, UserId→)
DeliveryItems(Id, DeliveryId→, ProductId→, OwnerId→, Quantity, UnitCostCents, LineCostCents)
StockingLists(Id, Name, Status, IsAutoGenerated, GroupBy, Notes, CreatedAt, CreatedByUserId→,
              PurchasedAt, ReceivedAt)
StockingListItems(Id, StockingListId→, ProductId→ NULL, CustomerRequestId→ NULL, Description,
                  SupplierId→, OwnerId→, CurrentStock, AvgDailySales, DaysToNextDelivery,
                  SuggestedQty, Quantity, EstimatedUnitCostCents, Reason)
CustomerRequests(Id, ProductName, NormalizedName, Barcode, CategoryId→, ProductId→, Status,
                 RequestCount, TotalQuantity, FirstRequestedAt, LastRequestedAt, Notes)
CustomerRequestEvents(Id, CustomerRequestId→, RequestedAt, Quantity, Notes, UserId→)
Users(Id, Username UNIQUE NOCASE, DisplayName, PasswordHash, Role, IsActive, CreatedAt, LastLoginAt)
AuditLogs(Id, CreatedAt, UserId→, UserName, Action, EntityType, EntityId, Details)  IX(CreatedAt)
AppSettings(Key PK, Value)
```

All FKs use `ON DELETE RESTRICT`, so nothing financial can be orphaned or cascaded away.
Nothing financial is ever deleted: products, owners, suppliers, users and payment methods are
deactivated (`IsActive = false`); sales are **cancelled**, not deleted.

## 5. Key business rules

**Ownership and lots.** Every unit of stock is in a lot `(product, owner, unit cost, remaining)`.
Stock of an owner = `SUM(QtyRemaining)` of their lots. Selling consumes that owner's lots oldest
first (FIFO). Each sale line is one `(product, owner)` pair and records the exact lots consumed
(`SaleItemLots`) so cost and profit are exact and cancellations put stock back into the same lots.

**Allocation modes** (setting `AllocationMode`):
* `Manual` (default): if more than one owner has stock, the cashier must pick the owner in the POS.
  With only one owner in stock, that owner is used automatically (no ambiguity, nothing is mixed).
* `OldestFirst`: the owner whose oldest lot is oldest is used first; can span owners, one line per owner.
* `Automatic`: the product's priority owner first, then the owner with the most stock.

**Negative stock** is rejected. A Manager/Administrator can authorize selling without stock in the
POS; this creates an audited `Correction` lot at the product's reference cost for the chosen owner,
so lots never go negative and the event stays visible.

**Sales** run inside one database transaction (plus an in-process write gate): validate cash
session, re-read prices from the DB (the browser is never trusted for prices or costs), allocate,
consume lots, write sale, lines, lot links and movements, commit. Any failure rolls everything back.

**Cash.** One open `DailyClosing` per register. Expected cash =
`opening + cash sales + deposits + other-in − withdrawals − cash expenses − refunds − other-out`.
Card/transfer sales never count as cash. A difference ≠ 0 requires a note. A closed day is frozen
(totals stored, per-method totals stored in `DailyClosingPayments`). Cancelling a sale from the open
day simply removes it from the totals; cancelling a sale from a closed day records a cash refund in
the current day (only if it was paid in cash).

**Reorder suggestion** (per product):
```
avgDaily     = units sold in last 28 days / days with history (max 28) × seasonal factor
daysToNext   = days until the preferred supplier's next scheduled delivery (default 7 if none)
cycle        = average days between that supplier's deliveries (7 if none)
safety       = max(MinStock, ceil(avgDaily × SafetyDays))
atArrival    = max(0, stock − avgDaily × daysToNext)
suggested    = max(0, ceil(avgDaily × cycle + safety − atArrival))
```
Example (stock 5, 4/day, next delivery in 3 days, weekly cycle, safety 5): stock runs out in
~1.3 days, before the delivery → order `ceil(4×7 + 5 − 0) = 33`.
Customer requests add priority and, for products without sales history, their requested quantity.

**Trends.** Last 28 days vs. previous 28 days. Under 14 days of history or under 5 units the result
is labelled *“Datos insuficientes”* instead of showing a misleading percentage. Seasonal factors
need at least 12 months of history; otherwise they are 1.0 and labelled as unavailable.

**Backups** use SQLite's online backup API (`SqliteConnection.BackupDatabase`), safe while the app
is in use (WAL mode). Restore validates the file (`PRAGMA integrity_check`), takes a safety backup
of the current database first, then copies the backup into the live database with the same API.

## 6. Performance choices for an AMD A6 / 4 GB / HDD

* SQLite in WAL mode, `synchronous=NORMAL`, one file, no server.
* Server-rendered pages, ~8 KB of vanilla JS for the POS, one CSS file, no frameworks or web fonts.
* Pagination (50 rows) on products, inventory, sales, movements, audit.
* `AsNoTracking()` for reads; aggregates computed in SQL (`SUM` on integers); denormalized
  `BusinessDate`/`YearMonth` on sale lines to avoid full scans for reports.
* The only background work is a timer that checks once an hour whether an automatic backup is due.
