using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipSpecsAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ShipStaticSpecs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", nullable: true),
                    SizeClass = table.Column<int>(type: "INTEGER", nullable: false),
                    HullMass = table.Column<double>(type: "REAL", nullable: false),
                    HullCost = table.Column<long>(type: "INTEGER", nullable: false),
                    BaseShieldStrength = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseArmour = table.Column<int>(type: "INTEGER", nullable: false),
                    CrewSeats = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipStaticSpecs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipStaticSpecs_Symbol",
                table: "ShipStaticSpecs",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "ShipStaticSpecs");
        }
    }
}
