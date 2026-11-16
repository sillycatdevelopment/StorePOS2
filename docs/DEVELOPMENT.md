# StorePOS 2.0 — Developer guide

Punto de venta e inventario para una tienda pequeña con **varios dueños de mercancía**.
ASP.NET Core 10 (Razor Pages) + SQLite (EF Core) + ClosedXML. Sin Node.js, sin servidor de base
de datos, sin frameworks de JavaScript. Pensado para un AMD A6 con 4 GB de RAM.

## Build, test and publish

Requires the **.NET 10 SDK** on the development machine (not on the store PC).

```powershell
.\scripts\setup.ps1          # Windows
./scripts/setup.sh           # macOS / Linux
```

The script:
1. restores packages and the `dotnet-ef` local tool,
2. creates the initial EF Core migration (`src/StorePos.Core/Migrations`) if it does not exist,
3. runs the automated tests,
4. publishes a self-contained Windows x64 build into `publish/` (the store PC needs no .NET install).

Run it once **before the first real use**: the app uses migrations when they exist and only falls back
to `EnsureCreated` otherwise. Commit the generated `Migrations` folder. For later schema changes:
`dotnet ef migrations add <Name> --project src/StorePos.Core --startup-project src/StorePos.Web`.

To try it on the dev machine: `dotnet run --project src/StorePos.Web` → http://localhost:5000

## Using it in the store

1. Copy `publish/` to the store PC (e.g. `C:\StorePOS`). Open `Iniciar-StorePOS.bat`.
2. The first visit asks you to create the administrator, the store name and the first owner.
3. Other devices on the same network open `http://<IP-of-the-PC>:5000`. Allow port 5000 in the Windows
   firewall (*Windows Defender Firewall → Advanced → Inbound rule → Port → TCP 5000*).
   Everyone must sign in.
4. The database is `publish/data/storepos.db`. Backups go to `publish/data/backups/` unless you pick
   another folder in *Configuración → Respaldos*. Copy backups to a USB drive or the cloud regularly.

Settings in `appsettings.json`: `StorePos:DatabasePath`, `StorePos:Urls` (default `http://0.0.0.0:5000`).

## Roles

| | Cajero | Encargado | Administrador |
|---|:-:|:-:|:-:|
| Vender, abrir/cerrar caja, gastos/depósitos, solicitudes | ✓ | ✓ | ✓ |
| Descuentos, cancelar ventas, retiros de caja, venta sin existencia | | ✓ | ✓ |
| Productos, inventario, dueños, proveedores, surtido, reportes, Excel | | ✓ | ✓ |
| Usuarios, configuración, respaldos/restauración, bitácora | | | ✓ |

## What was built, by phase

| Phase | Delivered |
|---|---|
| 1 | Review of StorePOS 1 + architecture and schema: [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| 2 | Solution (Core / Web / Tests), SQLite + EF Core, FK/indexes/check constraints, layout, products, categories, owners |
| 3 | Inventory lots per owner, FIFO inside each owner, movement journal, adjustments, physical counts, damage, transfers between owners, filters, stock status |
| 4 | POS: barcode scanner (keyboard wedge), name search, cart, owner selection, discounts (managers), cash/card/other, change, transactional sale, cancellation with audit, printable receipt |
| 5 | Cash day: opening float, cash movements, live expected cash, closing with count/difference/note, frozen history, refunds for sales from closed days |
| 6 | Suppliers with weekday schedules, upcoming deliveries, delivery registration (supplier + owner + cost), stocking lists (auto/manual, grouped, edit, purchased, received → inventory) |
| 7 | Reports by day/week/month/year, owner, category, supplier, payment method; best/worst sellers; trends, weekday profile, seasonality (12-month minimum), recommendations; customer requests with aggregation |
| 8 | 9 Excel exports with filters; online SQLite backups, automatic backups, restore with validation + safety backup; roles; audit log |
| 9 | 30 xUnit tests on a real in-memory SQLite database (see below) |

## Tests (`tests/StorePos.Tests`)

Ownership separation (the Coca-Cola example), manual mode never mixing owners, FIFO cost, automatic
allocation with priority owner, rejection of insufficient stock with full rollback, authorized
no-stock sales, permissions, ticket discounts split per owner, closed register, cancellation back to
the same lots, kilogram sales, expected cash (card excluded), closing difference requires note,
refunds for sales from closed days, movement journal integrity, transfers, deliveries, request
aggregation, the documented reorder formula (stock 5, 4/day, next delivery 3 days → 33), stocking
lists not touching inventory until received, and seasonality thresholds.

## Releasing updates (GitHub) and the automatic updater

**Release** — push a version tag and GitHub builds, tests and publishes the Windows zip by itself
(`.github/workflows/release.yml`, runs on GitHub's Windows machines, nothing to install):
```
git add . && git commit -m "describe the change" && git push
git tag v2.0.3 && git push origin v2.0.3
```
The workflow fails (and nothing is released) if tests fail or if the data model changed without a
migration. When you change anything in `src/StorePos.Core/Models` or the DbContext, first run
`dotnet ef migrations add <Name> --project src/StorePos.Core --startup-project src/StorePos.Web` and commit it.

**Update on the store PC** — `Iniciar-StorePOS.bat` checks GitHub every time it starts and installs a newer
release automatically; `Actualizar-StorePOS.bat` does the same on demand (asking first). The updater
downloads and checks the package before closing anything, backs up `data\storepos.db` to
`respaldo_actualizacion\`, and never touches `data\`, `appsettings.json` or the backups. New database
changes are applied by EF migrations when StorePOS starts. Without internet it simply starts the
installed version. For a private repository put a read-only token in `$Token` inside `Actualizar-StorePOS.ps1`.

## Deleting test data

*Configuración → Borrar datos* (administrators) offers two resets, each preceded by an automatic backup:
**Borrar operaciones** clears sales, cash closings, stock, deliveries, lists, requests and the audit log
but keeps the catalog, owners, suppliers and users; **Borrar todo** returns to a fresh install.
Single records (products, owners, suppliers, categories, users, payment methods, stocking lists,
customer requests) have an *Eliminar* button; records that already have sales or stock must be
deactivated instead, so totals never break.

## Known limitations / next steps

* **Partial returns** of individual items are recorded as *Devolución* stock entries from the product
  page; there is no "return line X of ticket Y" flow yet (full ticket cancellation is implemented).
* One sale = one payment method (no split payment cash + card).
* One cash register is used by the POS (the schema supports several).
* Receipt printing uses the browser print dialog (works with 58/80 mm thermal printers set as the
  default printer); no direct ESC/POS driver.
* HTTP only on the LAN. If the app will be reachable from outside the store, put it behind HTTPS.
