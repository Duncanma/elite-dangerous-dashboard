using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPowerplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PowerplayStandingHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Power = table.Column<string>(type: "TEXT", nullable: false),
                    TotalMerits = table.Column<long>(type: "INTEGER", nullable: false),
                    MeritsDelta = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PowerplayStandingHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PowerplayStandings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Power = table.Column<string>(type: "TEXT", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalMerits = table.Column<long>(type: "INTEGER", nullable: false),
                    TimePledgedSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PowerplayStandings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PowerplayStandingHistories_TimestampUtc",
                table: "PowerplayStandingHistories",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PowerplayStandingHistories");

            migrationBuilder.DropTable(
                name: "PowerplayStandings");
        }
    }
}
