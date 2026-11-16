using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorePos.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashRegisters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashRegisters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Owners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Contact = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Owners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    IsCash = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Contact = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    SearchName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Unit = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchasePriceCents = table.Column<long>(type: "INTEGER", nullable: false),
                    SalePriceCents = table.Column<long>(type: "INTEGER", nullable: false),
                    MinStock = table.Column<long>(type: "INTEGER", nullable: false),
                    PreferredSupplierId = table.Column<int>(type: "INTEGER", nullable: true),
                    PriorityOwnerId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Product_MinStock", "MinStock >= 0");
                    table.CheckConstraint("CK_Product_Prices", "PurchasePriceCents >= 0 AND SalePriceCents >= 0");
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Owners_PriorityOwnerId",
                        column: x => x.PriorityOwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Suppliers_PreferredSupplierId",
                        column: x => x.PreferredSupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierSchedules_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyClosings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CashRegisterId = table.Column<int>(type: "INTEGER", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OpenedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    OpeningCashCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    SaleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalSalesCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CashSalesCents = table.Column<long>(type: "INTEGER", nullable: false),
                    NonCashSalesCents = table.Column<long>(type: "INTEGER", nullable: false),
                    DepositsCents = table.Column<long>(type: "INTEGER", nullable: false),
                    WithdrawalsCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpensesCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CashExpensesCents = table.Column<long>(type: "INTEGER", nullable: false),
                    RefundsCents = table.Column<long>(type: "INTEGER", nullable: false),
                    OtherInCents = table.Column<long>(type: "INTEGER", nullable: false),
                    OtherOutCents = table.Column<long>(type: "INTEGER", nullable: false),
                    GrossProfitCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpectedCashCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CountedCashCents = table.Column<long>(type: "INTEGER", nullable: false),
                    DifferenceCents = table.Column<long>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyClosings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyClosings_CashRegisters_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyClosings_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyClosings_Users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockingLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAutoGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    GroupBy = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    PurchasedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockingLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockingLists_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalQuantity = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstRequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastRequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerRequests_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerRequests_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyClosingId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountCents = table.Column<long>(type: "INTEGER", nullable: false),
                    AffectsCash = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.CheckConstraint("CK_CashMovement_Amount", "AmountCents > 0");
                    table.ForeignKey(
                        name: "FK_CashMovements_DailyClosings_DailyClosingId",
                        column: x => x.DailyClosingId,
                        principalTable: "DailyClosings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashMovements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyClosingPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyClosingId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentMethodId = table.Column<int>(type: "INTEGER", nullable: false),
                    MethodName = table.Column<string>(type: "TEXT", nullable: false),
                    IsCash = table.Column<bool>(type: "INTEGER", nullable: false),
                    AmountCents = table.Column<long>(type: "INTEGER", nullable: false),
                    SaleCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyClosingPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyClosingPayments_DailyClosings_DailyClosingId",
                        column: x => x.DailyClosingId,
                        principalTable: "DailyClosings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyClosingPayments_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    DailyClosingId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentMethodId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentMethodName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    PaymentIsCash = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubtotalCents = table.Column<long>(type: "INTEGER", nullable: false),
                    DiscountCents = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CostCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ProfitCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CashReceivedCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ChangeCents = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CancelReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.CheckConstraint("CK_Sale_Total", "TotalCents >= 0 AND DiscountCents >= 0");
                    table.ForeignKey(
                        name: "FK_Sales_DailyClosings_DailyClosingId",
                        column: x => x.DailyClosingId,
                        principalTable: "DailyClosings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sales_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sales_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sales_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TotalCostCents = table.Column<long>(type: "INTEGER", nullable: false),
                    StockingListId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliveries_StockingLists_StockingListId",
                        column: x => x.StockingListId,
                        principalTable: "StockingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Deliveries_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Deliveries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerRequestEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRequestEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerRequestEvents_CustomerRequests_CustomerRequestId",
                        column: x => x.CustomerRequestId,
                        principalTable: "CustomerRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockingListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StockingListId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomerRequestId = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentStock = table.Column<long>(type: "INTEGER", nullable: false),
                    AvgDailySales = table.Column<double>(type: "REAL", nullable: false),
                    DaysToNextDelivery = table.Column<int>(type: "INTEGER", nullable: true),
                    SuggestedQty = table.Column<long>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    EstimatedUnitCostCents = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockingListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockingListItems_CustomerRequests_CustomerRequestId",
                        column: x => x.CustomerRequestId,
                        principalTable: "CustomerRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockingListItems_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockingListItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockingListItems_StockingLists_StockingListId",
                        column: x => x.StockingListId,
                        principalTable: "StockingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockingListItems_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    CategoryName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Unit = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitPriceCents = table.Column<long>(type: "INTEGER", nullable: false),
                    DiscountCents = table.Column<long>(type: "INTEGER", nullable: false),
                    LineTotalCents = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitCostCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CostCents = table.Column<long>(type: "INTEGER", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    YearMonth = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleItems", x => x.Id);
                    table.CheckConstraint("CK_SaleItem_Qty", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_SaleItems_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleItems_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeliveryId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitCostCents = table.Column<long>(type: "INTEGER", nullable: false),
                    LineCostCents = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryItems", x => x.Id);
                    table.CheckConstraint("CK_DeliveryItem_Qty", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_DeliveryItems_Deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "Deliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryItems_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: true),
                    DeliveryItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    UnitCostCents = table.Column<long>(type: "INTEGER", nullable: false),
                    QtyReceived = table.Column<long>(type: "INTEGER", nullable: false),
                    QtyRemaining = table.Column<long>(type: "INTEGER", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLots", x => x.Id);
                    table.CheckConstraint("CK_Lot_Qty", "QtyRemaining >= 0 AND QtyRemaining <= QtyReceived AND UnitCostCents >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryLots_DeliveryItems_DeliveryItemId",
                        column: x => x.DeliveryItemId,
                        principalTable: "DeliveryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    LotId = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitCostCents = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitPriceCents = table.Column<long>(type: "INTEGER", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: true),
                    DeliveryId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "Deliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleItemLots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SaleItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    LotId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitCostCents = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleItemLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleItemLots_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleItemLots_SaleItems_SaleItemId",
                        column: x => x.SaleItemId,
                        principalTable: "SaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_DailyClosingId",
                table: "CashMovements",
                column: "DailyClosingId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_UserId",
                table: "CashMovements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequestEvents_CustomerRequestId",
                table: "CustomerRequestEvents",
                column: "CustomerRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_Barcode",
                table: "CustomerRequests",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_CategoryId",
                table: "CustomerRequests",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_NormalizedName_Status",
                table: "CustomerRequests",
                columns: new[] { "NormalizedName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_ProductId",
                table: "CustomerRequests",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosingPayments_DailyClosingId",
                table: "DailyClosingPayments",
                column: "DailyClosingId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosingPayments_PaymentMethodId",
                table: "DailyClosingPayments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_BusinessDate",
                table: "DailyClosings",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_CashRegisterId_Status",
                table: "DailyClosings",
                columns: new[] { "CashRegisterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_ClosedByUserId",
                table: "DailyClosings",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_OpenedByUserId",
                table: "DailyClosings",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_ReceivedAt",
                table: "Deliveries",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_StockingListId",
                table: "Deliveries",
                column: "StockingListId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_SupplierId_ReceivedAt",
                table: "Deliveries",
                columns: new[] { "SupplierId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_UserId",
                table: "Deliveries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryItems_DeliveryId",
                table: "DeliveryItems",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryItems_OwnerId",
                table: "DeliveryItems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryItems_ProductId",
                table: "DeliveryItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_DeliveryItemId",
                table: "InventoryLots",
                column: "DeliveryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_OwnerId",
                table: "InventoryLots",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_ProductId_OwnerId_QtyRemaining",
                table: "InventoryLots",
                columns: new[] { "ProductId", "OwnerId", "QtyRemaining" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_ProductId_ReceivedAt",
                table: "InventoryLots",
                columns: new[] { "ProductId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_SupplierId",
                table: "InventoryLots",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CreatedAt",
                table: "InventoryMovements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_DeliveryId",
                table: "InventoryMovements",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_LotId",
                table: "InventoryMovements",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_OwnerId_CreatedAt",
                table: "InventoryMovements",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ProductId_CreatedAt",
                table: "InventoryMovements",
                columns: new[] { "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_SaleId",
                table: "InventoryMovements",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_UserId",
                table: "InventoryMovements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Name",
                table: "PaymentMethods",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_CategoryId",
                table: "Products",
                columns: new[] { "IsActive", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_PreferredSupplierId",
                table: "Products",
                column: "PreferredSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_PriorityOwnerId",
                table: "Products",
                column: "PriorityOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SearchName",
                table: "Products",
                column: "SearchName");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItemLots_LotId",
                table: "SaleItemLots",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItemLots_SaleItemId",
                table: "SaleItemLots",
                column: "SaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_BusinessDate_ProductId",
                table: "SaleItems",
                columns: new[] { "BusinessDate", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_OwnerId_BusinessDate",
                table: "SaleItems",
                columns: new[] { "OwnerId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_ProductId_YearMonth",
                table: "SaleItems",
                columns: new[] { "ProductId", "YearMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId",
                table: "SaleItems",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BusinessDate",
                table: "Sales",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CancelledByUserId",
                table: "Sales",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CreatedAt",
                table: "Sales",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_DailyClosingId_Status",
                table: "Sales",
                columns: new[] { "DailyClosingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_PaymentMethodId",
                table: "Sales",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_UserId",
                table: "Sales",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockingListItems_CustomerRequestId",
                table: "StockingListItems",
                column: "CustomerRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_StockingListItems_OwnerId",
                table: "StockingListItems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_StockingListItems_ProductId",
                table: "StockingListItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockingListItems_StockingListId",
                table: "StockingListItems",
                column: "StockingListId");

            migrationBuilder.CreateIndex(
                name: "IX_StockingListItems_SupplierId",
                table: "StockingListItems",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_StockingLists_CreatedByUserId",
                table: "StockingLists",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockingLists_Status",
                table: "StockingLists",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSchedules_SupplierId_Day",
                table: "SupplierSchedules",
                columns: new[] { "SupplierId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "CustomerRequestEvents");

            migrationBuilder.DropTable(
                name: "DailyClosingPayments");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "SaleItemLots");

            migrationBuilder.DropTable(
                name: "StockingListItems");

            migrationBuilder.DropTable(
                name: "SupplierSchedules");

            migrationBuilder.DropTable(
                name: "InventoryLots");

            migrationBuilder.DropTable(
                name: "SaleItems");

            migrationBuilder.DropTable(
                name: "CustomerRequests");

            migrationBuilder.DropTable(
                name: "DeliveryItems");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "DailyClosings");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "StockingLists");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Owners");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "CashRegisters");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
