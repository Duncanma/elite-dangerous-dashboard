using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityStationData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommunityMaxPadSizeClass",
                table: "Stations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CommunityPlanetary",
                table: "Stations",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommunityMaxPadSizeClass",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "CommunityPlanetary",
                table: "Stations");
        }
    }
}
