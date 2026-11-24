using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bankrules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MatchText = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bankrules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bankstatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    ClosingBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    ClosingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImportedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DuplicateCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bankstatements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "banktransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StatementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Counterparty = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CounterpartyAccount = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banktransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bankrules_CompanyId",
                table: "bankrules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_bankstatements_CompanyId",
                table: "bankstatements",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_bankstatements_CompanyId_AccountId_ImportedAt",
                table: "bankstatements",
                columns: new[] { "CompanyId", "AccountId", "ImportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_banktransactions_CompanyId",
                table: "banktransactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_banktransactions_CompanyId_AccountId_Hash",
                table: "banktransactions",
                columns: new[] { "CompanyId", "AccountId", "Hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_banktransactions_CompanyId_AccountId_Status_Date",
                table: "banktransactions",
                columns: new[] { "CompanyId", "AccountId", "Status", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_banktransactions_StatementId",
                table: "banktransactions",
                column: "StatementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bankrules");

            migrationBuilder.DropTable(
                name: "bankstatements");

            migrationBuilder.DropTable(
                name: "banktransactions");
        }
    }
}
