using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorePos.Core.Migrations
{
    /// <inheritdoc />
    public partial class CajasYSueltos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UnitProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    PackProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitsPerPack = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitOwnerId = table.Column<int>(type: "INTEGER", nullable: true),
                    AutoOpen = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackLinks", x => x.Id);
                    table.CheckConstraint("CK_PackLink", "UnitsPerPack >= 2 AND UnitProductId <> PackProductId");
                    table.ForeignKey(
                        name: "FK_PackLinks_Owners_UnitOwnerId",
                        column: x => x.UnitOwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackLinks_Products_PackProductId",
                        column: x => x.PackProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackLinks_Products_UnitProductId",
                        column: x => x.UnitProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackLinks_PackProductId",
                table: "PackLinks",
                column: "PackProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PackLinks_UnitOwnerId",
                table: "PackLinks",
                column: "UnitOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PackLinks_UnitProductId",
                table: "PackLinks",
                column: "UnitProductId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackLinks");
        }
    }
}
