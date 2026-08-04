using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipStation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Station",
                table: "Ships",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Station",
                table: "Ships");
        }
    }
}
