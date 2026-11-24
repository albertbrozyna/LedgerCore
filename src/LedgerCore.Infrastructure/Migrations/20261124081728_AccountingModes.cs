using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountingModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KpirKind",
                table: "invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RyczaltRate",
                table: "invoices",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountingMode",
                table: "companies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KpirKind",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "RyczaltRate",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "AccountingMode",
                table: "companies");
        }
    }
}
