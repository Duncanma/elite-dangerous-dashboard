using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddColonisationDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ColonisationDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MarketId = table.Column<long>(type: "INTEGER", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CommodityName = table.Column<string>(type: "TEXT", nullable: false),
                    CommodityDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    JournalFile = table.Column<string>(type: "TEXT", nullable: false),
                    LineNumber = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColonisationDeliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColonisationDeliveries_JournalFile_LineNumber_CommodityName",
                table: "ColonisationDeliveries",
                columns: new[] { "JournalFile", "LineNumber", "CommodityName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColonisationDeliveries_MarketId",
                table: "ColonisationDeliveries",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_ColonisationDeliveries_TimestampUtc",
                table: "ColonisationDeliveries",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ColonisationDeliveries");
        }
    }
}
