using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalLineDedupKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JournalFile",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LineNumber",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JournalFile",
                table: "PowerplayStandingHistories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LineNumber",
                table: "PowerplayStandingHistories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_JournalFile_LineNumber",
                table: "Transactions",
                columns: new[] { "JournalFile", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PowerplayStandingHistories_JournalFile_LineNumber",
                table: "PowerplayStandingHistories",
                columns: new[] { "JournalFile", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEvents_JournalFile_LineNumber",
                table: "JournalEvents",
                columns: new[] { "JournalFile", "LineNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_JournalFile_LineNumber",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_PowerplayStandingHistories_JournalFile_LineNumber",
                table: "PowerplayStandingHistories");

            migrationBuilder.DropIndex(
                name: "IX_JournalEvents_JournalFile_LineNumber",
                table: "JournalEvents");

            migrationBuilder.DropColumn(
                name: "JournalFile",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "JournalFile",
                table: "PowerplayStandingHistories");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                table: "PowerplayStandingHistories");
        }
    }
}
