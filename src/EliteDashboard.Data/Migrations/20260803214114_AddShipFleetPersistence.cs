using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipFleetPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShipId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShipType = table.Column<string>(type: "TEXT", nullable: true),
                    ShipTypeDisplay = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    StarSystem = table.Column<string>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ships", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ships_ShipId",
                table: "Ships",
                column: "ShipId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ships");
        }
    }
}
