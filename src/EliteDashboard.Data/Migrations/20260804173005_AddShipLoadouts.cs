using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipLoadouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShipLoadouts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShipId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShipType = table.Column<string>(type: "TEXT", nullable: true),
                    SeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CargoCapacity = table.Column<double>(type: "REAL", nullable: false),
                    MaxJumpRange = table.Column<double>(type: "REAL", nullable: false),
                    FuelCapacityMain = table.Column<double>(type: "REAL", nullable: false),
                    FuelCapacityReserve = table.Column<double>(type: "REAL", nullable: false),
                    UnladenMass = table.Column<double>(type: "REAL", nullable: false),
                    Rebuy = table.Column<long>(type: "INTEGER", nullable: false),
                    HullValue = table.Column<long>(type: "INTEGER", nullable: false),
                    ModulesValue = table.Column<long>(type: "INTEGER", nullable: false),
                    ModulesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipLoadouts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipLoadouts_ShipId",
                table: "ShipLoadouts",
                column: "ShipId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipLoadouts");
        }
    }
}
