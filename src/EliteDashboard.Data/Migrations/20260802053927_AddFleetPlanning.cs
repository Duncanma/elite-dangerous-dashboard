using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShipUpgradePlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShipId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShipType = table.Column<string>(type: "TEXT", nullable: true),
                    ModuleSlot = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentModuleSymbol = table.Column<string>(type: "TEXT", nullable: true),
                    DesiredModuleSymbol = table.Column<string>(type: "TEXT", nullable: true),
                    DesiredEngineeringNotes = table.Column<string>(type: "TEXT", nullable: true),
                    EstimatedCost = table.Column<long>(type: "INTEGER", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipUpgradePlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipWishlistItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShipType = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    TargetLoadoutNotes = table.Column<string>(type: "TEXT", nullable: true),
                    EstimatedCost = table.Column<long>(type: "INTEGER", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PurchasedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipWishlistItems", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipUpgradePlans");

            migrationBuilder.DropTable(
                name: "ShipWishlistItems");
        }
    }
}
