using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class JournalEntryAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AmountCents",
                table: "journalentries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "journalentries",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsClosing",
                table: "journalentries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Uzupełnienie istniejących danych (bez utraty): grosze z kwoty, data i znacznik zamknięcia roku z nagłówka księgowania.
            migrationBuilder.Sql("UPDATE journalentries SET AmountCents = CAST(ROUND(CAST(Amount AS REAL) * 100) AS INTEGER);");
            migrationBuilder.Sql("UPDATE journalentries SET Date = (SELECT t.Date FROM transactions t WHERE t.Id = journalentries.TransactionId), IsClosing = (SELECT CASE WHEN t.Number LIKE 'ZR/%' THEN 1 ELSE 0 END FROM transactions t WHERE t.Id = journalentries.TransactionId);");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CompanyId_Date",
                table: "transactions",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_journalentries_Company_Account_Date",
                table: "journalentries",
                columns: new[] { "CompanyId", "AccountId", "Date", "Side", "AmountCents" });

            migrationBuilder.CreateIndex(
                name: "IX_journalentries_Company_Date_Account",
                table: "journalentries",
                columns: new[] { "CompanyId", "Date", "AccountId", "Side", "AmountCents" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_CompanyId_Date",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_journalentries_Company_Account_Date",
                table: "journalentries");

            migrationBuilder.DropIndex(
                name: "IX_journalentries_Company_Date_Account",
                table: "journalentries");

            migrationBuilder.DropColumn(
                name: "AmountCents",
                table: "journalentries");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "journalentries");

            migrationBuilder.DropColumn(
                name: "IsClosing",
                table: "journalentries");
        }
    }
}
