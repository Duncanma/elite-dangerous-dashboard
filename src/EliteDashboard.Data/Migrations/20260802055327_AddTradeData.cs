using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Commodities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InternalSymbol = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commodities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StarSystems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false),
                    Z = table.Column<double>(type: "REAL", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarSystems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeRoutePlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Engine = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartingCapital = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalHops = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedProfit = table.Column<long>(type: "INTEGER", nullable: false),
                    ActivatedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeRoutePlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MarketId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    StarSystemId = table.Column<long>(type: "INTEGER", nullable: false),
                    StationType = table.Column<string>(type: "TEXT", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdateSource = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stations_StarSystems_StarSystemId",
                        column: x => x.StarSystemId,
                        principalTable: "StarSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeRouteHops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TradeRoutePlanId = table.Column<long>(type: "INTEGER", nullable: false),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginStationId = table.Column<long>(type: "INTEGER", nullable: false),
                    DestinationStationId = table.Column<long>(type: "INTEGER", nullable: false),
                    CommodityId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlannedBuyPrice = table.Column<long>(type: "INTEGER", nullable: false),
                    PlannedSellPrice = table.Column<long>(type: "INTEGER", nullable: false),
                    PlannedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedProfit = table.Column<long>(type: "INTEGER", nullable: false),
                    BoughtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SoldUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeRouteHops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeRouteHops_TradeRoutePlans_TradeRoutePlanId",
                        column: x => x.TradeRoutePlanId,
                        principalTable: "TradeRoutePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketPriceQuotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StationId = table.Column<long>(type: "INTEGER", nullable: false),
                    CommodityId = table.Column<long>(type: "INTEGER", nullable: false),
                    BuyPrice = table.Column<long>(type: "INTEGER", nullable: false),
                    SellPrice = table.Column<long>(type: "INTEGER", nullable: false),
                    Demand = table.Column<long>(type: "INTEGER", nullable: false),
                    Supply = table.Column<long>(type: "INTEGER", nullable: false),
                    CollectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPriceQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketPriceQuotes_Commodities_CommodityId",
                        column: x => x.CommodityId,
                        principalTable: "Commodities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketPriceQuotes_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Commodities_InternalSymbol",
                table: "Commodities",
                column: "InternalSymbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceQuotes_CommodityId",
                table: "MarketPriceQuotes",
                column: "CommodityId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceQuotes_StationId_CommodityId",
                table: "MarketPriceQuotes",
                columns: new[] { "StationId", "CommodityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StarSystems_Name",
                table: "StarSystems",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stations_MarketId",
                table: "Stations",
                column: "MarketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stations_StarSystemId",
                table: "Stations",
                column: "StarSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeRouteHops_TradeRoutePlanId",
                table: "TradeRouteHops",
                column: "TradeRoutePlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketPriceQuotes");

            migrationBuilder.DropTable(
                name: "TradeRouteHops");

            migrationBuilder.DropTable(
                name: "Commodities");

            migrationBuilder.DropTable(
                name: "Stations");

            migrationBuilder.DropTable(
                name: "TradeRoutePlans");

            migrationBuilder.DropTable(
                name: "StarSystems");
        }
    }
}
