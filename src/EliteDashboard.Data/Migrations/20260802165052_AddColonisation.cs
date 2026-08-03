using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddColonisation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ColonisationSites",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MarketId = table.Column<long>(type: "INTEGER", nullable: false),
                    ConstructionProgress = table.Column<double>(type: "REAL", nullable: false),
                    ConstructionComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConstructionFailed = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColonisationSites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ColonisationSystemClaims",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StarSystem = table.Column<string>(type: "TEXT", nullable: false),
                    SystemAddress = table.Column<long>(type: "INTEGER", nullable: false),
                    ClaimedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Released = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColonisationSystemClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ColonisationResourceRequirements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ColonisationSiteId = table.Column<long>(type: "INTEGER", nullable: false),
                    CommodityName = table.Column<string>(type: "TEXT", nullable: false),
                    CommodityDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    RequiredAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    ProvidedAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    Payment = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColonisationResourceRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ColonisationResourceRequirements_ColonisationSites_ColonisationSiteId",
                        column: x => x.ColonisationSiteId,
                        principalTable: "ColonisationSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColonisationResourceRequirements_ColonisationSiteId",
                table: "ColonisationResourceRequirements",
                column: "ColonisationSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ColonisationSites_MarketId",
                table: "ColonisationSites",
                column: "MarketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColonisationSystemClaims_StarSystem",
                table: "ColonisationSystemClaims",
                column: "StarSystem");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ColonisationResourceRequirements");

            migrationBuilder.DropTable(
                name: "ColonisationSystemClaims");

            migrationBuilder.DropTable(
                name: "ColonisationSites");
        }
    }
}
