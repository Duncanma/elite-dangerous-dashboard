using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropShipLoadoutFuelCapacityReserve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FuelCapacityReserve",
                table: "ShipLoadouts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "FuelCapacityReserve",
                table: "ShipLoadouts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
