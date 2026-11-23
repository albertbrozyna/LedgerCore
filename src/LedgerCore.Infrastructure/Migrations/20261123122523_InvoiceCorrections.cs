using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CorrectedAmount",
                table: "invoices",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrectedInvoiceId",
                table: "invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CorrectedInvoiceId",
                table: "invoices",
                column: "CorrectedInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_CorrectedInvoiceId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "CorrectedAmount",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "CorrectedInvoiceId",
                table: "invoices");
        }
    }
}
